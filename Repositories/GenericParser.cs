using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using Parser.Models;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
namespace Parser.Repositories;

/// <summary>
/// A parser for VHDL 'generic' blocks. It handles the declaration of generic constants within an entity.
/// </summary>
public class GenericParser : IParseGeneric {
    private TokenStream _streamer;
    private readonly ILogger<GenericParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<GenericParser>();
    private Dictionary<string, HashSet<Variable>> _scopes;
    private readonly VectorParser _vectorParser;
    private readonly RangeParser _rangeParser;
    private string _name = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericParser"/> class.
    /// </summary>
    /// <param name="streamer">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    /// <param name="name">The name of the scope (e.g., entity name) where the generic belongs.</param>
    public GenericParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes, string name)
    {
        _streamer = streamer;
        _scopes = scopes;
        _name = name;
        _vectorParser = _rangeParser = new(_streamer, _scopes, _name);
    }
    /// <summary>
    /// Starts the parsing of the entire generic block.
    /// </summary>
    public void Parse(){
        _logger.LogInformation("Parsing Generic");
        ParseStructure();
        _logger.LogInformation("Generic Parsed");
    }
    /// <summary>
    /// Parses the entire generic structure including the 'generic' keyword, parentheses, and all constant declarations within.
    /// </summary>
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
    /// <summary>
    /// Parses individual generic constant declarations within the generic block.
    /// </summary>
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
                    // Asignation - value can be literal or identifier (package constant)
                    _streamer.Expect(OPERATORS);
                    if(_streamer.Current?.Type == LITERALS)
                        _streamer.Expect(LITERALS);
                    else if(_streamer.Current?.Type == IDENTIFIERS)
                        _streamer.Expect(IDENTIFIERS);
                    else if(_streamer.Current?.Type == QUALIFIED_IDENTIFIERS)
                        _streamer.Expect(QUALIFIED_IDENTIFIERS);
                }
                if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower().Equals("after")){
                    _streamer.Expect(KEYWORDS); // after
                    _streamer.Expect(LITERALS);
                    _streamer.Expect(TIME_VARIABLES);
                }
            }
        }
    }
    /// <summary>
    /// Parses comma-separated identifiers in a generic declaration, adding each as a new variable to the current scope.
    /// </summary>
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
    /// <summary>
    /// Checks if a variable exists within the current generic's scope.
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
}