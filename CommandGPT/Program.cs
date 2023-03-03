/* Script to query ChatGPT from the command line */

using System.Text.Json;
using System.Text.Json.Serialization;

string appName = "CommandGPT";

string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
string configDir = Path.Combine(homeDir, ".config", "CommandGPT");
string apiKeyPath = Path.Combine(configDir, "apikey.txt");
string initialPromptPath = Path.Combine(configDir, "prompt.json");
string historyPath = Path.Combine(configDir, "history.jsonl");

//
// Load the API key
//
if (!File.Exists(apiKeyPath))
{
    Error($"No API key found. Please put your API key in a file located at:\n\n{apiKeyPath}\n\nYou can get your API key from:\n\nhttps://platform.openai.com/account/api-keys\n");
}
string apiKey = (await File.ReadAllTextAsync(apiKeyPath)).Trim();

//
// Load the initial prompt used to prime the network
//
Message[] initialPrompt = Array.Empty<Message>();
if (File.Exists(initialPromptPath))
{
    initialPrompt = JsonSerializer.Deserialize<Message[]>(File.ReadAllText(initialPromptPath)) ?? Array.Empty<Message>();
}

//
// Load historical messages to give context to the network
//
List<HistoricMessage> history = new();
if (File.Exists(historyPath))
{
    history = File.ReadAllLines(historyPath)
        .Select(line => JsonSerializer.Deserialize<HistoricMessage>(line))
        .OfType<HistoricMessage>()
        .ToList();
}

// string modelId = "gpt-3.5-turbo";

//
// Parse the prompt from the user
//
string prompt = String.Join(" ", args).Trim();
if (string.IsNullOrWhiteSpace(prompt))
{
    Error($"You didn't provide a prompt. Please provide a prompt as the arguments to this program.\n\nFor example:\n\n{appName} Hello, how are you?\n");
}
var promptMessage = new Message()
{
    Role = "user",
    Content = prompt
};

//
// Build the request
//
var requestData = new Request()
{
    ModelId = "gpt-3.5-turbo",
    Messages = initialPrompt.Concat(new[] { promptMessage }).ToArray()
};
var requestJson = JsonSerializer.Serialize(requestData);

//
// Make the request
//
var http = new HttpClient();
var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
request.Headers.Add("Authorization", $"Bearer {apiKey}");
request.Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
var response = http.Send(request);
var responseJson = await response.Content.ReadAsStringAsync();
if (!response.IsSuccessStatusCode)
{
    Error($"Request failed with status code {(int)response.StatusCode} {response.StatusCode}:\n\n{responseJson}");
}
var responseData = JsonSerializer.Deserialize<Response>(responseJson);
var choices = responseData?.Choices ?? Array.Empty<Choice>();
if (choices.Length == 0)
{
    Error($"No choices were returned by the API.");
}

//
// Print the response
//
var choice = choices[0];
var responseMessage = choice.Message;
var responseText = responseMessage.Content.Trim();
Console.WriteLine(responseText);

//
// Fini
//
return 0;

static void Error(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(message);
    Console.ResetColor();
    Environment.Exit(1);
}

class Request
{
    [JsonPropertyName("model")]
    public string ModelId { get; set; } = "";
    [JsonPropertyName("messages")]
    public Message[] Messages { get; set; } = Array.Empty<Message>();
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
    public Message Message { get; set; } = new();
    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = "";
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
}
