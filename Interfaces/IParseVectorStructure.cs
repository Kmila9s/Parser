namespace Parser.Interfaces;

/// <summary>
/// Extends the IParser interface for components that parse vector structures like std_logic_vector.
/// </summary>
public interface IParseVectorStructure : IParser, IParserBaseMethods{
    /// <summary>
    /// Parses the body of a vector definition (e.g., the part within parentheses).
    /// </summary>
    abstract void ParseBody();
}