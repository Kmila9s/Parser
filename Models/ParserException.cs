namespace Parser.Models;
using static Parser.Models.Constants;
using static Parser.Models.Constants.ParseExceptions;
public class ParserException : Exception{
    public ParserException() : base (){}
    public ParserException(string message) : base(message) {}
    public ParserException(string message, Exception innerException) : base(message, innerException) {}
    public ParserException(ParseExceptions ExceptionCode) : base(GetError(ExceptionCode)){}
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