namespace TeamsBot.Models
{
    /// <summary>
    /// Represents details extracted from a facilitator prompt for action item creation
    /// Enhanced with AI-powered extraction capabilities
    /// </summary>
    public class ActionItemDetails
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";
        public string? AssignedTo { get; set; }
        public string WorkItemType { get; set; } = "Task";
        public string? EstimatedEffort { get; set; }
        public string? DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
