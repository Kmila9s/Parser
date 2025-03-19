using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;
namespace Parser.Repositories;
public class FunctionParser : IParseStructures {
    private TokenStream _streamer;
    private readonly ILogger<ArchitectureParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ArchitectureParser>();
    private Dictionary<string, HashSet<string>> _scopes = new();
    private string _name = string.Empty, _root = string.Empty;
    private DataStructuresParser _dataStructuresParser = default!;
    private FunctionAssignationParser _functionAssignationParser = default!;
    public FunctionParser(TokenStream stream, Dictionary<string, HashSet<string>> scopes, string RootName)
    {
        _scopes = scopes;
        _streamer = stream;
        _root = RootName;
    }
    public string GetName() => _name;
    public Dictionary<string, HashSet<string>> GetScopes() => _scopes;
    public bool ExistsVariable(string variable)
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    public void ParseDeclaration(){
        _functionAssignationParser = new(_streamer, _scopes, _name);
        _functionAssignationParser.Parse();
    }
    public void ParseBodyDeclaration(){
        throw new NotImplementedException();
    }
    public void Parse(){
        _logger.LogInformation("Parsing function");
        _streamer.Expect(KEYWORDS);
        _name = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(IDENTIFIERS);
        if(_scopes.ContainsKey(_root) && _scopes[_root].Contains(_name))
            throw new ParserException(DUPLICATE_DECLARATION);
        if(_streamer.Current?.Type == PARENTHESIS){
            // FUNCTION WITH PARAMETERS
            _streamer.Expect(PARENTHESIS);
            ParseDeclaration();
            _streamer.Expect(PARENTHESIS);
        }
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(KEYWORDS); // RETURN DATATYPE
        if(_streamer.Current?.Type == OPERATORS){
            _streamer.Expect(OPERATORS);
            _logger.LogInformation("Single Function Parsed");
            return;
        }
        // Function with body
        _streamer.Expect(KEYWORDS); // is
        ParseBodyDeclaration();
        _logger.LogInformation("Function Parsed");
    }
}