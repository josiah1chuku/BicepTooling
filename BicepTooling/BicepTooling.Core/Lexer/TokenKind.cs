namespace BicepTooling.Lexer;

public enum TokenKind
{
    Integer, String, MultilineString, True, False, Null, Identifier,
    Param, Var, Resource, Module, Output, Existing, Import, Metadata, Type,
    If, For, In,
    StringType, IntType, BoolType, ObjectType, ArrayType,
    Equal, NotEqual, LessThan, GreaterThan, LessOrEqual, GreaterOrEqual,
    And, Or, Not,
    Assign, Question, Colon, DoubleColon, Arrow, Pipe, Ampersand,
    Plus, Minus, Star, Slash, Percent,
    LeftParen, RightParen, LeftBrace, RightBrace, LeftBracket, RightBracket,
    Comma, Dot, Ellipsis, At, Hash,
    NewLine, Comment, BlockComment, EndOfFile, Unknown
}
