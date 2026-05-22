namespace BicepTooling.Lexer;

public class Token
{
    public TokenKind Kind     { get; }
    public string    Text     { get; }
    public int       Line     { get; }
    public int       Column   { get; }
    public int       Position { get; }

    public Token(TokenKind kind, string text, int line, int column, int position)
    {
        Kind = kind; Text = text; Line = line; Column = column; Position = position;
    }

    public string Location => $"[Line {Line}, Col {Column}]";
    public override string ToString() => $"{Kind}({Text}) {Location}";
    public static readonly Token EOF = new(TokenKind.EndOfFile, string.Empty, 0, 0, 0);
}
