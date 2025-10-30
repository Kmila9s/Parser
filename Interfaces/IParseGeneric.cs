namespace Parser.Interfaces;

/// <summary>
/// Extends the IParser interface for components that parse VHDL generic blocks.
/// </summary>
public interface IParseGeneric : IParser, IParserBaseMethods {
    /// <summary>
    /// Parses the entire generic structure.
    /// </summary>
    void ParseStructure();
    /// <summary>
    /// Parses individual generic constant assignations within the generic block.
    /// </summary>
    void ParseAssignations();
    /// <summary>
    /// Parses comma-separated identifiers in a generic declaration.
    /// </summary>
    void ParseAssignationsWithComas();
}