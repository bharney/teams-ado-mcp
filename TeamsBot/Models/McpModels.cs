namespace TeamsBot.Models
{
    /// <summary>
    /// Represents a work item type in Azure DevOps
    /// </summary>
    public enum WorkItemType
    {
        Task,
        Bug,
        UserStory,
        Epic
    }

    /// <summary>
    /// Enhanced todo item model for MCP integration
    /// Extends ActionItemDetails with additional MCP-specific fields
    /// </summary>
    public class TodoItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public WorkItemType WorkItemType { get; set; } = WorkItemType.Task;
        public string? Assignee { get; set; }
        public string Priority { get; set; } = "Medium";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? FacilitatorId { get; set; }
        public string? ConversationId { get; set; }
        public Dictionary<string, object> AdditionalProperties { get; set; } = new();
    }

    /// <summary>
    /// Meeting context for conversation intelligence
    /// </summary>
    public class MeetingContext
    {
        public string ConversationId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? FacilitatorId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<string> Participants { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Result from MCP work item creation
    /// </summary>
    public class WorkItemResult
    {
        public int? Id { get; set; }
        public string? Url { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}
