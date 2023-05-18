public class PluginOptions
{
    public string? AuditQueue { get; set; }

    public string? ConnectionString { get; set; }
    public string? TestRunId { get; set; }
    public long? RunCount { get; set; }

    public string ApplyUniqueRunPrefix(string text)
    {
        return $"{RunCount:D3}.{text}";
    }
}
