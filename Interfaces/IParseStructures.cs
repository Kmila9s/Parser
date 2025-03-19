using Parser.Repositories;

namespace Parser.Interfaces;
public interface IParseStructures : IParser, IParserBaseMethods {
    public string GetName();
    void ParseDeclaration();
    void ParseBodyDeclaration();
}