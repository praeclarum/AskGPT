/* Script to query ChatGPT from the command line */

using System.Text.Json;
using AskGPT;

string appName = "AskGPT";

string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
string configDir = Path.Combine(homeDir, ".config", "AskGPT");
Directory.CreateDirectory(configDir);

string apiKeyPath = Path.Combine(configDir, "apikey.txt");
string initialPromptPath = Path.Combine(configDir, "prompt.json");
string historyPath = Path.Combine(configDir, "history.jsonl");

const int maxHistory = 1000;

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
// Load all historical messages to give context to the network
//
List<HistoricMessage> history = new();
if (File.Exists(historyPath))
{
    history = File.ReadAllLines(historyPath)
        .Select(line => JsonSerializer.Deserialize<HistoricMessage>(line))
        .OfType<HistoricMessage>()
        .ToList();
}

if (true && history.Count > 101)
{
    for (int i = 1; i <= 101; i += 2)
    {
        string lastText = history[^i].Message.Content;
        var lastFormatter = new Formatter();
        lastFormatter.Append(lastText);
        lastFormatter.Finish();
    }
    
    return 0;
}

//
// Choose history to use - only the last 15 mins
//
var now = DateTimeOffset.Now;
var historyToUse = history
    .Where(message => (now - message.Timestamp).TotalMinutes <= 15)
    .ToArray();

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
DateTimeOffset promptTimestamp = DateTimeOffset.Now;

//
// Build the request
//
var requestData = new Request()
{
    ModelId = "gpt-3.5-turbo",
    Messages = initialPrompt.Concat(historyToUse.Select(x => x.Message)).Concat(new[] { promptMessage }).ToArray(),
    Stream = true,
};
var requestJson = JsonSerializer.Serialize(requestData);

//
// Make the request
//
var http = new HttpClient();
var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
request.Headers.Add("Authorization", $"Bearer {apiKey}");
request.Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
if (!response.IsSuccessStatusCode)
{
    Error($"Request failed with status code {response.StatusCode}:\n\n{await response.Content.ReadAsStringAsync()}");
}
string responseText = "";
var formatter = new Formatter();
using (var s = await response.Content.ReadAsStreamAsync()) {
    using (var sr = new StreamReader(s)) {
        while (!sr.EndOfStream) {
            var line = await sr.ReadLineAsync() ?? "";
            if (line.StartsWith("data: {")) {
                var deltaJson = line.Substring("data: ".Length);
                var delta = JsonSerializer.Deserialize<Response>(deltaJson);
                var content = delta?.Choices[0].Delta?.Content ?? "";
                responseText += content;
                formatter.Append(content);
            }
            else if (line.StartsWith("data: [DONE]")) {
                break;
            }
        }
    }
}
formatter.Finish();

//
// Add it to the history
//
history.Add(new HistoricMessage()
{
    Timestamp = promptTimestamp,
    Message = promptMessage,
});
history.Add(new HistoricMessage()
{
    Timestamp = DateTimeOffset.Now,
    Message = new Message
    {
        Role = "assistant",
        Content = responseText,
    }
});
history = history.Skip(Math.Max(0, history.Count - maxHistory)).ToList();
await File.WriteAllLinesAsync(historyPath, history.Select(message => JsonSerializer.Serialize(message)));

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
