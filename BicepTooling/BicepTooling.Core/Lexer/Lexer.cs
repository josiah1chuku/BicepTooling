namespace BicepTooling.Lexer;
public class Lexer
{
    private readonly string _source;
    private int _pos, _line, _column, _start;
    private static readonly Dictionary<string, TokenKind> Keywords = new()
    {
        ["param"] = TokenKind.Param,
        ["var"] = TokenKind.Var,
        ["resource"] = TokenKind.Resource,
        ["module"] = TokenKind.Module,
        ["output"] = TokenKind.Output,
        ["existing"] = TokenKind.Existing,
        ["import"] = TokenKind.Import,
        ["metadata"] = TokenKind.Metadata,
        ["type"] = TokenKind.Type,
        ["if"] = TokenKind.If,
        ["for"] = TokenKind.For,
        ["in"] = TokenKind.In,
        ["true"] = TokenKind.True,
        ["false"] = TokenKind.False,
        ["null"] = TokenKind.Null,
        ["string"] = TokenKind.StringType,
        ["int"] = TokenKind.IntType,
        ["bool"] = TokenKind.BoolType,
        ["object"] = TokenKind.ObjectType,
        ["array"] = TokenKind.ArrayType,
    };
    public Lexer(string source) { _source = source; _pos = 0; _line = 1; _column = 1; _start = 0; }
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (!IsAtEnd()) { _start = _pos; var t = ScanToken(); if (t != null) tokens.Add(t); }
        tokens.Add(new Token(TokenKind.EndOfFile, string.Empty, _line, _column, _pos));
        return tokens;
    }
    private Token? ScanToken()
    {
        char c = Advance();
        if (c == ' ' || c == '\t' || c == '\r') return SkipWhitespace();
        if (c == '\n') return HandleNewLine();
        if (c == '/' && Peek() == '/') return ReadLineComment();
        if (c == '/' && Peek() == '*') return ReadBlockComment();
        if (c == '\'') return ReadString();
        if (char.IsDigit(c)) return ReadNumber();
        if (char.IsLetter(c) || c == '_') return ReadIdentifierOrKeyword();
        return c switch
        {
            '=' when Peek() == '=' => TwoChar(TokenKind.Equal),
            '=' when Peek() == '>' => TwoChar(TokenKind.Arrow),
            '=' => MakeToken(TokenKind.Assign),
            '!' when Peek() == '=' => TwoChar(TokenKind.NotEqual),
            '!' => MakeToken(TokenKind.Not),
            '<' when Peek() == '=' => TwoChar(TokenKind.LessOrEqual),
            '<' => MakeToken(TokenKind.LessThan),
            '>' when Peek() == '=' => TwoChar(TokenKind.GreaterOrEqual),
            '>' => MakeToken(TokenKind.GreaterThan),
            '&' when Peek() == '&' => TwoChar(TokenKind.And),
            '&' => MakeToken(TokenKind.Ampersand),
            '|' when Peek() == '|' => TwoChar(TokenKind.Or),
            '|' => MakeToken(TokenKind.Pipe),
            '.' when Peek() == '.' && PeekNext() == '.' => ThreeChar(TokenKind.Ellipsis),
            '.' => MakeToken(TokenKind.Dot),
            ':' when Peek() == ':' => TwoChar(TokenKind.DoubleColon),
            ':' => MakeToken(TokenKind.Colon),
            '(' => MakeToken(TokenKind.LeftParen),
            ')' => MakeToken(TokenKind.RightParen),
            '{' => MakeToken(TokenKind.LeftBrace),
            '}' => MakeToken(TokenKind.RightBrace),
            '[' => MakeToken(TokenKind.LeftBracket),
            ']' => MakeToken(TokenKind.RightBracket),
            ',' => MakeToken(TokenKind.Comma),
            '@' => MakeToken(TokenKind.At),
            '#' => MakeToken(TokenKind.Hash),
            '+' => MakeToken(TokenKind.Plus),
            '-' => MakeToken(TokenKind.Minus),
            '*' => MakeToken(TokenKind.Star),
            '%' => MakeToken(TokenKind.Percent),
            '?' => MakeToken(TokenKind.Question),
            _ => MakeToken(TokenKind.Unknown)
        };
    }
    private Token? SkipWhitespace()
    {
        while (!IsAtEnd() && (Peek() == ' ' || Peek() == '\t' || Peek() == '\r')) Advance();
        return null;
    }
    private Token HandleNewLine() { _line++; _column = 1; return MakeToken(TokenKind.NewLine); }
    private Token ReadLineComment()
    {
        Advance();
        while (!IsAtEnd() && Peek() != '\n') Advance();
        return MakeToken(TokenKind.Comment);
    }
    private Token ReadBlockComment()
    {
        Advance();
        while (!IsAtEnd()) {
            if (Peek() == '*' && PeekNext() == '/') { Advance(); Advance(); break; }
            if (Peek() == '\n') { _line++; _column = 1; }
            Advance();
        }
        return MakeToken(TokenKind.BlockComment);
    }
    private Token ReadString()
    {
        while (!IsAtEnd() && Peek() != '\'' && Peek() != '\n') Advance();
        if (!IsAtEnd()) Advance();
        return MakeToken(TokenKind.String);
    }
    private Token ReadNumber()
    {
        while (!IsAtEnd() && char.IsDigit(Peek())) Advance();
        return MakeToken(TokenKind.Integer);
    }
    private Token ReadIdentifierOrKeyword()
    {
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_')) Advance();
        string text = _source[_start.._pos];
        if (Keywords.TryGetValue(text, out TokenKind kind)) return MakeToken(kind);
        return MakeToken(TokenKind.Identifier);
    }
    private char Peek() => IsAtEnd() ? '\0' : _source[_pos];
    private char PeekNext() => (_pos + 1 >= _source.Length) ? '\0' : _source[_pos + 1];
    private char Advance() { char c = _source[_pos]; _pos++; _column++; return c; }
    private bool IsAtEnd() => _pos >= _source.Length;
    private Token MakeToken(TokenKind kind)
    {
        string text = _source[_start.._pos];
        return new Token(kind, text, _line, _column - text.Length, _start);
    }
    private Token TwoChar(TokenKind kind) { Advance(); return MakeToken(kind); }
    private Token ThreeChar(TokenKind kind) { Advance(); Advance(); return MakeToken(kind); }
}
