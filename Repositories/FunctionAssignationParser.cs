using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;
namespace Parser.Repositories;
public class FunctionAssignationParser : IParseDeclarations {
    private TokenStream _streamer;
    private readonly ILogger<FunctionAssignationParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<FunctionAssignationParser>();
    private string _name = string.Empty, _entityName = string.Empty;
    private Dictionary<string, HashSet<string>> _scopes = new();
    private VectorParser _vectorParser = default!;
    private RangeParser _rangeParser = default!;
    public FunctionAssignationParser(TokenStream stream, Dictionary<string, HashSet<string>> scopes, string Name)
    {
        _streamer = stream;
        _scopes = scopes;
        _name = Name;
    }
    public void Parse()
    {
        _logger.LogInformation("Parsing Function Assignatinos");
        ParseDeclarations();
        _logger.LogInformation("Function assignations parsed");
    }
    public Dictionary<string, HashSet<string>> GetScopes() => _scopes;
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    public void ParseDeclarations(){
        while(_streamer.Current?.Type == IDENTIFIERS){
            //_streamer.Expect(IDENTIFIERS);
            ParseWithComas();
            _streamer.Expect(OPERATORS); // :
            _streamer.Expect(KEYWORDS); // DATATYPE
            if(_streamer.Current.Type != OPERATORS && _streamer.Current.Value != ";")
                break;
            else
                _streamer.Expect(OPERATORS);
        }
    }
    private void ParseWithComas(){
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!_scopes.ContainsKey(_name))
                throw new ParserException(MISSING_STRUCTURE);
            if(ExistsVariable(_streamer.Current.Value))
                throw new ParserException(DUPLICATE_DECLARATION);
            _scopes[_name].Add(_streamer.Current.Value);
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current.Type == SEPARATORS && _streamer.Current.Value.Equals(","))
                _streamer.MoveNext();
            else
                break;   
        }
    }
}