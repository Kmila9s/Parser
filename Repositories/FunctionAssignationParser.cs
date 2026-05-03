using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;
namespace Parser.Repositories;

/// <summary>
/// A parser for the parameter declarations within a VHDL function signature.
/// </summary>
public class FunctionAssignationParser : IParseDeclarations {
    private TokenStream _streamer;
    private readonly ILogger<FunctionAssignationParser> _logger = SharedLogging.Factory.CreateLogger<FunctionAssignationParser>();
    private string _name = string.Empty, _entityName = string.Empty;
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    //private VectorParser _vectorParser = default!;
    //private RangeParser _rangeParser = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionAssignationParser"/> class.
    /// </summary>
    /// <param name="stream">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    /// <param name="Name">The name of the function scope.</param>
    public FunctionAssignationParser(TokenStream stream, Dictionary<string, HashSet<Variable>> scopes, string Name)
    {
        _streamer = stream;
        _scopes = scopes;
        _name = Name;
    }
    /// <summary>
    /// Starts the parsing of the function parameter declarations.
    /// </summary>
    public void Parse()
    {
        _logger.LogInformation("Parsing Function Assignatinos");
        ParseDeclarations();
        _logger.LogInformation("Function assignations parsed");
    }
    /// <summary>
    /// Gets the dictionary of scopes.
    /// </summary>
    /// <returns>The dictionary of scopes.</returns>
    public Dictionary<string, HashSet<Variable>> GetScopes() => _scopes;
    /// <summary>
    /// Checks if a variable exists within the current function's scope.
    /// </summary>
    /// <param name="variable">The name of the variable to check.</param>
    /// <returns>True if the variable exists, otherwise false.</returns>
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    /// <summary>
    /// Parses the list of parameter declarations.
    /// Handles vector types like std_logic_vector(31 downto 0).
    /// </summary>
    public void ParseDeclarations(){
        while(_streamer.Current?.Type == IDENTIFIERS){
            ParseWithComas();
            _streamer.Expect(OPERATORS); // :
            string dataType = _streamer.Current?.Value.ToLower() ?? string.Empty;
            // Data type can be keyword (std_logic, integer) or identifier (custom type)
            if(_streamer.Current?.Type == KEYWORDS)
                _streamer.Expect(KEYWORDS);
            else if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.Expect(IDENTIFIERS);
            // If it's a vector type or custom type with array access, parse the bounds
            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseVectorBounds();
            }
            // Check if there are more parameters separated by ;
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ";")
                _streamer.Expect(OPERATORS);
            else
                break;
        }
    }
    /// <summary>
    /// Checks if the given data type is a vector type that requires bounds.
    /// </summary>
    private bool IsVectorType(string dataType)
        => dataType == "std_logic_vector" || dataType == "bit_vector" ||
           dataType == "boolean_vector" || dataType == "integer_vector" ||
           dataType == "signed" || dataType == "unsigned";
    /// <summary>
    /// Parses vector bounds like (31 downto 0).
    /// </summary>
    private void ParseVectorBounds(){
        _streamer.Expect(PARENTHESIS); // (
        // Parse the bound expression
        while(_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")"){
            if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == IDENTIFIERS){
                _streamer.Expect(IDENTIFIERS);
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                _streamer.Expect(KEYWORDS); // downto, to
            }
            else if(_streamer.Current?.Type == OPERATORS){
                _streamer.Expect(OPERATORS);
            }
            else{
                break;
            }
        }
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Parses comma-separated identifiers for function parameters, adding them to the function's scope.
    /// </summary>
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