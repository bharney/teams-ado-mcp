using System.Text.Json;
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

    public T GetValue<T>(string key, bool required = true)
    {
        if (!_parameters.TryGetValue(key, out var value))
        {
            if (required)
                throw new McpToolException($"Required parameter '{key}' not provided");
            return default(T)!;
        }

        // Handle direct type match first
        if (value is T typedValue)
        {
            return typedValue;
        }

        // Handle JsonElement conversion for JSON deserialization scenarios
        if (value is JsonElement jsonElement)
        {
            try
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)jsonElement.GetString()!;
                }
                if (typeof(T) == typeof(int))
                {
                    return (T)(object)jsonElement.GetInt32();
                }
                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)jsonElement.GetBoolean();
                }
                if (typeof(T) == typeof(double))
                {
                    return (T)(object)jsonElement.GetDouble();
                }
                // Add more type conversions as needed
            }
            catch (Exception ex)
            {
                if (required)
                    throw new McpToolException($"Parameter '{key}' cannot be converted to type {typeof(T).Name}: {ex.Message}");
                return default(T)!;
            }
        }

        if (required)
            throw new McpToolException($"Parameter '{key}' is not of expected type {typeof(T).Name}");
        
        return default(T)!;
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
