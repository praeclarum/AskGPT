using System.Text;

namespace AskGPT;

class Formatter
{
    int bracketDepth = 0;
    TokenState state = TokenState.Trivia;
    readonly StringBuilder token = new StringBuilder();
    readonly FormattedWriter writer = new FormattedWriter();

    enum TokenState {
        Unknown,
        Trivia,
        Word,
        Number,
        OneTick,
        TwoTick,
        ThreeTick,
        Finished,
    }

    static readonly Dictionary<string, HashSet<string>> keywordsForProgrammingLanguage = new() {
        { "python", new() { "class", "def", "if", "import", "return", "while", "for", "break" } },
    };
    
    public void Append(string markdown)
    {
        for (var i = 0; i < markdown.Length; i++) {
            while (!Run(markdown[i])) {
                // Do nothing
            }
        }
    }

    public void Finish()
    {
        EndToken();
        state = TokenState.Finished;
    }

    void EndToken()
    {
        switch (state) {
            case TokenState.Unknown:
                break;
            case TokenState.Finished:
                break;
            case TokenState.Trivia:
                writer.Write(token.ToString(), TokenFormat.Body);                
                break;
            case TokenState.Word:
                writer.Write(token.ToString(), TokenFormat.Body);
                break;
            case TokenState.Number:
                writer.Write(token.ToString(), TokenFormat.Number);
                break;
            case TokenState.OneTick:
            case TokenState.TwoTick:
            case TokenState.ThreeTick:
                writer.Write(token.ToString(), TokenFormat.Markdown);
                break;            
            default:
                throw new NotImplementedException($"Cannot end state {state}");
        }
        token.Clear();
        state = TokenState.Unknown;
    }

    /// <summary>
    /// Returns true if `ch` was consumed.
    /// </summary>
    bool Run(char ch)
    {
        switch (state) {
            case TokenState.Finished:
                return true;
            case TokenState.Unknown:
                if (char.IsWhiteSpace(ch)) {
                    token.Append(ch);
                    state = TokenState.Trivia;
                }
                else if (char.IsDigit(ch)) {
                    token.Append(ch);
                    state = TokenState.Number;
                }
                else {
                    switch (ch) {
                        case '.':
                        case ',':
                        case ';':
                        case ':':
                        case '!':
                        case '?':
                            writer.Write(ch.ToString(), TokenFormat.Punctuation);
                            break;
                        case '(':
                        case '[':
                        case '{':
                            bracketDepth++;
                            writer.Write(ch.ToString(), TokenFormat.Bracket + bracketDepth);
                            break;
                        case ')':
                        case ']':
                        case '}':
                            writer.Write(ch.ToString(), TokenFormat.Bracket + bracketDepth);
                            bracketDepth--;
                            break;
                        case '`':
                            token.Append(ch);
                            state = TokenState.OneTick;
                            break;
                        default:
                            token.Append(ch);
                            state = TokenState.Word;
                            break;
                    }                    
                }
                return true;
            case TokenState.Trivia:
                if (char.IsWhiteSpace(ch)) {
                    token.Append(ch);
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.Word:
                if (char.IsLetterOrDigit(ch) || ch == '_') {
                    token.Append(ch);
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.Number:
                if (char.IsDigit(ch)) {
                    token.Append(ch);
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.OneTick:
                if (ch == '`') {
                    token.Append(ch);
                    state = TokenState.TwoTick;
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.TwoTick:
                if (ch == '`') {
                    token.Append(ch);
                    state = TokenState.ThreeTick;
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.ThreeTick:
                token.Append(ch);
                if (ch == '\n') {
                    EndToken();
                    return true;
                }
                else {
                    return true;
                }
            default:
                throw new NotImplementedException($"Unknown state {state}");
        }
    }
}

enum TokenFormat {
    Markdown,
    Body,
    Number,
    Punctuation,
    Bracket = 1000,
}

class FormattedWriter
{
    ConsoleColor[] bracketColors = new ConsoleColor[] {
        ConsoleColor.Yellow,
        ConsoleColor.Magenta,
        ConsoleColor.Cyan
    };
    public void Write(string token, TokenFormat format)
    {
        switch (format) {
            case TokenFormat.Markdown:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                break;
            case TokenFormat.Body:
                break;
            case TokenFormat.Number:
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                break;
            case TokenFormat.Punctuation:
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
            default:
                if (format >= TokenFormat.Bracket) {
                    var index = (int)format - (int)TokenFormat.Bracket;
                    Console.ForegroundColor = bracketColors[index % bracketColors.Length];
                }
                else {
                    throw new NotImplementedException($"Unknown format {format}");
                }
                break;
        }
        Console.Write(token);
        Console.ResetColor();
    }
}
