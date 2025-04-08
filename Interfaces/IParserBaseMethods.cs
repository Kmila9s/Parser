using Parser.Models;

namespace Parser.Interfaces;
public interface IParserBaseMethods {
    bool ExistsVariable(string variable);
    public Dictionary<string, HashSet<Variable>> GetScopes();
}