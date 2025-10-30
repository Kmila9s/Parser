namespace Parser.Models;
using static Parser.Models.Constants;
using static Parser.Models.Constants.ParseExceptions;

/// <summary>
/// Represents errors that occur during VHDL parsing.
/// </summary>
public class ParserException : Exception{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class.
    /// </summary>
    public ParserException() : base (){}
    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ParserException(string message) : base(message) {}
    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public ParserException(string message, Exception innerException) : base(message, innerException) {}
    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class with a specific error code.
    /// </summary>
    /// <param name="ExceptionCode">The error code from <see cref="ParseExceptions"/>.</param>
    public ParserException(ParseExceptions ExceptionCode) : base(GetError(ExceptionCode)){}
    /// <summary>
    /// Initializes a new instance of the <see cref="ParserException"/> class with a specific error code and line/column numbers.
    /// </summary>
    /// <param name="ExceptionCode">The error code from <see cref="ParseExceptions"/>.</param>
    /// <param name="line">The line number where the error occurred.</param>
    /// <param name="col">The column number where the error occurred.</param>
    public ParserException(ParseExceptions ExceptionCode, int line, int col) : base(GetError(ExceptionCode, line, col)){}
    private static string GetError(ParseExceptions code, int line = -1, int col = -1){
        string error = "";
        switch (code) {
            case INITIALIZATION:
                error = $"The instance have to be initialized before" + (line != -1 ? $" {line} - {col}" : string.Empty);
                return error;
            case DUPLICATE_DECLARATION:
                error = "Instance already defined" + (line != -1 ? $" {line} - {col}" : string.Empty);
                return error;
            case DUPLICATE_STRUCTURE:
                error = "Structure already defined" + (line != -1 ? $" {line} - {col}" : string.Empty);
                return error;
            case MISSING_DECLARATION:
                error = "Instance not in this scope" + (line != -1 ? $" {line} - {col}" : string.Empty);
                return error;
            case SYNTAX_ERROR:
                error = "Instance error" + (line != -1 ? $" {line} - {col}" : string.Empty);
                return error;
            default:
                error = "Error code not found" + (line != -1 ? $" {line} - {col}" : string.Empty);
                return error;
        }
    }
}