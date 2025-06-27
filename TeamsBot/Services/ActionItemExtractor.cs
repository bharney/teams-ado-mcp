using System.Text.Json;
using System.Text.RegularExpressions;
using TeamsBot.Models;

namespace TeamsBot.Services
{
    /// <summary>
    /// Service for extracting action items and work item information from conversation text
    /// Uses NLP techniques and pattern matching to identify tasks, bugs, and user stories
    /// </summary>
    public interface IActionItemExtractor
    {
        Task<TodoItem?> ExtractActionItemAsync(string text);
        Task<IEnumerable<TodoItem>> ExtractFromFacilitatorPromptAsync(MeetingContext context);
        Task<WorkItemType> ClassifyWorkItemTypeAsync(string text);
    }

    public class ActionItemExtractor : IActionItemExtractor
    {
        private readonly ILogger<ActionItemExtractor> _logger;

        // Patterns for work item type classification
        private static readonly Dictionary<WorkItemType, string[]> WorkItemPatterns = new()
        {
            [WorkItemType.Bug] = new[] { "bug", "error", "issue", "broken", "fix", "not working", "problem", "defect" },
            [WorkItemType.Task] = new[] { "task", "do", "implement", "create", "setup", "configure", "install", "update" },
            [WorkItemType.UserStory] = new[] { "user story", "feature", "requirement", "as a user", "need to", "want to", "should be able" },
            [WorkItemType.Epic] = new[] { "epic", "large feature", "milestone", "major work", "initiative" }
        };

        // Patterns for extracting action items
        private static readonly Regex ActionItemRegex = new(
            @"(?:action item|todo|task|need to|should|must|will|assign|responsible for|follow up)[\s\w]*:?\s*(.+?)(?:\.|$|;|\n)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AssigneeRegex = new(
            @"(?:assign(?:ed)?\s+to|@(\w+)|responsible:\s*(\w+)|owner:\s*(\w+))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ActionItemExtractor(ILogger<ActionItemExtractor> logger)
        {
            _logger = logger;
        }

        public async Task<TodoItem?> ExtractActionItemAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return null;

                // Remove common command prefixes
                var cleanedText = text.Replace("/create work item:", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace("/create task:", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace("/create bug:", "", StringComparison.OrdinalIgnoreCase)
                                     .Trim();

                if (string.IsNullOrWhiteSpace(cleanedText))
                    return null;

                var workItemType = await ClassifyWorkItemTypeAsync(cleanedText);
                var assignee = ExtractAssignee(text);
                var priority = ExtractPriority(text);

                return new TodoItem
                {
                    Title = ExtractTitle(cleanedText),
                    Description = cleanedText,
                    WorkItemType = workItemType,
                    Assignee = assignee,
                    Priority = priority,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting action item from text: {Text}", text);
                return null;
            }
        }

        public async Task<IEnumerable<TodoItem>> ExtractFromFacilitatorPromptAsync(MeetingContext context)
        {
            var items = new List<TodoItem>();

            try
            {
                var matches = ActionItemRegex.Matches(context.Message);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var actionText = match.Groups[1].Value.Trim();
                        var item = await ExtractActionItemAsync(actionText);
                        
                        if (item != null)
                        {
                            item.FacilitatorId = context.FacilitatorId;
                            item.ConversationId = context.ConversationId;
                            items.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting action items from facilitator prompt");
            }

            return items;
        }

        public async Task<WorkItemType> ClassifyWorkItemTypeAsync(string text)
        {
            var lowerText = text.ToLowerInvariant();

            // Check each work item type pattern
            foreach (var (workItemType, patterns) in WorkItemPatterns)
            {
                if (patterns.Any(pattern => lowerText.Contains(pattern)))
                {
                    return workItemType;
                }
            }

            // Default to Task if no specific pattern matches
            return WorkItemType.Task;
        }

        private static string ExtractTitle(string text)
        {
            // Take first sentence or up to 100 characters
            var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var title = sentences.FirstOrDefault()?.Trim() ?? text;

            if (title.Length > 100)
            {
                title = title.Substring(0, 97) + "...";
            }

            return title;
        }

        private static string? ExtractAssignee(string text)
        {
            var match = AssigneeRegex.Match(text);
            if (match.Success)
            {
                // Find the first non-empty group
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    if (!string.IsNullOrEmpty(match.Groups[i].Value))
                    {
                        return match.Groups[i].Value;
                    }
                }
            }
            return null;
        }

        private static string ExtractPriority(string text)
        {
            var lowerText = text.ToLowerInvariant();
            
            if (lowerText.Contains("critical") || lowerText.Contains("urgent") || lowerText.Contains("high priority"))
                return "High";
            
            if (lowerText.Contains("low priority") || lowerText.Contains("nice to have"))
                return "Low";
            
            return "Medium"; // Default priority
        }
    }
}
