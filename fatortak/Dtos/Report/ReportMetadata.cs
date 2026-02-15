using System.Collections.Generic;

namespace fatortak.Dtos.Report
{
    public class ReportMetadata
    {
        public string Title { get; set; }
        public string UserEmail { get; set; }
        public string DateRange { get; set; }
        public string GeneratedAt { get; set; }
        public string Language { get; set; } = "en"; // "en" or "ar"
        public List<ReportColumn> Columns { get; set; } = new List<ReportColumn>();
        public Dictionary<string, string> Filters { get; set; } = new Dictionary<string, string>();
    }

    public class ReportColumn
    {
        public string Header { get; set; }
        public string PropertyName { get; set; }
        public string Format { get; set; } // e.g., "C" for currency, "d" for date
    }
}
