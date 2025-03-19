namespace Parser.Interfaces;
public interface IParseGeneric : IParser, IParserBaseMethods {
    void ParseStructure();
    void ParseAssignations();
    void ParseAssignationsWithComas();
}