using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using Parser.Models;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;

namespace Parser.Repositories;
public class EntityParser : IParseStructures {
    private TokenStream _streamer;
    private readonly ILogger<EntityParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EntityParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private GenericParser _genericParser = default!;
    private PortsParser _portsParser = default!;
    public Entity Entity { get; private set; }= default!;
    private Architecture Architecture = default!;
    private string _name = string.Empty;
    public EntityParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes){
        _streamer = streamer;
        _scopes = scopes;
    }
    public void Parse(){
        _logger.LogInformation("Parsing Entity");
        ParseDeclaration();
        _logger.LogInformation("Entity Parsed");
    }
    public string GetName() => _name;
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    public Dictionary<string, HashSet<Variable>> GetScopes() => _scopes;
    public void ParseDeclaration(){
        _streamer.Expect(KEYWORDS);
        _name = _streamer.Current?.Value ?? string.Empty;
        Entity.Name = _name;
        Entity.Ports = new();
        if(_scopes.ContainsKey(_name))
            throw new ParserException(DUPLICATE_STRUCTURE);
        _scopes.Add(_name, new());
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS);
        if(_streamer.Current?.Value.ToLower() == "generic"){
            _genericParser = new(_streamer, _scopes, _name);
            _genericParser.Parse();
        }
        if(_streamer.Current?.Value.ToLower() == "port"){
            _portsParser = new(_streamer, _scopes, _name);
            _portsParser.Parse();
            var ports = _portsParser.GetScopes();
            Entity.Ports = new(ports[_name].ToList());
        }
        _streamer.Expect(KEYWORDS);
        string ClosureName = _streamer.Current?.Value ?? string.Empty;
        if(String.IsNullOrEmpty(ClosureName) || String.IsNullOrEmpty(_name))
            throw new Exception($"Expected Entity name");
        _streamer.Expect(IDENTIFIERS);
        if(!ClosureName.Equals(_name))
            throw new Exception($"Expecting same identifier for entity closure, found: {_streamer.Current?.Value}, Expected: {_name} ");
        _streamer.Expect(OPERATORS);
    }
    public void ParseBodyDeclaration(){

    }
}