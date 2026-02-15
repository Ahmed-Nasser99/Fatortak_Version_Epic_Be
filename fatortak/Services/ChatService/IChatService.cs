using fatortak.Dtos.GeminiDto;
using fatortak.Entities;

namespace fatortak.Services.ChatService
{
    public interface IChatService
    {
        Task<ChatSession> CreateNewSessionAsync(string title);
        Task SaveMessageAsync(Guid sessionId, string content, string role, string metadata = null, VisualizationData? visualizationData = null);
        Task<List<ChatSession>> GetUserSessionsAsync();
        Task<List<ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId);
        Task DeleteSessionAsync(Guid sessionId);
    }
}
