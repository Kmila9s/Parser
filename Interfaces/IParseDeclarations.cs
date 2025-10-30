namespace Parser.Interfaces;

/// <summary>
/// Extends the IParser interface for components that parse declaration sections.
/// </summary>
public interface IParseDeclarations : IParser, IParserBaseMethods{
    /// <summary>
    /// Parses the declaration part of a VHDL block.
    /// </summary>
    void ParseDeclarations();
}