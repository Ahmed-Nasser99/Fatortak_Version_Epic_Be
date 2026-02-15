namespace fatortak.Dtos.GeminiDto
{
    public class VisualizationData
    {
        public string Type { get; set; } // "kpi" or "chart"
        public object Data { get; set; }
        public string? ChartType { get; set; } // "line", "bar", "pie"
        public string? Title { get; set; }
    }
}
