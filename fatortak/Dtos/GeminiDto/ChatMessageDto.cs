namespace fatortak.Dtos.GeminiDto
{
    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public VisualizationData? VisualizationData { get; set; }
    }
}
