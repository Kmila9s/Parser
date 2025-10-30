using Parser.Repositories;

namespace Parser.Interfaces;
/// <summary>
/// Defines the contract for parsers that handle structured VHDL blocks like entities, architectures, or packages.
/// These structures typically have a declaration part and a body.
/// </summary>
public interface IParseStructures : IParser, IParserBaseMethods {
    /// <summary>
    /// Gets the name of the structure being parsed.
    /// </summary>
    /// <returns>The name of the structure.</returns>
    public string GetName();
    /// <summary>
    /// Parses the declaration part of the structure.
    /// </summary>
    void ParseDeclaration();
    /// <summary>
    /// Parses the body or implementation part of the structure.
    /// </summary>
    void ParseBodyDeclaration();
}