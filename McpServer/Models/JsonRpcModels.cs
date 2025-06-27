using System.Text.Json.Serialization;

namespace McpServer.Models;

/// <summary>
/// JSON-RPC 2.0 request model following MCP protocol specification
/// </summary>
public record JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public object? Params { get; init; }

    [JsonPropertyName("id")]
    public object? Id { get; init; }
}

/// <summary>
/// JSON-RPC 2.0 response model following MCP protocol specification
/// </summary>
public record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("result")]
    public object? Result { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }

    [JsonPropertyName("id")]
    public object? Id { get; init; }
}

/// <summary>
/// JSON-RPC 2.0 error model
/// </summary>
public record JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("data")]
    public object? Data { get; init; }
}

/// <summary>
/// MCP tool execution parameters
/// </summary>
public record McpToolParameters
{
    private readonly Dictionary<string, object> _parameters = new();

    public McpToolParameters Add(string key, object value)
    {
        _parameters[key] = value;
        return this;
    }

    public T? Get<T>(string key)
    {
        if (_parameters.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (_parameters.TryGetValue(key, out var objValue) && objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    public Dictionary<string, object> ToDictionary() => new(_parameters);
}

/// <summary>
/// MCP tool execution result
/// </summary>
public record McpToolResult
{
    public bool Success { get; init; }
    public object? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public int? ErrorCode { get; init; }

    public static McpToolResult Successful(object? data = null) => new()
    {
        Success = true,
        Data = data
    };

    public static McpToolResult Failed(string errorMessage, int errorCode = -1) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };
}
