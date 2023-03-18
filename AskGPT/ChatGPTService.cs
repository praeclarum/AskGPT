using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAI.ChatGPT;

public class ChatGPTService
{
    readonly string apiKey;
    readonly HttpClient httpClient;

    public ChatGPTService(string apiKey, HttpClient httpClient)
    {
        this.apiKey = apiKey;
        this.httpClient = httpClient;
    }

    public HttpRequestMessage CreateHttpRequest(Request requestData)
    {
        var requestJson = JsonSerializer.Serialize(requestData);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
        return request;
    }

    public async Task<Response> GetCompletionAsync(Message[] messages, string modelId)
    {
        DateTimeOffset promptTimestamp = DateTimeOffset.Now;
        var requestData = new Request()
        {
            ModelId = modelId,
            Messages = messages,
            Stream = false,
        };
        var request = CreateHttpRequest(requestData);
        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Request failed with status code {response.StatusCode}:\n\n{await response.Content.ReadAsStringAsync()}");
        }
        var responseJson = await response.Content.ReadAsStringAsync();
        var responseObj = JsonSerializer.Deserialize<Response>(responseJson);
        return responseObj ?? throw new Exception("Failed to deserialize response");
    }
}

public class Request
{
    [JsonPropertyName("model")]
    public string ModelId { get; set; } = "";
    [JsonPropertyName("messages")]
    public Message[] Messages { get; set; } = Array.Empty<Message>();
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

public class Response
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

public class Choice
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

public class Error
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public class Usage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}
