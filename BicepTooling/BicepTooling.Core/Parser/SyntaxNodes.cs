namespace BicepTooling.Parser;

public abstract class SyntaxNode
{
}

public sealed class CompilationUnitSyntax : SyntaxNode
{
    public IReadOnlyList<StatementSyntax> Statements { get; }

    public CompilationUnitSyntax(IReadOnlyList<StatementSyntax> statements)
    {
        Statements = statements;
    }
}

public abstract class StatementSyntax : SyntaxNode
{
}

public abstract class ExpressionSyntax : SyntaxNode
{
}

public sealed class ParameterDeclarationSyntax : StatementSyntax
{
    public string Name { get; }
    public string Type { get; }
    public ExpressionSyntax? Value { get; }

    public ParameterDeclarationSyntax(string name, string type, ExpressionSyntax? value)
    {
        Name = name;
        Type = type;
        Value = value;
    }
}

public sealed class VariableDeclarationSyntax : StatementSyntax
{
    public string Name { get; }
    public ExpressionSyntax Value { get; }

    public VariableDeclarationSyntax(string name, ExpressionSyntax value)
    {
        Name = name;
        Value = value;
    }
}

public sealed class ResourceDeclarationSyntax : StatementSyntax
{
    public string Name { get; }
    public ExpressionSyntax Type { get; }
    public ExpressionSyntax Body { get; }

    public ResourceDeclarationSyntax(string name, ExpressionSyntax type, ExpressionSyntax body)
    {
        Name = name;
        Type = type;
        Body = body;
    }
}

public sealed class OutputDeclarationSyntax : StatementSyntax
{
    public string Name { get; }
    public string Type { get; }
    public ExpressionSyntax Value { get; }

    public OutputDeclarationSyntax(string name, string type, ExpressionSyntax value)
    {
        Name = name;
        Type = type;
        Value = value;
    }
}

public sealed class TernaryExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Condition { get; }
    public ExpressionSyntax WhenTrue  { get; }
    public ExpressionSyntax WhenFalse { get; }
    public TernaryExpressionSyntax(ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse)
    { Condition = condition; WhenTrue = whenTrue; WhenFalse = whenFalse; }
}

public sealed class FunctionCallExpressionSyntax : ExpressionSyntax
{
    public string Name { get; }
    public FunctionCallExpressionSyntax(string name) { Name = name; }
}

public sealed class IdentifierExpressionSyntax : ExpressionSyntax
{
    public string Name { get; }

    public IdentifierExpressionSyntax(string name)
    {
        Name = name;
    }
}

public sealed class MemberAccessExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Target { get; }
    public string Member { get; }

    public MemberAccessExpressionSyntax(ExpressionSyntax target, string member)
    {
        Target = target;
        Member = member;
    }
}

public sealed class StringLiteralExpressionSyntax : ExpressionSyntax
{
    public string Value { get; }

    public StringLiteralExpressionSyntax(string value)
    {
        Value = value;
    }
}

public sealed class IntegerLiteralExpressionSyntax : ExpressionSyntax
{
    public string Value { get; }

    public IntegerLiteralExpressionSyntax(string value)
    {
        Value = value;
    }
}

public sealed class BooleanLiteralExpressionSyntax : ExpressionSyntax
{
    public bool Value { get; }

    public BooleanLiteralExpressionSyntax(bool value)
    {
        Value = value;
    }
}

public sealed class NullLiteralExpressionSyntax : ExpressionSyntax
{
    public NullLiteralExpressionSyntax()
    {
    }
}

public sealed class ArrayExpressionSyntax : ExpressionSyntax
{
    public IReadOnlyList<ExpressionSyntax> Items { get; }

    public ArrayExpressionSyntax(IReadOnlyList<ExpressionSyntax> items)
    {
        Items = items;
    }
}

public sealed class ObjectExpressionSyntax : ExpressionSyntax
{
    public IReadOnlyList<ObjectPropertySyntax> Properties { get; }

    public ObjectExpressionSyntax(IReadOnlyList<ObjectPropertySyntax> properties)
    {
        Properties = properties;
    }
}

public sealed class ObjectPropertySyntax : SyntaxNode
{
    public string Name { get; }
    public ExpressionSyntax Value { get; }

    public ObjectPropertySyntax(string name, ExpressionSyntax value)
    {
        Name = name;
        Value = value;
    }
}
