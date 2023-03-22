using System.Text;

namespace AskGPT;

class Formatter
{
    int bracketDepth = 0;
    TokenState state = TokenState.WS;
    readonly Stack<FormatContext> contextStack = new();
    readonly Stack<int> bracketDepthStack = new();
    readonly StringBuilder token = new StringBuilder();
    readonly FormattedWriter writer = new ConsoleWriter();

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
        OneSingleQuote,
        OneSingleQuoteInterior,
        OneForwardSlash,
        Finished,
    }

    static readonly HashSet<string> codeKeywords;
    static readonly HashSet<string> codeValwords;

    static readonly HashSet<string> csharpKeywords = new() {
        "and", "as",
        "break", "byte",
        "case", "catch", "class",
        "do", "double",
        "else",
        "finally", "float", "for", "from",
        "global", "goto", 
        "if", "is", "in", "int",
        "let", "lock", "long",
        "new", "not",
        "or",
        "private", "protected", "public",
        "return",
        "short", "static", "string", "struct", "switch",
        "try",
        "var", "void",
        "while", "with",
    };
    static readonly HashSet<string> csharpValwords = new() {
        "true", "false", "null",
    };

    static readonly HashSet<string> pythonKeywords = new() {
        "and", "as", "assert",
        "break",
        "class", "def",
        "elif", "else", "except",
        "for", "from",
        "global", 
        "if", "import", "in", "is",
        "lambda",
        "not",
        "or",
        "return",
        "switch",
        "try",
        "while", "with",
    };
    static readonly HashSet<string> pythonValwords = new() {
        "True", "False", "None",
    };

    static Formatter()
    {
        codeKeywords = new HashSet<string>(csharpKeywords);
        codeKeywords.UnionWith(pythonKeywords);
        codeValwords = new HashSet<string>(csharpValwords);
        codeValwords.UnionWith(pythonValwords);
    }

    public Formatter() {
        contextStack.Push(FormatContext.Text);
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
            while (contextStack.Count > 1) {
                EndContext();
            }
            writer.Finish();
            state = TokenState.Finished;
        }
    }

    void BeginContext(FormatContext contextType)
    {
        contextStack.Push(contextType);
        bracketDepthStack.Push(bracketDepth);
        bracketDepth = 0;
        writer.BeginContext(contextType);
    }
    void EndContext()
    {
        contextStack.Pop();
        bracketDepth = bracketDepthStack.Pop();
        writer.EndContext();
    }
    FormatContext CurrentContextType => contextStack.Peek();

    bool InCode => CurrentContextType == FormatContext.Code;
    bool InCodeish => CurrentContextType >= FormatContext.Code;
    bool InCFamilyCode => CurrentContextType == FormatContext.CSharpCode;
    bool InUnixScriptCode => CurrentContextType == FormatContext.PythonCode;

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
                    case FormatContext.Text:
                        Write(tokenText, TokenFormat.Body);
                        break;
                    case FormatContext.Code:
                        Write(tokenText, codeKeywords.Contains(tokenText) ? TokenFormat.Keyword 
                            : (codeValwords.Contains(tokenText) ? TokenFormat.Valword : TokenFormat.Identifier));
                        break;
                    case FormatContext.CSharpCode:
                        Write(tokenText, csharpKeywords.Contains(tokenText) ? TokenFormat.Keyword 
                            : (csharpValwords.Contains(tokenText) ? TokenFormat.Valword : TokenFormat.Identifier));
                        break;
                    case FormatContext.PythonCode:
                        Write(tokenText, pythonKeywords.Contains(tokenText) ? TokenFormat.Keyword 
                            : (pythonValwords.Contains(tokenText) ? TokenFormat.Valword : TokenFormat.Identifier));
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
            case TokenState.OneSingleQuote:
            case TokenState.OneSingleQuoteInterior:
                Write(tokenText, TokenFormat.String);
                break;            
            case TokenState.OneTick:
            case TokenState.TwoTick:
            case TokenState.ThreeTick:
                Write(tokenText, TokenFormat.Markdown);
                break;
            case TokenState.OneForwardSlash:
                Write(tokenText, TokenFormat.Operator);
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
                            bracketDepth = Math.Max(0, bracketDepth - 1);
                            break;
                        case '=':
                        case '+':
                        case '-':
                        case '*':
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
                        case '/':
                            token.Append(ch);
                            state = TokenState.OneForwardSlash;
                            break;
                        case '\'':
                            if (CurrentContextType >= FormatContext.Code) {
                                token.Append(ch);
                                state = TokenState.OneSingleQuote;
                            }
                            else {
                                Write("'", TokenFormat.Body);
                            }
                            break;
                        case '#':
                            if (InUnixScriptCode || InCode) {
                                token.Append(ch);
                                state = TokenState.LineComment;
                            }
                            else {
                                Write("#", TokenFormat.Operator);
                            }
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
                    if (CurrentContextType == FormatContext.InlineCode) {
                        EndContext();
                    }
                    else {
                        BeginContext(FormatContext.InlineCode);
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
                    if (CurrentContextType >= FormatContext.Code) {
                        EndContext();
                    }
                    else {
                        switch (tokenText) {
                            case "```csharp":
                            case "``` csharp":
                            case "```cs":
                            case "``` cs":
                                BeginContext(FormatContext.CSharpCode);
                                break;
                            case "```python":
                            case "``` python":
                            case "```py":
                            case "``` py":
                                BeginContext(FormatContext.PythonCode);
                                break;
                            default:
                                BeginContext(FormatContext.Code);
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
            case TokenState.OneSingleQuote:
                token.Append(ch);
                if (ch == '\'') {
                    EndToken();
                }
                else {
                    state = TokenState.OneSingleQuoteInterior;
                }
                return true;
            case TokenState.OneSingleQuoteInterior:
                token.Append(ch);
                if (ch == '\'') {
                    EndToken();
                }
                return true;
            case TokenState.OneForwardSlash:
                if (ch == '/' && (InCFamilyCode || InCode)) {
                    token.Append(ch);
                    state = TokenState.LineComment;
                }
                else {
                    EndToken();
                    return false;
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
    Valword,
    Function,
    Punctuation,
    Operator,
    Comment,
    Bracket = 1000,
}

enum FormatContext {
    Text,
    InlineCode,
    Code = 1000,
    CSharpCode,
    PythonCode,
}

static class FormatContextExtensions
{
    public static bool IsCodeish(this FormatContext contextType)
    {
        return contextType >= FormatContext.Code;
    }
    public static bool IsCode(this FormatContext contextType)
    {
        return contextType == FormatContext.Code;
    }
}

abstract class FormattedWriter
{
    readonly Stack<FormatContext> contextStack = new();
    protected FormattedWriter()
    {
        contextStack.Push(FormatContext.Text);        
    }
    public void BeginContext(FormatContext contextType)
    {
        contextStack.Push(contextType);
    }
    public void EndContext()
    {
        contextStack.Pop();
    }
    public FormatContext CurrentContextType => contextStack.Peek();
    public abstract void Write(string token, TokenFormat format);
    public virtual void Finish() {}
}

class ConsoleWriter : FormattedWriter
{
    readonly int width = Console.WindowWidth;
    int column = 0;
    ConsoleColor[] bracketColors = new ConsoleColor[] {
        ConsoleColor.DarkYellow,
        ConsoleColor.DarkMagenta,
        ConsoleColor.DarkCyan
    };
    public override void Write(string token, TokenFormat format)
    {
        switch (format) {
            case TokenFormat.Markdown:
                // Console.ForegroundColor = ConsoleColor.DarkGray;
                // break;
                return;
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
            case TokenFormat.Valword:
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
        if (CurrentContextType.IsCodeish()) {
            // Console.BackgroundColor = ConsoleColor.DarkGray;
            var newlineIndex = token.LastIndexOf('\n');
            column = newlineIndex >= 0 ? token.Length - newlineIndex - 1 : column + token.Length;
            Console.Write(token);
        }
        else {
            // Console.BackgroundColor = ConsoleColor.Black;
            var tokenLines = token.Split('\n');
            var lineHead = "";
            foreach (var line in tokenLines) {
                Console.Write(lineHead);
                if (lineHead.Length > 0) {
                    column = 0;
                }
                var words = line.Split(' ');
                var head = "";
                foreach (var word in words) {
                    var columnAfterWrite = column + word.Length + head.Length;
                    if (columnAfterWrite > width) {
                        Console.WriteLine();
                        head = "";
                        column = word.Length;
                    }
                    else {
                        column = columnAfterWrite;
                    }
                    Console.Write(head);
                    Console.Write(word);
                    head = " ";
                }
                lineHead = "\n";
            }
        }
    }

    public override void Finish()
    {
        Console.WriteLine();
    }
}
