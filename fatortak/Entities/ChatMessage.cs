using System.ComponentModel.DataAnnotations;

namespace fatortak.Entities
{
    public class ChatMessage
    {
        [Key]
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public string Content { get; set; }

        public string Role { get; set; } // "user" or "assistant"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string Metadata { get; set; }
        public string? VisualizationData { get; set; }
        public ChatSession Session { get; set; }
    }
}
