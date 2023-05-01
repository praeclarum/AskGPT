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

    List<(string text, TokenFormat format)> writeBuffer = new();

    FormatContext CurrentContextType => contextStack.Peek();

    bool InContext(FormatContext c) => contextStack.Contains(c);
    bool InCodeBlock => contextStack.Any(c => c.IsCodeBlock());
    bool InCodeish => contextStack.Any(c => c.IsCodeish());
    bool InCFamilyCode => InContext(FormatContext.CSharpCode);
    bool InUnixScriptCode => InContext(FormatContext.PythonCode);

    enum TokenState {
        Unknown,
        WS,
        OneNewline,
        ManyNewline,
        LineComment,
        Word,
        Url,
        Number,
        NumberWithDot,
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
        OneStar,
        Finished,
    }

    static readonly HashSet<string> codeKeywords;
    static readonly HashSet<string> codeValwords;

    static readonly HashSet<string> csharpKeywords = new() {
        "and", "as",
        "break", "byte",
        "case", "catch", "class", "continue",
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
        "class", "continue",
        "def",
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
        bracketDepthStack.Push(0);
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
            FlushWriteBuffer(GetStyle());
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
        writer.BeginContext(contextType);
    }
    void EndContext()
    {
        contextStack.Pop();
        bracketDepth = bracketDepthStack.Pop();
        writer.EndContext();
    }
    void EndContext(FormatContext upToContextType)
    {
        while (contextStack.Count > 1) {
            var t = contextStack.Peek();
            EndContext();
            if (t == upToContextType) {
                break;
            }
        }
    }

    void BeginParagraph()
    {
        // Reset the bracket depth
        bracketDepth = 0;
    }

    void Write(string text, TokenFormat format)
    {
        var style = GetStyle();
        if (format == TokenFormat.Identifier)
        {
            writeBuffer.Add((text, format));
        }
        else if (text == ".")
        {
            writeBuffer.Add((text, format));
        }
        else if (text == "(" && writeBuffer.Count > 0 && writeBuffer[^1].format == TokenFormat.Identifier)
        {
            var (lastText, lastFormat) = writeBuffer[^1];
            writeBuffer[^1] = (lastText, TokenFormat.Function);
            FlushWriteBuffer(style);
            writer.Write(text, format, style);
        }
        else
        {
            FlushWriteBuffer(style);
            writer.Write(text, format, style);
        }
    }

    void FlushWriteBuffer(TokenStyle style)
    {
        foreach (var (text, format) in writeBuffer) {
            writer.Write(text, format, style);
        }
        writeBuffer.Clear();
    }

    TokenStyle GetStyle()
    {
        var s = TokenStyle.None;
        if (InContext(FormatContext.Bold)) {
            s |= TokenStyle.Bold;
        }
        else if (InContext(FormatContext.Italic)) {
            s |= TokenStyle.Italic;
        }
        else if (InContext(FormatContext.Underline)) {
            s |= TokenStyle.Italic;
        }
        return s;
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
            case TokenState.OneNewline:
            case TokenState.ManyNewline:
                Write(tokenText, TokenFormat.Body);                
                break;
            case TokenState.LineComment:
                Write(tokenText, TokenFormat.Comment);
                break;
            case TokenState.Word:
                if (InCodeish) {
                    if (InContext(FormatContext.CSharpCode)) {
                        Write(tokenText, csharpKeywords.Contains(tokenText) ? TokenFormat.Keyword 
                            : (csharpValwords.Contains(tokenText) ? TokenFormat.Valword : TokenFormat.Identifier));
                    }
                    else if (InContext(FormatContext.PythonCode)) {
                        Write(tokenText, pythonKeywords.Contains(tokenText) ? TokenFormat.Keyword 
                            : (pythonValwords.Contains(tokenText) ? TokenFormat.Valword : TokenFormat.Identifier));                        
                    }
                    else {
                        Write(tokenText, TokenFormat.Identifier);
                    }
                }
                else {
                    Write(tokenText, TokenFormat.Body);
                }
                break;
            case TokenState.Url:
                Write(tokenText, TokenFormat.Url);
                break;
            case TokenState.Number:
            case TokenState.NumberWithDot:
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
            case TokenState.OneStar:
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
                if (ch == '\n') {
                    token.Append(ch);
                    state = TokenState.OneNewline;
                }
                else if (char.IsWhiteSpace(ch)) {
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
                        case '%':
                        case '&':
                        case '|':
                        case '^':
                        case '~':
                        case '<':
                        case '>':
                            Write(ch.ToString(), TokenFormat.Operator);
                            break;
                        case '*':
                            if (InContext(FormatContext.Bold) || InContext(FormatContext.Italic)) {
                                token.Append(ch);
                                state = TokenState.OneStar;
                            }
                            else {
                                Write(ch.ToString(), TokenFormat.Operator);
                            }
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
                            if (InUnixScriptCode || InCodeBlock) {
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
                if (ch == '\n') {
                    token.Append(ch);
                    state = TokenState.OneNewline;
                    return true;
                }
                else if (ch == '*') {
                    EndToken();
                    if (InCodeish) {
                        return false;
                    }
                    token.Append(ch);
                    state = TokenState.OneStar;
                    return true;
                }
                else if (char.IsWhiteSpace(ch)) {
                    token.Append(ch);
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.OneNewline:
                if (ch == '\n') {
                    token.Append(ch);
                    state = TokenState.ManyNewline;
                    return true;
                }
                else if (ch == '*') {
                    EndToken();
                    if (InCodeish) {
                        return false;
                    }
                    token.Append(ch);
                    state = TokenState.OneStar;
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.ManyNewline:
                if (ch == '\n') {
                    token.Append(ch);
                    return true;
                }
                else if (ch == '*') {
                    EndToken();
                    if (InCodeish) {
                        return false;
                    }
                    token.Append(ch);
                    state = TokenState.OneStar;
                    return true;
                }
                else {
                    EndToken();
                    if (!InCodeish) {
                        BeginParagraph();
                    }
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
                else if (ch == ':' && !InCodeish && token.ToString() is string word &&
                        (word.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                         word.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                         word.Equals("ftp", StringComparison.OrdinalIgnoreCase) ||
                         word.Equals("file", StringComparison.OrdinalIgnoreCase))) {
                    token.Append(ch);
                    state = TokenState.Url;
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.Url:
                if (char.IsLetterOrDigit(ch) || ch == '/' || ch == ':' || ch == '?' || ch == '&' || ch == '=' || ch == '#' || ch == '.' || ch == ',' || ch == ';' || ch == '!' || ch == '-' || ch == '_' || ch == '~' || ch == '%' || ch == '*' || ch == '\'' || ch == '+' || ch == '@' || ch == '$' || ch == '^' || ch == '|' || ch == '`' || ch == '<' || ch == '>' || ch == '\\') {
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
                else if (ch == '.') {
                    token.Append(ch);
                    state = TokenState.NumberWithDot;
                    return true;
                }
                else {
                    EndToken();
                    return false;
                }
            case TokenState.NumberWithDot:
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
                    if (InContext(FormatContext.InlineCode)) {
                        EndContext(FormatContext.InlineCode);
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
                if (ch == '/' && (InCFamilyCode || InCodeBlock)) {
                    token.Append(ch);
                    state = TokenState.LineComment;
                }
                else {
                    EndToken();
                    return false;
                }
                return true;
            case TokenState.OneStar:
                if (ch == '*') {
                    token.Append(ch);
                    if (InContext(FormatContext.Bold)) {
                        EndContext(FormatContext.Bold);
                        EndToken();
                    }
                    else {
                        EndToken();
                        BeginContext(FormatContext.Bold);
                    }
                    return true;
                }
                else if (char.IsWhiteSpace(ch)) {
                    Write("*", TokenFormat.Operator);
                    token.Clear();
                    token.Append(ch);
                    state = TokenState.WS;
                    return true;
                }
                else {
                    EndToken();
                    if (InContext(FormatContext.Italic)) {
                        EndContext(FormatContext.Italic);
                    }
                    else {
                        BeginContext(FormatContext.Italic);
                    }
                    return false;
                }
            default:
                throw new NotImplementedException($"Unknown state {state}");
        }
    }
}

enum TokenFormat {
    Markdown,
    Body,
    Url,
    Underline,
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

[Flags]
enum TokenStyle {
    None = 0,
    Bold = 0x1,
    Dimmed = 0x2,
    Italic = 0x4,
    Underline = 0x8,
}

enum FormatContext {
    Text,
    Bold,
    Italic,
    Underline,
    InlineCode,
    Code = 1000,
    CSharpCode,
    PythonCode,
}

static class FormatContextExtensions
{
    public static bool IsCodeish(this FormatContext contextType)
    {
        return contextType >= FormatContext.Code || contextType == FormatContext.InlineCode;
    }
    public static bool IsCodeBlock(this FormatContext contextType)
    {
        return contextType >= FormatContext.Code;
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
    public abstract void Write(string token, TokenFormat format, TokenStyle style);
    public virtual void Finish() {}
}

class ConsoleWriter : FormattedWriter
{
    readonly int width = Console.WindowWidth;
    int column = 0;
    const bool showMarkdown = false;
    ConsoleColor[] bracketColors = new ConsoleColor[] {
        ConsoleColor.DarkYellow,
        ConsoleColor.DarkMagenta,
        ConsoleColor.DarkCyan
    };
    public override void Write(string token, TokenFormat format, TokenStyle style)
    {
        var needsResetAfter = false;
        if (style.HasFlag(TokenStyle.Bold)) {
            Console.Write("\u001B[1m");
            needsResetAfter = true;
        }
        if (style.HasFlag(TokenStyle.Dimmed)) {
            Console.Write("\u001B[2m");
            needsResetAfter = true;
        }
        if (style.HasFlag(TokenStyle.Italic)) {
            Console.Write("\u001B[3m");
            needsResetAfter = true;
        }
        if (style.HasFlag(TokenStyle.Underline)) {
            Console.Write("\u001B[4m");
            needsResetAfter = true;
        }
        switch (format) {
            case TokenFormat.Markdown:
                if (showMarkdown) {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
                }
                else {
                    return;
                }
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
            case TokenFormat.Url:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\u001B[4m");
                needsResetAfter = true;
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
        if (CurrentContextType.IsCodeBlock()) {
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
            if (needsResetAfter) {
                Console.Write("\u001B[0m");
            }
        }
    }

    public override void Finish()
    {
        Console.WriteLine();
    }
}
