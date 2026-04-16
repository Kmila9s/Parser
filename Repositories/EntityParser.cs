using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using Parser.Models;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;

namespace Parser.Repositories;

/// <summary>
/// A parser for VHDL 'entity' blocks. It handles the entity declaration, including its generics and ports.
/// </summary>
public class EntityParser : IParseStructures {
    private TokenStream _streamer;
    private readonly ILogger<EntityParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<EntityParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private GenericParser _genericParser = default!;
    private PortsParser _portsParser = default!;
    private string _name = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityParser"/> class.
    /// </summary>
    /// <param name="streamer">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    public EntityParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes){
        _streamer = streamer;
        _scopes = scopes;
    }
    /// <summary>
    /// Starts the parsing of the entity block.
    /// </summary>
    public void Parse()
    {
        _logger.LogInformation("Parsing Entity");
        ParseDeclaration();
        _logger.LogInformation("Entity Parsed");
    }
    
    /// <summary>
    /// Gets the name of the parsed entity.
    /// </summary>
    /// <returns>The entity name.</returns>
    public string GetName() => _name;
    /// <summary>
    /// Checks if a variable exists within the current entity's scope.
    /// </summary>
    /// <param name="variable">The name of the variable to check.</param>
    /// <returns>True if the variable exists, otherwise false.</returns>
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    /// <summary>
    /// Gets the dictionary of scopes.
    /// </summary>
    /// <returns>The dictionary of scopes.</returns>
    public Dictionary<string, HashSet<Variable>> GetScopes() => _scopes;
    /// <summary>
    /// Parses the entity declaration, including entity name, generic block, port block, and end closure.
    /// </summary>
    public void ParseDeclaration(){
        _streamer.Expect(KEYWORDS);
        _name = _streamer.Current?.Value ?? string.Empty;
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
    /// <summary>
    /// This method is not implemented for EntityParser as entities do not have a body declaration part.
    /// </summary>
    public void ParseBodyDeclaration(){

    }
}