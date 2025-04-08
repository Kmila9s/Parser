using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using Parser.Models;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;

namespace Parser.Repositories;
public class ArchitectureAssignationParser : IParseDeclarations{
    private TokenStream _streamer;
    private readonly ILogger<ArchitectureAssignationParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ArchitectureAssignationParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private VectorParser _vectorParser = default!;
    private RangeParser _rangeParser = default!;
    private string _name = string.Empty, _entityName = string.Empty;
    public ArchitectureAssignationParser(TokenStream stream, Dictionary<string, HashSet<Variable>> scopes, string Name, string EntityName = "")
    {
        _streamer = stream;
        _name = Name;
        _scopes = scopes;
        _entityName = EntityName;
    }
    public virtual void Parse(){
        _logger.LogInformation("Parsing Architecture Assignatinos");
        ParseDeclarations();
        _logger.LogInformation("Architecture assignations parsed");
    }
    public Dictionary<string, HashSet<Variable>> GetScopes() => _scopes;
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    public void ParseDeclarations(){
        while(_streamer.Current?.Type == KEYWORDS && 
            (
                _streamer.Current?.Value == "signal" || _streamer.Current?.Value == "constant" ||
                _streamer.Current?.Value == "attribute" || _streamer.Current?.Value == "group" ||
                _streamer.Current?.Value == "disconnect"
            )
        ){
            _streamer.Expect(KEYWORDS); 
            ParseWithComas();
            _streamer.Expect(OPERATORS);
            string val = _streamer.Current?.Value.ToUpper() ?? string.Empty;

            _vectorParser = new(_streamer, _scopes, String.IsNullOrEmpty(_entityName) ? _name : _entityName );
            _vectorParser.Parse();

            _rangeParser = new(_streamer, _scopes, String.IsNullOrEmpty(_entityName) ? _name : _entityName );
            _rangeParser.Parse();

            if(!_vectorParser.Parsed && !_rangeParser.Parsed)
                _streamer.Expect(KEYWORDS);

            if(val.ToLower().Equals("integer")){
                _rangeParser = new(_streamer, _scopes, String.IsNullOrEmpty(_entityName) ? _name : _entityName );
                _rangeParser.Parse();
            }
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
            _streamer.Expect(OPERATORS); // ;
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