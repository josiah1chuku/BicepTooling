namespace BicepTooling.Tests;

using BicepTooling.Lexer;
using BicepTooling.Parser;

public class ParserTests
{
    [Fact]
    public void ParseParameterDeclaration_CreatesParameterSyntax()
    {
        var source = "param location string = 'eastus'";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        var compilation = parser.ParseCompilationUnit();

        Assert.Single(compilation.Statements);
        var parameter = Assert.IsType<ParameterDeclarationSyntax>(compilation.Statements[0]);
        Assert.Equal("location", parameter.Name);
        Assert.Equal("string", parameter.Type);
        Assert.IsType<StringLiteralExpressionSyntax>(parameter.Value);
        Assert.Equal("'eastus'", ((StringLiteralExpressionSyntax)parameter.Value).Value);
    }

    [Fact]
    public void ParseResourceDeclaration_CreatesResourceSyntax()
    {
        var source = "resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = { name: 'test' }";
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());

        var compilation = parser.ParseCompilationUnit();

        var resource = Assert.IsType<ResourceDeclarationSyntax>(compilation.Statements[0]);
        Assert.Equal("storageAccount", resource.Name);
        var typeExpr = Assert.IsType<StringLiteralExpressionSyntax>(resource.Type);
        Assert.Equal("'Microsoft.Storage/storageAccounts@2023-01-01'", typeExpr.Value);
        var body = Assert.IsType<ObjectExpressionSyntax>(resource.Body);
        Assert.Single(body.Properties);
        Assert.Equal("name", body.Properties[0].Name);
    }

    [Fact]
    public void ParseOutputDeclaration_CreatesMemberAccessExpression()
    {
        var source = "output storageId string = storageAccount.id";
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());

        var compilation = parser.ParseCompilationUnit();
        var output = Assert.IsType<OutputDeclarationSyntax>(compilation.Statements[0]);
        Assert.Equal("storageId", output.Name);
        Assert.Equal("string", output.Type);

        var memberAccess = Assert.IsType<MemberAccessExpressionSyntax>(output.Value);
        Assert.Equal("storageAccount", ((IdentifierExpressionSyntax)memberAccess.Target).Name);
        Assert.Equal("id", memberAccess.Member);
    }

    [Fact]
    public void ParseBooleanAndNullLiterals_CreatesCorrectNodes()
    {
        var source = "var isEnabled = true\nvar missingValue = null";
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());

        var compilation = parser.ParseCompilationUnit();
        var first = Assert.IsType<VariableDeclarationSyntax>(compilation.Statements[0]);
        Assert.IsType<BooleanLiteralExpressionSyntax>(first.Value);
        Assert.True(((BooleanLiteralExpressionSyntax)first.Value).Value);

        var second = Assert.IsType<VariableDeclarationSyntax>(compilation.Statements[1]);
        Assert.IsType<NullLiteralExpressionSyntax>(second.Value);
    }

    [Fact]
    public void ParseArrayExpression_CreatesArrayNode()
    {
        var source = "var values = [1, 2, 'three']";
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());

        var compilation = parser.ParseCompilationUnit();
        var variable = Assert.IsType<VariableDeclarationSyntax>(compilation.Statements[0]);
        var array = Assert.IsType<ArrayExpressionSyntax>(variable.Value);
        Assert.Equal(3, array.Items.Count);
        Assert.IsType<IntegerLiteralExpressionSyntax>(array.Items[0]);
        Assert.IsType<IntegerLiteralExpressionSyntax>(array.Items[1]);
        Assert.IsType<StringLiteralExpressionSyntax>(array.Items[2]);
    }

    [Fact]
    public void ParseSampleFile_CreatesExpectedStatements()
    {
        var source = "param location string = 'eastus'\nvar storageSku = 'Standard_LRS'\nresource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = { name: storageAccountName location: location sku: { name: storageSku } kind: 'StorageV2' }\noutput storageId string = storageAccount.id";
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());

        var compilation = parser.ParseCompilationUnit();
        Assert.Equal(4, compilation.Statements.Count);
        Assert.IsType<ParameterDeclarationSyntax>(compilation.Statements[0]);
        Assert.IsType<VariableDeclarationSyntax>(compilation.Statements[1]);
        Assert.IsType<ResourceDeclarationSyntax>(compilation.Statements[2]);
        Assert.IsType<OutputDeclarationSyntax>(compilation.Statements[3]);
    }
}


