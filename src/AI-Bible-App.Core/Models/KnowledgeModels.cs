namespace AI_Bible_App.Core.Models
{
    public class KnowledgeDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public IDictionary<string, string>? Metadata { get; set; }
    }

    public class MemoryRecord
    {
        public string Key { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
