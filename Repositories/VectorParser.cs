using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;

namespace Parser.Repositories;

/// <summary>
/// A parser for VHDL vector type declarations (e.g., std_logic_vector).
/// </summary>
public class VectorParser : IParseVectorStructure {
    private readonly TokenStream _streamer;
    private readonly ILogger<VectorParser> _logger = SharedLogging.Factory.CreateLogger<VectorParser>();
    private Dictionary<string, HashSet<Variable>> _scopes;
    /// <summary>
    /// Gets the calculated size of the vector.
    /// </summary>
    public int VectorSize { get; private set; }
    private string _name;
    /// <summary>
    /// Gets a value indicating whether the parser successfully parsed a vector declaration.
    /// </summary>
    public bool Parsed { get; private set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorParser"/> class.
    /// </summary>
    /// <param name="streamer">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    /// <param name="name">The name of the current scope.</param>
    public VectorParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes, string name)
    {
        _streamer = streamer;
        _scopes = scopes;
        _name = name;
    }
    /// <summary>
    /// Starts the parsing process. It checks if the current token sequence represents a vector type and parses it if it does.
    /// </summary>
    public virtual void Parse(){
        _logger.LogInformation("Parsing vector");
        string dataType = (_streamer.Current?.Value ?? string.Empty).ToUpper();
        if(IsVector(dataType)){
            _streamer.Expect(KEYWORDS); // DATATYPE
            ParseBody();
            Parsed = true;
        }
        _logger.LogInformation("Vector Parsed");
    }
    /// <summary>
    /// Parses vector attributes like 'length or 'range.
    /// </summary>
    public void ParseAttributes(){
        var variable = _streamer.Current?.Value.Split("'")[0];
        var attribute = _streamer.Current?.Value.Split("'")[1];
        // Don't enforce strict scope checking for attributes - the base variable
        // might be from a package scope, entity port, or other accessible scope
        // that's not directly in our current scope hierarchy
        _streamer.MoveNext();
    }
    /// <summary>
    /// Determines if the given data type string represents a VHDL vector type.
    /// </summary>
    /// <param name="dataType">The uppercased data type name.</param>
    /// <returns>True if the type is a vector type (STD_LOGIC_VECTOR, BIT_VECTOR, SIGNED, UNSIGNED, etc.).</returns>
    private bool IsVector(string dataType)
        => dataType.Equals("STD_LOGIC_VECTOR") || dataType.Equals("BIT_VECTOR") ||
            dataType.Equals("BOOLEAN_VECTOR") || dataType.Equals("INTEGER_VECTOR") ||
            dataType.Equals("SIGNED") || dataType.Equals("UNSIGNED") ||
            dataType.Equals("STD_ULOGIC_VECTOR");
    /// <summary>
    /// Parses the body of the vector declaration (e.g., '(7 downto 0)', '((2*WIDTH) downto 0)').
    /// </summary>
    public virtual void ParseBody(){
        _streamer.Expect(PARENTHESIS); // (
        // Parse until we find 'downto' or 'to' at the same depth level
        ParseBoundExpression();
        // Now expect 'downto' or 'to'
        if(_streamer.Current?.Type == KEYWORDS &&
           (_streamer.Current?.Value.ToLower() == "downto" || _streamer.Current?.Value.ToLower() == "to")){
            _streamer.Expect(KEYWORDS); // downto/to
        }
        // Parse the second bound
        ParseBoundExpression();
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Parses a bound expression which may contain nested parentheses.
    /// Stops when hitting 'downto', 'to', or ')' at depth 0.
    /// </summary>
    private void ParseBoundExpression(){
        int depth = 0;
        while(_streamer.Current != null){
            string val = _streamer.Current?.Value.ToLower() ?? string.Empty;
            // Stop conditions at depth 0
            if(depth == 0 && (_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == ")")){
                break;
            }
            if(depth == 0 && _streamer.Current?.Type == KEYWORDS && (val == "downto" || val == "to")){
                break;
            }
            // Handle parentheses for depth tracking
            if(_streamer.Current?.Type == PARENTHESIS){
                if(_streamer.Current?.Value == "(") depth++;
                else if(_streamer.Current?.Value == ")") depth--;
                _streamer.MoveNext();
            }
            // Handle function calls
            else if(_streamer.Current?.Type == IDENTIFIERS){
                _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == OPERATORS){
                _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                _streamer.MoveNext();
            }
            else{
                break;
            }
        }
    }
    /// <summary>
    /// Parses a complex expression, typically used for defining the bounds of a vector.
    /// </summary>
    private void ParseOperation(){
        while(_streamer.Current?.Value != ";" || _streamer.Current?.Type != PARENTHESIS){
            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
                if((_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";") || _streamer.Current?.Type == KEYWORDS){
                    _streamer.MoveNext();
                    ParseOperation();
                }
            }
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "not")
                _streamer.MoveNext();
            if(_streamer.Current?.Type == VECTORS_ATTRIBUTES)
                ParseAttributes();
            else if(_streamer.Current?.Type == IDENTIFIERS){
                string identName = _streamer.Current.Value;
                _streamer.Expect(IDENTIFIERS);
                // Check if this is a function call (identifier followed by parenthesis)
                if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                    // Function call in vector bounds - parse arguments
                    ParseFunctionCall();
                }
                else if(!ExistsVariable(identName)){
                    // Allow unknown identifiers as they could be generic parameters or function names
                    // In vector bounds, we're more permissive
                }
            }
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";")
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToString().ToLower() != "downto" && _streamer.Current.Value.ToString().ToLower() != "to")
                if(_streamer.Current.Value.ToLower().Equals("after")){
                    _streamer.MoveNext();
                    if(_streamer.Current?.Type == LITERALS || (_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value)))
                        _streamer.MoveNext();
                    else
                        throw new ParserException(SYNTAX_ERROR);
                    _streamer.Expect(TIME_VARIABLES);
                }else
                    _streamer.Expect(KEYWORDS); // CASES LIKE XOR, AND, OR
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "(")
                continue;
            else
                break;
        }
    }
    /// <summary>
    /// Parses function call arguments in vector bounds like clog2(DEPTH).
    /// </summary>
    private void ParseFunctionCall(){
        _streamer.Expect(PARENTHESIS); // (
        // Parse function arguments
        while(_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")"){
            if(_streamer.Current?.Type == IDENTIFIERS){
                _streamer.Expect(IDENTIFIERS);
                // Nested function call
                if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                    ParseFunctionCall();
                }
            }
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == OPERATORS){
                _streamer.Expect(OPERATORS);
            }
            else if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == ","){
                _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
            }
            else{
                break;
            }
        }
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Parses expressions enclosed in parentheses.
    /// </summary>
    private void ParseParenthesis(){
        _streamer.Expect(PARENTHESIS);
        ParseOperation();
        _streamer.Expect(PARENTHESIS);
    }
    /// <summary>
    /// Checks if the given token is an identifier that exists as a declared variable in the current scope.
    /// </summary>
    /// <param name="current">The token to check.</param>
    /// <returns>True if the token is a declared identifier; otherwise, false.</returns>
    protected bool CheckVariable(Token? current)
        => current?.Type == IDENTIFIERS && ExistsVariable(current.Value);
    /// <summary>
    /// Gets the dictionary of scopes.
    /// </summary>
    /// <returns>The dictionary of scopes.</returns>
    public Dictionary<string, HashSet<Variable>> GetScopes() => _scopes;
    /// <summary>
    /// Checks if a variable exists within the current scope.
    /// </summary>
    /// <param name="variable">The name of the variable to check.</param>
    /// <returns>True if the variable exists, otherwise false.</returns>
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
}