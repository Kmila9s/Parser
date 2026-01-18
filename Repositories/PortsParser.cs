using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using System.Numerics;
using Parser.Models;

namespace Parser.Repositories;

/// <summary>
/// A parser for VHDL 'port' blocks. It handles the declaration of input and output ports for an entity.
/// </summary>
public class PortsParser : IParseDeclarations {
    private TokenStream _streamer;
    private readonly ILogger<PortsParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PortsParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private VectorParser _vectorParser = default!;
    private RangeParser _rangeParser = default!;
    private string _name = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortsParser"/> class.
    /// </summary>
    /// <param name="stream">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    /// <param name="Name">The name of the scope (e.g., entity name) where the ports belong.</param>
    public PortsParser(TokenStream stream, Dictionary<string, HashSet<Variable>> scopes, string Name)
    {
        _streamer = stream;
        _name = Name;
        _scopes = scopes;
    }
    /// <summary>
    /// Starts the parsing of the entire port block.
    /// </summary>
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
    /// <summary>
    /// Parses individual port declarations within the port block.
    /// </summary>
    public void ParseDeclarations(){
        List<Variable> variables = new();
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!_scopes.ContainsKey(_name))
                throw new ParserException(MISSING_STRUCTURE);
            if(variables.Contains(_streamer.Current.Value))
                throw new ParserException(DUPLICATE_DECLARATION);
            Variable variable = new(){
                Name = _streamer.Current.Value
            };
            
            _streamer.Expect(IDENTIFIERS);
            variables.Add(variable);
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

        if(!_vectorParser.Parsed && !_rangeParser.Parsed){
            string dataType = _streamer.Current?.Value ?? string.Empty;
            variables.ForEach(p => {
                p.Type = Constants.GetDatatype(dataType);
                _scopes[_name].Add(p);
            });
            // Data type can be keyword (std_logic, integer) or identifier (custom type)
            if(_streamer.Current?.Type == KEYWORDS)
                _streamer.Expect(KEYWORDS);
            else if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.Expect(IDENTIFIERS);
            // Handle array type with bounds like coefficient_array_t(0 to N-1)
            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseTypeBounds();
            }
        }else{
            if(_vectorParser.Parsed)
                variables.ForEach(p => {
                    p.Type = Constants.DATATYPES.STD_LOGIC_VECTOR;
                    _scopes[_name].Add(p);
                });
            else if(_rangeParser.Parsed)
                variables.ForEach(p => {
                    p.Type = Constants.DATATYPES.STD_LOGIC_VECTOR;
                    _scopes[_name].Add(p);
                });
        }
    }

    /// <summary>
    /// Parses type bounds like (0 to N-1) for array types.
    /// </summary>
    private void ParseTypeBounds(){
        _streamer.Expect(PARENTHESIS); // (
        int depth = 1;
        while(depth > 0 && _streamer.Current != null){
            if(_streamer.Current?.Type == PARENTHESIS){
                if(_streamer.Current?.Value == "(") depth++;
                else if(_streamer.Current?.Value == ")") depth--;
                if(depth == 0) break;
            }
            _streamer.MoveNext();
        }
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Checks if a variable exists within the current port's scope.
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