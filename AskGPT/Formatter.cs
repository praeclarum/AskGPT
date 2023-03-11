using System.Text;

namespace AskGPT;

class Formatter
{
    int bracketDepth = 0;
    TokenState state = TokenState.WS;
    Stack<ContextType> contextStack = new();
    readonly StringBuilder token = new StringBuilder();
    readonly FormattedWriter writer = new FormattedWriter();

    enum TokenState {
        Unknown,
        WS,
        LineComment,
        Word,
        Number,
        OneTick,
        TwoTick,
        ThreeTick,
        OneDoubleQuote,
        OneDoubleQuoteInterior,
        TwoDoubleQuote,
        ThreeDoubleQuoteInterior,
        ThreeDoubleQuoteInteriorOneDoubleQuote,
        ThreeDoubleQuoteInteriorTwoDoubleQuote,
        Finished,
    }

    enum ContextType {
        Text,
        InlineCode,
        Code = 1000,
        PythonCode,
    }

    static readonly HashSet<string> pythonKeywords = new() {
        "and", "as", "assert",
        "break",
        "class", "def",
        "for",
        "if", "import", "in", "is",
        "lambda",
        "not",
        "or",
        "return",
        "while",
    };

    static readonly Dictionary<string, HashSet<string>> keywordsForProgrammingLanguage = new() {
        { "python", pythonKeywords },
    };

    public Formatter() {
        contextStack.Push(ContextType.Text);
    }
    
    public void Append(string markdown)
    {
        for (var i = 0; i < markdown.Length; i++) {
            while (!Run(markdown[i])) {
                // Keep running until we've consumed the character
            }
        }
    }

    public void Finish()
    {
        if (state != TokenState.Finished) {
            EndToken();
            FlushWriteBuffer();
            state = TokenState.Finished;
        }
    }

    void BeginContext(ContextType contextType)
    {
        contextStack.Push(contextType);
    }
    void EndContext()
    {
        contextStack.Pop();
    }
    ContextType CurrentContextType => contextStack.Peek();

    List<(string text, TokenFormat format)> writeBuffer = new();

    void Write(string text, TokenFormat format)
    {
        if (format == TokenFormat.Identifier) {
            writeBuffer.Add((text, format));
        }
        else if (text == ".") {
            writeBuffer.Add((text, format));
        }
        else if (text == "(" && writeBuffer.Count > 0 && writeBuffer[^1].format == TokenFormat.Identifier) {
            var (lastText, lastFormat) = writeBuffer[^1];
            writeBuffer[^1] = (lastText, TokenFormat.Function);
            FlushWriteBuffer();
            writer.Write(text, format);
        }
        else {
            FlushWriteBuffer();
            writer.Write(text, format);
        }
    }

    void FlushWriteBuffer()
    {
        foreach (var (text, format) in writeBuffer) {
            writer.Write(text, format);
        }
        writeBuffer.Clear();
    }

    void EndToken()
    {
        var tokenText = token.ToString();
        switch (state) {
            case TokenState.Unknown:
                break;
            case TokenState.Finished:
                break;
            case TokenState.WS:
                Write(tokenText, TokenFormat.Body);                
                break;
            case TokenState.LineComment:
                Write(tokenText, TokenFormat.Comment);
                break;
            case TokenState.Word:
                switch (CurrentContextType) {
                    case ContextType.Text:
                        Write(tokenText, TokenFormat.Body);
                        break;
                    case ContextType.PythonCode:
                        Write(tokenText, pythonKeywords.Contains(tokenText) ? TokenFormat.Keyword : TokenFormat.Identifier);
                        break;
                    default:
                        Write(tokenText, TokenFormat.Identifier);
                        break;
                }
                break;
            case TokenState.Number:
                Write(tokenText, TokenFormat.Number);
                break;
            case TokenState.OneDoubleQuote:
            case TokenState.OneDoubleQuoteInterior:
            case TokenState.TwoDoubleQuote:
            case TokenState.ThreeDoubleQuoteInterior:
            case TokenState.ThreeDoubleQuoteInteriorOneDoubleQuote:
            case TokenState.ThreeDoubleQuoteInteriorTwoDoubleQuote:
                Write(tokenText, TokenFormat.String);
                break;            
            case TokenState.OneTick:
            case TokenState.TwoTick:
            case TokenState.ThreeTick:
                Write(tokenText, TokenFormat.Markdown);
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
                    state = TokenState.WS;
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
                            Write(ch.ToString(), TokenFormat.Punctuation);
                            break;
                        case '(':
                        case '[':
                        case '{':
                            bracketDepth++;
                            Write(ch.ToString(), TokenFormat.Bracket + bracketDepth);
                            break;
                        case ')':
                        case ']':
                        case '}':
                            Write(ch.ToString(), TokenFormat.Bracket + bracketDepth);
                            bracketDepth--;
                            break;
                        case '=':
                        case '+':
                        case '-':
                        case '*':
                        case '/':
                        case '%':
                        case '&':
                        case '|':
                        case '^':
                        case '~':
                        case '<':
                        case '>':
                            Write(ch.ToString(), TokenFormat.Operator);
                            break;
                        case '`':
                            token.Append(ch);
                            state = TokenState.OneTick;
                            break;
                        case '"':
                            token.Append(ch);
                            state = TokenState.OneDoubleQuote;
                            break;
                        case '#':
                            token.Append(ch);
                            state = TokenState.LineComment;
                            break;
                        default:
                            token.Append(ch);
                            state = TokenState.Word;
                            break;
                    }                    
                }
                return true;
            case TokenState.WS:
                if (char.IsWhiteSpace(ch)) {
                    token.Append(ch);
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.LineComment:
                if (ch == '\n') {
                    EndToken();
                    return false;
                }
                else {
                    token.Append(ch);
                    return true;
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
                    if (CurrentContextType == ContextType.InlineCode) {
                        EndContext();
                    }
                    else {
                        BeginContext(ContextType.InlineCode);
                    }
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
                    var tokenText = token.ToString().Trim();
                    EndToken();
                    if (CurrentContextType >= ContextType.Code) {
                        EndContext();
                    }
                    else {
                        switch (tokenText) {
                            case "```python":
                            case "``` python":
                            case "```py":
                            case "``` py":
                                BeginContext(ContextType.PythonCode);
                                break;
                            default:
                                BeginContext(ContextType.Code);
                                break;
                        }
                    }
                    return true;
                }
                else {
                    return true;
                }
            case TokenState.OneDoubleQuote:
                token.Append(ch);
                if (ch == '"') {
                    state = TokenState.TwoDoubleQuote;
                }
                else {
                    state = TokenState.OneDoubleQuoteInterior;
                }
                return true;
            case TokenState.OneDoubleQuoteInterior:
                token.Append(ch);
                if (ch == '"') {
                    EndToken();
                }
                return true;
            case TokenState.TwoDoubleQuote:
                if (ch == '"') {
                    token.Append(ch);
                    state = TokenState.ThreeDoubleQuoteInterior;
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.ThreeDoubleQuoteInterior:
                token.Append(ch);
                if (ch == '"') {
                    state = TokenState.ThreeDoubleQuoteInteriorOneDoubleQuote;
                }
                return true;
            case TokenState.ThreeDoubleQuoteInteriorOneDoubleQuote:
                token.Append(ch);
                if (ch == '"') {
                    state = TokenState.ThreeDoubleQuoteInteriorTwoDoubleQuote;
                }
                else {
                    state = TokenState.ThreeDoubleQuoteInterior;
                }
                return true;
            case TokenState.ThreeDoubleQuoteInteriorTwoDoubleQuote:
                token.Append(ch);
                if (ch == '"') {
                    EndToken();
                }
                else {
                    state = TokenState.ThreeDoubleQuoteInterior;
                }
                return true;
            default:
                throw new NotImplementedException($"Unknown state {state}");
        }
    }
}

enum TokenFormat {
    Markdown,
    Body,
    Number,
    String,
    Identifier,
    Keyword,
    Function,
    Punctuation,
    Operator,
    Comment,
    Bracket = 1000,
}

class FormattedWriter
{
    ConsoleColor[] bracketColors = new ConsoleColor[] {
        ConsoleColor.DarkYellow,
        ConsoleColor.DarkMagenta,
        ConsoleColor.DarkCyan
    };
    public void Write(string token, TokenFormat format)
    {
        switch (format) {
            case TokenFormat.Markdown:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                break;
            case TokenFormat.Body:
                Console.ResetColor();
                break;
            case TokenFormat.Comment:
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
            case TokenFormat.Keyword:
                Console.ForegroundColor = ConsoleColor.Magenta;
                break;
            case TokenFormat.Identifier:
                Console.ForegroundColor = ConsoleColor.Cyan;
                break;
            case TokenFormat.Function:
                Console.ForegroundColor = ConsoleColor.Green;
                break;
            case TokenFormat.Number:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case TokenFormat.String:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case TokenFormat.Operator:
                Console.ForegroundColor = ConsoleColor.Gray;
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
    }
}
