namespace fatortak.Dtos.GeminiDto
{
    public class GeminiRequest
    {
        public string Query { get; set; }
        public Guid? SessionId { get; set; }
        public List<RequestChatMessage> ChatHistory { get; set; } = new List<RequestChatMessage>();
    }

    public class RequestChatMessage // New DTO for chat messages
    {
        public string Role { get; set; } // "user" or "model"
        public string Content { get; set; }
    }
}
