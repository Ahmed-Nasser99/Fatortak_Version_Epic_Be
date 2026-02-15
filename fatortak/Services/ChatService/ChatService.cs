using fatortak.Context;
using fatortak.Dtos.GeminiDto;
using fatortak.Entities;
using fatortak.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace fatortak.Services.ChatService
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ChatService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;


        public async Task<ChatSession> CreateNewSessionAsync(string title)
        {
            var userId = UserHelper.GetUserId();
            var _userId = new Guid(userId);

            var session = new ChatSession
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                UserId = _userId,
                Title = title,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();

            return session;
        }

        public async Task SaveMessageAsync(Guid sessionId, string content, string role, string metadata = null, VisualizationData? visualizationData = null)
        {
            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Content = content,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                Metadata = metadata,
                VisualizationData = visualizationData != null
                ? JsonSerializer.Serialize(visualizationData)
                : null,
            };

            _context.ChatMessages.Add(message);

            // Update session's UpdatedAt
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<ChatSession>> GetUserSessionsAsync()
        {
            var userId = UserHelper.GetUserId();
            var _userId = new Guid(userId);

            return await _context.ChatSessions
                .Where(s => s.TenantId == _tenantId && s.UserId == _userId && !s.IsDeleted)
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync();
        }

        public async Task<List<ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
            return messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
                VisualizationData = string.IsNullOrEmpty(m.VisualizationData)
        ? null
        : JsonSerializer.Deserialize<VisualizationData>(m.VisualizationData)
            }).ToList();
        }

        public async Task DeleteSessionAsync(Guid sessionId)
        {
            var userId = UserHelper.GetUserId();
            var _userId = new Guid(userId);
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session != null && session.TenantId == _tenantId && session.UserId == _userId)
            {
                session.IsDeleted = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}
