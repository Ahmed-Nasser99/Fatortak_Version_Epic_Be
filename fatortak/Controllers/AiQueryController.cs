using fatortak.Context;
using fatortak.Dtos.GeminiDto;
using fatortak.Dtos.Invoice;
using fatortak.Entities;
using fatortak.Services.ChatService;
using fatortak.Services.GeminiService;
using fatortak.Services.QuotaService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AiQueryController : ControllerBase
    {
        private readonly IQuotaService _quota;
        private readonly GeminiService _gemini;
        private readonly IChatService _chatService;
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AiQueryController> _logger;

        public AiQueryController(
            GeminiService gemini,
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IQuotaService quota,
            IChatService chatService,
            ILogger<AiQueryController> logger)
        {
            _gemini = gemini;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _quota = quota;
            _chatService = chatService;
            _logger = logger;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] GeminiRequest request)
        {
            try
            {
                // Step 1: Session management
                var sessionId = request.SessionId ?? Guid.NewGuid();
                if (request.SessionId == null)
                {
                    var firstMessage = request.ChatHistory.FirstOrDefault()?.Content ?? request.Query;
                    var title = firstMessage.Length > 50
                        ? firstMessage.Substring(0, 47) + "..."
                        : firstMessage;
                    var newSession = await _chatService.CreateNewSessionAsync(title);
                    sessionId = newSession.Id;
                }

                // Step 2: Save user message
                await _chatService.SaveMessageAsync(
                    sessionId,
                    request.Query,
                    "user",
                    JsonSerializer.Serialize(new { Tokens = request.Query.Length / 4 }));

                // Step 3: Generate SQL query with retry logic
                _logger.LogInformation("Generating SQL for query: {Query}", request.Query);

                string sql = null;
                Exception lastException = null;

                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        sql = await _gemini.GetSqlQueryAsync(request.Query, request.ChatHistory);

                        if (string.IsNullOrWhiteSpace(sql))
                        {
                            throw new InvalidOperationException("Empty SQL generated");
                        }

                        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Generated SQL is not a SELECT statement");
                        }

                        _logger.LogInformation("Generated SQL (attempt {Attempt}): {SQL}", attempt, sql);
                        break; // Success
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.LogWarning(ex, "SQL generation attempt {Attempt} failed for query: {Query}", attempt, request.Query);

                        if (attempt == 2)
                        {
                            // Last attempt failed
                            _logger.LogError("All SQL generation attempts failed for query: {Query}", request.Query);
                            return BadRequest(new { error = $"Failed to understand your question. Error: {lastException?.Message}" });
                        }

                        // Add context for retry
                        request.ChatHistory.Add(new RequestChatMessage
                        {
                            Role = "user",
                            Content = "The previous query had an error. Please generate a simpler SELECT query without UNION unless absolutely necessary."
                        });
                    }
                }

                // Step 4: Execute SQL and get ALL results (no truncation)
                List<Dictionary<string, object>> allRows;
                try
                {
                    allRows = await _gemini.ExecuteSqlAsync(_context, sql);
                    _logger.LogInformation("Query returned {Count} rows", allRows?.Count ?? 0);
                }
                catch (Microsoft.Data.SqlClient.SqlException sqlEx)
                {
                    _logger.LogError(sqlEx, "SQL execution error. Query: {Query}, SQL: {SQL}", request.Query, sql);

                    // User-friendly error messages
                    var errorMessage = sqlEx.Message.Contains("UNION")
                        ? "There was an issue with the query structure. Please try asking in a simpler way."
                        : sqlEx.Message.Contains("syntax")
                        ? "Invalid query syntax. Please rephrase your question."
                        : "Database error occurred. Please try again.";

                    return BadRequest(new
                    {
                        error = errorMessage,
                        details = _logger.IsEnabled(LogLevel.Debug) ? sqlEx.Message : null
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error executing SQL: {SQL}", sql);
                    return BadRequest(new { error = "Error executing query. Please try rephrasing your question." });
                }

                // Step 5: Check if we have data
                if (allRows == null || allRows.Count == 0)
                {
                    var noDataMessage = request.Query.Contains("عربي") || request.Query.Contains("العملاء") || request.Query.Contains("المبيعات")
                        ? "لم يتم العثور على بيانات لهذا الاستعلام."
                        : "No data found for this query.";

                    await _chatService.SaveMessageAsync(
                        sessionId,
                        noDataMessage,
                        "assistant",
                        JsonSerializer.Serialize(new { Tokens = 0, SqlQuery = sql }));

                    return Ok(new AiResponseDto
                    {
                        Message = noDataMessage,
                        SessionId = sessionId,
                        VisualizationData = null
                    });
                }

                // Step 6: Generate AI summary with full data (AI will sample internally)
                var (summary, visualData) = await _gemini.GetArabicSummaryWithVisualsAsync(
                    request.Query,
                    allRows,  // Pass ALL rows - service handles sampling
                    request.ChatHistory,
                    sql);

                // Step 7: Record usage and save response
                await _quota.RecordAiUsageAsync(_tenantId);

                var metadata = new
                {
                    Tokens = summary?.Length / 4 ?? 0,
                    SqlQuery = sql,
                    RowCount = allRows.Count,
                    HasVisualization = visualData != null,
                    VisualizationType = visualData?.Type
                };

                await _chatService.SaveMessageAsync(
                    sessionId,
                    summary ?? "Response generated",
                    "assistant",
                    JsonSerializer.Serialize(metadata),
                    visualData);

                _logger.LogInformation(
                    "Successfully processed query. Rows: {Count}, Has Visualization: {HasViz}",
                    allRows.Count,
                    visualData != null);

                return Ok(new AiResponseDto
                {
                    Message = summary,
                    SessionId = sessionId,
                    VisualizationData = visualData
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("tenant filtering"))
            {
                _logger.LogError(ex, "Security violation: SQL query missing tenant filter");
                return BadRequest(new { error = "Security error: Query must filter by tenant." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI query: {Query}", request.Query);
                return BadRequest(new { error = $"Error processing query: {ex.Message}" });
            }
        }

        [HttpPost("InvoiceOcr")]
        public async Task<ActionResult<OcrInvoiceCreateDto>> InvoiceOcr([FromForm] InvoiceOcrRequest dto)
        {
            try
            {
                if (dto.File == null || dto.File.Length == 0)
                {
                    return BadRequest(new { error = "No file uploaded" });
                }

                _logger.LogInformation("Processing OCR for file: {FileName}", dto.File.FileName);

                var data = await _gemini.ExtractInvoiceDataAsync(dto.File);

                _logger.LogInformation("OCR extraction successful");

                return Ok(data);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogWarning(ex, "Unsupported file type");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OCR extraction");
                return BadRequest(new { error = $"OCR extraction failed: {ex.Message}" });
            }
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            try
            {
                var sessions = await _chatService.GetUserSessionsAsync();
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sessions");
                return BadRequest(new { error = "Failed to retrieve sessions" });
            }
        }

        [HttpGet("messages/{sessionId}")]
        public async Task<IActionResult> GetMessages(Guid sessionId)
        {
            try
            {
                var messages = await _chatService.GetSessionMessagesAsync(sessionId);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages for session: {SessionId}", sessionId);
                return BadRequest(new { error = "Failed to retrieve messages" });
            }
        }

        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> DeleteSession(Guid sessionId)
        {
            try
            {
                await _chatService.DeleteSessionAsync(sessionId);
                _logger.LogInformation("Deleted session: {SessionId}", sessionId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting session: {SessionId}", sessionId);
                return BadRequest(new { error = "Failed to delete session" });
            }
        }
    }
}