namespace TeamsBot.Models
{
    /// <summary>
    /// Represents Azure DevOps work item information
    /// </summary>
    public class WorkItemInfo
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string WorkItemType { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ChangedDate { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}
