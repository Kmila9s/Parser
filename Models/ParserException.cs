namespace Parser.Models;
using static Parser.Models.Constants;
using static Parser.Models.Constants.ParseExceptions;
public class ParserException : Exception{
    public ParserException() : base (){}
    public ParserException(string message) : base(message) {}
    public ParserException(string message, Exception innerException) : base(message, innerException) {}
    public ParserException(ParseExceptions ExceptionCode) : base(GetError(ExceptionCode)){}
    private static string GetError(ParseExceptions code){
        switch(code){
            case INITIALIZATION : 
                return "The instance have to be initialized before";
            case DUPLICATE_DECLARATION:
                return "Instance already defined";
            case DUPLICATE_STRUCTURE:
                return "Structure already defined";
            case MISSING_DECLARATION:
                return "Instance not in this scope";
            case SYNTAX_ERROR:
                return "Instance error";
            default:
                return "Error code not found";
        }
    }
}