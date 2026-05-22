namespace BicepTooling.Tests;

using BicepTooling.Lexer;

public class LexerTests
{
    [Fact]
    public void Tokenize_Keywords_CorrectTokens()
    {
        var lexer = new Lexer("param var resource output");
        var tokens = lexer.Tokenize();
        
        Assert.Equal(TokenKind.Param, tokens[0].Kind);
        Assert.Equal(TokenKind.Var, tokens[1].Kind);
        Assert.Equal(TokenKind.Resource, tokens[2].Kind);
        Assert.Equal(TokenKind.Output, tokens[3].Kind);
        Assert.Equal(TokenKind.EndOfFile, tokens[4].Kind);
    }

    [Fact]
    public void Tokenize_String_CorrectToken()
    {
        var lexer = new Lexer("'eastus'");
        var tokens = lexer.Tokenize();
        
        Assert.Equal(TokenKind.String, tokens[0].Kind);
        Assert.Equal("'eastus'", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_Number_CorrectToken()
    {
        var lexer = new Lexer("2023");
        var tokens = lexer.Tokenize();
        
        Assert.Equal(TokenKind.Integer, tokens[0].Kind);
        Assert.Equal("2023", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_Identifier_CorrectToken()
    {
        var lexer = new Lexer("storageAccountName");
        var tokens = lexer.Tokenize();
        
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("storageAccountName", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_Operators_CorrectTokens()
    {
        var lexer = new Lexer("= == != < > <= >=");
        var tokens = lexer.Tokenize();
        
        Assert.Equal(TokenKind.Assign, tokens[0].Kind);
        Assert.Equal(TokenKind.Equal, tokens[1].Kind);
        Assert.Equal(TokenKind.NotEqual, tokens[2].Kind);
        Assert.Equal(TokenKind.LessThan, tokens[3].Kind);
        Assert.Equal(TokenKind.GreaterThan, tokens[4].Kind);
        Assert.Equal(TokenKind.LessOrEqual, tokens[5].Kind);
        Assert.Equal(TokenKind.GreaterOrEqual, tokens[6].Kind);
    }

    [Fact]
    public void Tokenize_Braces_CorrectTokens()
    {
        var lexer = new Lexer("{ } [ ] ( )");
        var tokens = lexer.Tokenize();
        
        Assert.Equal(TokenKind.LeftBrace, tokens[0].Kind);
        Assert.Equal(TokenKind.RightBrace, tokens[1].Kind);
        Assert.Equal(TokenKind.LeftBracket, tokens[2].Kind);
        Assert.Equal(TokenKind.RightBracket, tokens[3].Kind);
        Assert.Equal(TokenKind.LeftParen, tokens[4].Kind);
        Assert.Equal(TokenKind.RightParen, tokens[5].Kind);
    }

    [Fact]
    public void Tokenize_SimpleDeclaration_CorrectTokens()
    {
        var source = "param location string = 'eastus'";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        
        Assert.Equal(TokenKind.Param, tokens[0].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
        Assert.Equal("location", tokens[1].Text);
        Assert.Equal(TokenKind.StringType, tokens[2].Kind);
        Assert.Equal(TokenKind.Assign, tokens[3].Kind);
        Assert.Equal(TokenKind.String, tokens[4].Kind);
    }

    [Fact]
    public void Tokenize_PreservesLineAndColumnInfo()
    {
        var source = "param\nlocation";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        
        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(2, tokens[1].Line);
    }
}
