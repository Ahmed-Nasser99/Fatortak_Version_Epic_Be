namespace fatortak.Dtos.GeminiDto
{
    public class AiResponseDto
    {
        public string Message { get; set; }
        public Guid SessionId { get; set; }
        public VisualizationData? VisualizationData { get; set; }
    }
}
