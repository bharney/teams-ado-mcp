namespace TeamsBot.Models
{
    /// <summary>
    /// Result of intent detection analysis for conversation intelligence
    /// Following Azure MCP patterns for structured AI responses
    /// </summary>
    public class IntentDetectionResult
    {
        public bool IsFacilitatorPrompt { get; set; }
        public float Confidence { get; set; }
        public string Intent { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
    }
}
