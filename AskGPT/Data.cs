
using System.Text.Json.Serialization;

class Request
{
    [JsonPropertyName("model")]
    public string ModelId { get; set; } = "";
    [JsonPropertyName("messages")]
    public Message[] Messages { get; set; } = Array.Empty<Message>();
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

class Response
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("object")]
    public string Object { get; set; } = "";
    [JsonPropertyName("created")]
    public ulong Created { get; set; }
    [JsonPropertyName("error")]
    public Error? Error { get; set; }
    [JsonPropertyName("choices")]
    public Choice[] Choices { get; set; } = Array.Empty<Choice>();
}

class Choice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    [JsonPropertyName("message")]
    public Message? Message { get; set; }
    [JsonPropertyName("delta")]
    public Message? Delta { get; set; }
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

class Error
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

class Usage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

class HistoricMessage
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    [JsonPropertyName("message")]
    public Message Message { get; set; } = new();
}
