namespace AI_Bible_App.Core.Models
{
    public sealed class CharacterPersona
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Tone { get; set; } = "neutral";
        public double Creativity { get; set; } = 0.5; // 0..1
        // If true the persona has an "unconscious" background process for implicit memory
        public bool HasUnconsciousProcesses { get; set; } = false;
        // optional short-term working memory size
        public int WorkingMemoryCapacity { get; set; } = 6;
    }
}
