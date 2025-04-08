using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using Parser.Models;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
namespace Parser.Repositories;
public class GenericParser : IParseGeneric {
    private TokenStream _streamer;
    private readonly ILogger<GenericParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<GenericParser>();
    private Dictionary<string, HashSet<Variable>> _scopes;
    private readonly VectorParser _vectorParser;
    private readonly RangeParser _rangeParser;
    private string _name = string.Empty;
    public GenericParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes, string name)
    {
        _streamer = streamer;
        _scopes = scopes;
        _name = name;
        _vectorParser = _rangeParser = new(_streamer, _scopes, _name);
    }
    public void Parse(){
        _logger.LogInformation("Parsing Generic");
        ParseStructure();
        _logger.LogInformation("Generic Parsed");
    }
    public void ParseStructure(){
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(PARENTHESIS);
        while(_streamer.Current?.Type == IDENTIFIERS){
            ParseAssignations();
            if(_streamer.Current?.Type == OPERATORS && (_streamer.Current?.Value.Equals(";") ?? false))
                _streamer.MoveNext();
        }
        _streamer.Expect(PARENTHESIS);
        _streamer.Expect(OPERATORS);
    }
    public void ParseAssignations(){
        while(_streamer.Current?.Type == IDENTIFIERS){
            ParseAssignationsWithComas();
            _streamer.Expect(OPERATORS);
            string val = _streamer.Current?.Value.ToUpper() ?? string.Empty;

            _vectorParser.Parse(); // If is not vector will do nothing

            _rangeParser.Parse(); // Same as vector

            if(!_vectorParser.Parsed && !_rangeParser.Parsed){
                _streamer.Expect(KEYWORDS);
                if(val.ToLower().Equals("time")){
                    _streamer.Expect(OPERATORS);
                    _streamer.Expect(LITERALS);
                    _streamer.Expect(TIME_VARIABLES);
                }
                if(_streamer.Current?.Type == OPERATORS && _streamer.Current.Value != ";"){
                    // Asignation
                    _streamer.Expect(OPERATORS);
                    _streamer.Expect(LITERALS);
                }
                if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower().Equals("after")){
                    _streamer.Expect(KEYWORDS); // after
                    _streamer.Expect(LITERALS);
                    _streamer.Expect(TIME_VARIABLES);
                }
            }
        }
    }
    public void ParseAssignationsWithComas(){
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!_scopes.ContainsKey(_name))
                _scopes.Add(_name, new());
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
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    public Dictionary<string, HashSet<Variable>> GetScopes() => _scopes;
}