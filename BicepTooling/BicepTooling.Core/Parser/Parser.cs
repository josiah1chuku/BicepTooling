using BicepTooling.Lexer;
namespace BicepTooling.Parser;

public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _position;

    public Parser(IEnumerable<Token> tokens)
    {
        _tokens = tokens.ToList();
        _position = 0;
    }

    public CompilationUnitSyntax ParseCompilationUnit()
    {
        var statements = new List<StatementSyntax>();
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.NewLine  ||
                Current.Kind == TokenKind.Comment  ||
                Current.Kind == TokenKind.BlockComment)
            { Consume(); continue; }

            // Skip @decorator lines: @description('...') @allowed([...]) etc.
            if (Current.Kind == TokenKind.At)
            {
                Consume();
                if (Current.Kind == TokenKind.Identifier) Consume();
                if (Current.Kind == TokenKind.LeftParen)  SkipParentheses();
                continue;
            }

            var stmt = ParseStatement();
            if (stmt != null)
                statements.Add(stmt);
        }
        return new CompilationUnitSyntax(statements);
    }

    private StatementSyntax? ParseStatement()
    {
        return Current.Kind switch
        {
            TokenKind.Param    => ParseParameterDeclaration(),
            TokenKind.Var      => ParseVariableDeclaration(),
            TokenKind.Resource => ParseResourceDeclaration(),
            TokenKind.Output   => ParseOutputDeclaration(),
            _                  => SkipUnknown()
        };
    }

    private StatementSyntax? SkipUnknown() { Consume(); return null; }

    private ParameterDeclarationSyntax ParseParameterDeclaration()
    {
        Consume();
        var name = ParseIdentifier();
        var type = ParseType();

        // DEFAULT VALUE IS OPTIONAL
        // param location string = 'eastus'  <- has default
        // param storageAccountName string   <- no default
        ExpressionSyntax? value = null;
        if (Current.Kind == TokenKind.Assign)
        {
            Consume();
            value = ParseExpression();
        }
        if (Current.Kind == TokenKind.NewLine) Consume();
        return new ParameterDeclarationSyntax(name, type, value);
    }

    private VariableDeclarationSyntax ParseVariableDeclaration()
    {
        Consume();
        var name = ParseIdentifier();
        Eat(TokenKind.Assign);
        var value = ParseExpression();
        if (Current.Kind == TokenKind.NewLine) Consume();
        return new VariableDeclarationSyntax(name, value);
    }

    private ResourceDeclarationSyntax ParseResourceDeclaration()
    {
        Consume();
        var name = ParseIdentifier();
        var type = ParseExpression();
        if (Current.Kind == TokenKind.Existing) Consume(); // resource x '...' existing = {}
        Eat(TokenKind.Assign);
        if (Current.Kind == TokenKind.If)       // resource x = if (cond) { ... }
        {
            Consume();
            if (Current.Kind == TokenKind.LeftParen) SkipParentheses();
        }
        var body = ParseExpression();
        if (Current.Kind == TokenKind.NewLine) Consume();
        return new ResourceDeclarationSyntax(name, type, body);
    }

    private OutputDeclarationSyntax ParseOutputDeclaration()
    {
        Consume();
        var name = ParseIdentifier();
        var type = ParseType();
        Eat(TokenKind.Assign);
        var value = ParseExpression();
        if (Current.Kind == TokenKind.NewLine) Consume();
        return new OutputDeclarationSyntax(name, type, value);
    }

    private ExpressionSyntax ParseExpression()
    {
        var expr = ParsePrimary();

        // Ternary: expr ? thenBranch : elseBranch
        if (Current.Kind == TokenKind.Question)
        {
            Consume();
            var whenTrue  = ParseExpression();
            Eat(TokenKind.Colon);
            var whenFalse = ParseExpression();
            return new TernaryExpressionSyntax(expr, whenTrue, whenFalse);
        }

        return expr;
    }

    private ExpressionSyntax ParsePrimary()
    {
        if (Current.Kind == TokenKind.String)
            return new StringLiteralExpressionSyntax(Consume().Text);
        if (Current.Kind == TokenKind.Integer)
            return new IntegerLiteralExpressionSyntax(Consume().Text);
        if (Current.Kind == TokenKind.True)  { Consume(); return new BooleanLiteralExpressionSyntax(true); }
        if (Current.Kind == TokenKind.False) { Consume(); return new BooleanLiteralExpressionSyntax(false); }
        if (Current.Kind == TokenKind.Null)  { Consume(); return new NullLiteralExpressionSyntax(); }
        if (Current.Kind == TokenKind.LeftBracket) return ParseArrayExpression();
        if (Current.Kind == TokenKind.LeftBrace)   return ParseObjectExpression();

        // Parenthesised expression — unwrap it
        if (Current.Kind == TokenKind.LeftParen)
        {
            Consume();
            var inner = ParseExpression();
            if (Current.Kind == TokenKind.RightParen) Consume();
            return inner;
        }

        // Unary not: !expr
        if (Current.Kind == TokenKind.Not)
        {
            Consume();
            return ParsePrimary();
        }

        if (Current.Kind == TokenKind.Identifier)
        {
            var name = Consume().Text;
            ExpressionSyntax expr;
            if (Current.Kind == TokenKind.LeftParen)
            {
                SkipParentheses();
                expr = new FunctionCallExpressionSyntax(name);
            }
            else
            {
                expr = new IdentifierExpressionSyntax(name);
            }
            while (Current.Kind == TokenKind.Dot)
            {
                Consume();
                var member = ParseIdentifier();
                expr = new MemberAccessExpressionSyntax(expr, member);
            }
            // Handle index access: expr[index]
            while (Current.Kind == TokenKind.LeftBracket)
            {
                Consume();
                while (Current.Kind != TokenKind.RightBracket &&
                       Current.Kind != TokenKind.EndOfFile)
                    Consume();
                if (Current.Kind == TokenKind.RightBracket) Consume();
            }
            return expr;
        }

        Consume();
        return new IdentifierExpressionSyntax("<unknown>");
    }

    private ObjectExpressionSyntax ParseObjectExpression()
    {
        Eat(TokenKind.LeftBrace);
        var properties = new List<ObjectPropertySyntax>();
        while (Current.Kind != TokenKind.RightBrace && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.NewLine ||
                Current.Kind == TokenKind.Comment ||
                Current.Kind == TokenKind.BlockComment)
            { Consume(); continue; }

            // Spread operator: ...expr
            if (Current.Kind == TokenKind.Ellipsis)
            {
                Consume();
                ParseExpression();
                if (Current.Kind == TokenKind.Comma) Consume();
                continue;
            }

            // Property name: identifier or quoted string
            string name;
            if (Current.Kind == TokenKind.String)
                name = Consume().Text.Trim('\'');
            else if (Current.Kind == TokenKind.Identifier)
                name = Consume().Text;
            else
            { Consume(); continue; } // skip unrecognised token

            if (Current.Kind != TokenKind.Colon) continue;
            Consume(); // :

            var value = ParseExpression();
            properties.Add(new ObjectPropertySyntax(name, value));
            if (Current.Kind == TokenKind.Comma) Consume();
        }
        Eat(TokenKind.RightBrace);
        return new ObjectExpressionSyntax(properties);
    }

    private ArrayExpressionSyntax ParseArrayExpression()
    {
        Eat(TokenKind.LeftBracket);
        var items = new List<ExpressionSyntax>();

        // For-loop comprehension: [for item in collection: expr]
        // Skip the entire body and return an empty array node.
        if (Current.Kind == TokenKind.For)
        {
            int depth = 1;
            while (depth > 0 && Current.Kind != TokenKind.EndOfFile)
            {
                if (Current.Kind == TokenKind.LeftBracket)  depth++;
                if (Current.Kind == TokenKind.RightBracket) depth--;
                if (depth > 0) Consume();
            }
            Eat(TokenKind.RightBracket);
            return new ArrayExpressionSyntax(items);
        }

        while (Current.Kind != TokenKind.RightBracket && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.NewLine || Current.Kind == TokenKind.Comma)
            { Consume(); continue; }
            items.Add(ParseExpression());
        }
        Eat(TokenKind.RightBracket);
        return new ArrayExpressionSyntax(items);
    }

    private void SkipParentheses()
    {
        Eat(TokenKind.LeftParen);
        int depth = 1;
        while (depth > 0 && Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.LeftParen)  depth++;
            if (Current.Kind == TokenKind.RightParen) depth--;
            Consume();
        }
    }

    private string ParseIdentifier() => Eat(TokenKind.Identifier).Text;

    private string ParseType()
    {
        return Current.Kind switch
        {
            TokenKind.StringType => Consume().Text,
            TokenKind.IntType    => Consume().Text,
            TokenKind.BoolType   => Consume().Text,
            TokenKind.ObjectType => Consume().Text,
            TokenKind.ArrayType  => Consume().Text,
            TokenKind.Identifier => Consume().Text,
            _ => "unknown"
        };
    }

    private Token Eat(TokenKind expected)
    {
        if (Current.Kind == expected) return Consume();
        throw new ParserException($"Expected {expected} but found {Current.Kind} at {Current.Location}");
    }

    private Token Consume()
    {
        var current = Current;
        if (Current.Kind != TokenKind.EndOfFile) _position++;
        return current;
    }

    private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
}

public sealed class ParserException : Exception
{
    public ParserException(string message) : base(message) { }
}
