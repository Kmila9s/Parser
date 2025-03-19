using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using System.Numerics;
using Parser.Models;

namespace Parser.Repositories;
public class PortsParser : IParseDeclarations {
    private TokenStream _streamer;
    private readonly ILogger<PortsParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PortsParser>();
    private Dictionary<string, HashSet<string>> _scopes = new();
    private VectorParser _vectorParser = default!;
    private RangeParser _rangeParser = default!;
    private string _name = string.Empty;
    public PortsParser(TokenStream stream, Dictionary<string, HashSet<string>> scopes, string Name)
    {
        _streamer = stream;
        _name = Name;
        _scopes = scopes;
    }
    public void Parse(){
        _logger.LogInformation("Parsing Port Declaration");
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(PARENTHESIS);
        while(_streamer.Current?.Type == IDENTIFIERS){
            ParseDeclarations();
            if(_streamer.Current?.Type == OPERATORS && (_streamer.Current?.Value.Equals(";") ?? false))
                _streamer.MoveNext();
        }
        _streamer.Expect(PARENTHESIS);
        _streamer.Expect(OPERATORS);
        _logger.LogInformation("Port Declaration Parsed");
    }
    public void ParseDeclarations(){
        
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!_scopes.ContainsKey(_name))
                throw new ParserException(MISSING_STRUCTURE);
            if(_scopes[_name].Contains(_streamer.Current.Value))
                throw new ParserException(DUPLICATE_DECLARATION);
            _scopes[_name].Add(_streamer.Current.Value);
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current.Type == SEPARATORS && _streamer.Current.Value.Equals(",")){
                _streamer.MoveNext();
            }else{
                break;
            }
        }
        _streamer.Expect(OPERATORS);
        _streamer.Expect(KEYWORDS);

        _vectorParser = new(_streamer, _scopes, _name);
        _vectorParser.Parse();

        _rangeParser = new(_streamer, _scopes, _name);
        _rangeParser.Parse();

        if(!_vectorParser.Parsed && !_rangeParser.Parsed)
            _streamer.Expect(KEYWORDS);
    }

    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    public Dictionary<string, HashSet<string>> GetScopes() => _scopes;
}