using System.Text.Json.Serialization;

namespace McpServer.Models;

/// <summary>
/// Azure DevOps REST API models for work item operations
/// </summary>
public class AdoWorkItemCreateRequest
{
    [JsonPropertyName("op")]
    public string Operation { get; set; } = "add";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

public class AdoWorkItemResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("rev")]
    public int Revision { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, object?> Fields { get; set; } = new();

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("_links")]
    public AdoWorkItemLinks? Links { get; set; }
}

public class AdoWorkItemLinks
{
    [JsonPropertyName("self")]
    public AdoLink? Self { get; set; }

    [JsonPropertyName("workItemUpdates")]
    public AdoLink? WorkItemUpdates { get; set; }

    [JsonPropertyName("workItemRevisions")]
    public AdoLink? WorkItemRevisions { get; set; }

    [JsonPropertyName("workItemComments")]
    public AdoLink? WorkItemComments { get; set; }

    [JsonPropertyName("html")]
    public AdoLink? Html { get; set; }

    [JsonPropertyName("workItemType")]
    public AdoLink? WorkItemType { get; set; }

    [JsonPropertyName("fields")]
    public AdoLink? Fields { get; set; }
}

public class AdoLink
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
}

public class AdoWorkItemsQueryResponse
{
    [JsonPropertyName("queryType")]
    public string QueryType { get; set; } = string.Empty;

    [JsonPropertyName("queryResultType")]
    public string QueryResultType { get; set; } = string.Empty;

    [JsonPropertyName("asOf")]
    public DateTime AsOf { get; set; }

    [JsonPropertyName("columns")]
    public List<AdoWorkItemColumn> Columns { get; set; } = new();

    [JsonPropertyName("workItems")]
    public List<AdoWorkItemReference> WorkItems { get; set; } = new();
}

public class AdoWorkItemColumn
{
    [JsonPropertyName("referenceName")]
    public string ReferenceName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class AdoWorkItemReference
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class AdoErrorResponse
{
    [JsonPropertyName("$id")]
    public string? Id { get; set; }

    [JsonPropertyName("innerException")]
    public object? InnerException { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("typeName")]
    public string TypeName { get; set; } = string.Empty;

    [JsonPropertyName("typeKey")]
    public string TypeKey { get; set; } = string.Empty;

    [JsonPropertyName("errorCode")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("eventId")]
    public int EventId { get; set; }
}
