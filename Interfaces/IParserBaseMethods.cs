namespace Parser.Interfaces;
public interface IParserBaseMethods {
    bool ExistsVariable(string variable);
    public Dictionary<string, HashSet<string>> GetScopes();
}