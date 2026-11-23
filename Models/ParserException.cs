namespace Parser.Models;
using static Parser.Models.Constants;
using static Parser.Models.Constants.ParseExceptions;

/// <summary>
/// Represents errors that occur during VHDL parsing.
/// Provides detailed error messages with line:col information and context.
/// </summary>
public class ParserException : Exception, ISourcePosition
{
    /// <summary>Gets the line number where the error occurred (1-based).</summary>
    public int Line { get; }

    /// <summary>Gets the column number where the error occurred (1-based).</summary>
    public int Column { get; }

    /// <summary>Gets the error code if one was specified.</summary>
    public ParseExceptions? ErrorCode { get; }

    /// <summary>Gets additional context about the error (e.g., token value, variable name).</summary>
    public string? Context { get; }

    /// <summary>
    /// The source file the position is in, when it is not the parsed text itself (an
    /// error of an elaborated design located back in the child's file, G82); null otherwise.
    /// </summary>
    public string? File { get; private init; }

    private ParserException(string message, int line, int col, string? context, ParseExceptions? code, string? file, Exception inner)
        : base(message, inner)
    {
        Line = line;
        Column = col;
        Context = context;
        ErrorCode = code;
        File = file;
    }

    /// <summary>
    /// This error located at <paramref name="line"/> of <paramref name="file"/> (the
    /// origin of a line of an elaborated text): same message, the position rewritten.
    /// </summary>
    public ParserException Relocated(string file, int line) =>
        new(System.Text.RegularExpressions.Regex.Replace(Message, $@"\bat Line {Line}\b", $"at Line {line} in {System.IO.Path.GetFileName(file)}"),
            line, Column, Context, ErrorCode, file, this);

    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class.
    /// </summary>
    public ParserException() : base("Unknown parsing error") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ParserException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance with message and location.
    /// </summary>
    public ParserException(string message, int line, int col)
        : base(FormatMessage(message, line, col, null))
    {
        Line = line;
        Column = col;
    }

    /// <summary>
    /// Initializes a new instance with message, location, and context.
    /// </summary>
    public ParserException(string message, int line, int col, string? context)
        : base(FormatMessage(message, line, col, context))
    {
        Line = line;
        Column = col;
        Context = context;
    }

    /// <summary>
    /// Initializes a new instance with a token for location information.
    /// </summary>
    public ParserException(string message, Token? token)
        : base(FormatMessage(message, token))
    {
        if (token != null)
        {
            Line = token.Line + 1;
            Column = token.Col + 1;
            Context = token.Value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    public ParserException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class with a specific error code.
    /// </summary>
    /// <param name="ExceptionCode">The error code from <see cref="ParseExceptions"/>.</param>
    public ParserException(ParseExceptions ExceptionCode) : base(GetErrorDescription(ExceptionCode))
    {
        ErrorCode = ExceptionCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class with a specific error code and line/column numbers.
    /// </summary>
    public ParserException(ParseExceptions ExceptionCode, int line, int col)
        : base(FormatMessage(GetErrorDescription(ExceptionCode), line, col, null))
    {
        ErrorCode = ExceptionCode;
        Line = line;
        Column = col;
    }

    /// <summary>
    /// Initializes a new instance with error code, location, and context.
    /// </summary>
    public ParserException(ParseExceptions ExceptionCode, int line, int col, string? context)
        : base(FormatMessage(GetErrorDescription(ExceptionCode), line, col, context))
    {
        ErrorCode = ExceptionCode;
        Line = line;
        Column = col;
        Context = context;
    }

    /// <summary>
    /// Initializes a new instance with error code and token.
    /// </summary>
    public ParserException(ParseExceptions ExceptionCode, Token? token)
        : base(FormatMessage(GetErrorDescription(ExceptionCode), token))
    {
        ErrorCode = ExceptionCode;
        if (token != null)
        {
            Line = token.Line + 1;
            Column = token.Col + 1;
            Context = token.Value;
        }
    }

    /// <summary>
    /// Formats the error message with location and context.
    /// </summary>
    private static string FormatMessage(string message, int line, int col, string? context)
    {
        string location = line <= 0 ? "" : col > 0 ? $" at Line {line}:{col}" : $" at Line {line}";
        string contextStr = !string.IsNullOrEmpty(context) ? $" (near '{context}')" : "";
        return $"{message}{location}{contextStr}";
    }

    /// <summary>
    /// Formats the error message with token information.
    /// </summary>
    private static string FormatMessage(string message, Token? token)
    {
        if (token == null)
            return $"{message} at end of input";

        return $"{message} at Line {token.Line + 1}:{token.Col + 1} (found '{token.Value}' of type {token.Type})";
    }

    /// <summary>
    /// Gets a human-readable description for the error code.
    /// </summary>
    private static string GetErrorDescription(ParseExceptions code)
    {
        return code switch
        {
            INITIALIZATION => "Variable/signal must be initialized before use",
            DUPLICATE_DECLARATION => "Duplicate declaration: identifier already defined in this scope",
            DUPLICATE_STRUCTURE => "Duplicate structure: entity/architecture already defined",
            MISSING_STRUCTURE => "Missing structure: referenced entity/architecture not found",
            MISSING_DECLARATION => "Undeclared identifier: variable/signal not found in scope",
            SYNTAX_ERROR => "Syntax error: unexpected token or malformed statement",
            UNKNOWN => "Unknown error occurred during parsing",
            _ => $"Parser error (code: {code})"
        };
    }

    /// <summary>
    /// Creates an exception for undeclared variable errors.
    /// </summary>
    public static ParserException UndeclaredVariable(string varName, Token? token)
    {
        return new ParserException($"Undeclared variable '{varName}'", token);
    }

    /// <summary>
    /// Creates an exception for type mismatch errors.
    /// </summary>
    public static ParserException TypeMismatch(string expected, string actual, Token? token)
    {
        return new ParserException($"Type mismatch: expected '{expected}', got '{actual}'", token);
    }
}