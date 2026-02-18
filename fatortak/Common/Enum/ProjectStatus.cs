namespace fatortak.Common.Enum
{
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum ProjectStatus
    {
        NotStarted,
        Active,
        Completed,
        OnHold,
        Cancelled,
        Settled,
        Archived,
        Draft
    }
}
