using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;
namespace Parser.Repositories;

/// <summary>
/// A parser for VHDL 'function' blocks. It handles the function's signature (name, parameters, return type) and its body.
/// </summary>
public class FunctionParser : IParseStructures {
    private TokenStream _streamer;
    private readonly ILogger<ArchitectureParser> _logger = SharedLogging.Factory.CreateLogger<ArchitectureParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private string _name = string.Empty, _root = string.Empty, _returnType = string.Empty;
    private DataStructuresParser _dataStructuresParser = default!;
    private FunctionAssignationParser _functionAssignationParser = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionParser"/> class.
    /// </summary>
    /// <param name="stream">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    /// <param name="RootName">The name of the parent scope (e.g., package or architecture name).</param>
    public FunctionParser(TokenStream stream, Dictionary<string, HashSet<Variable>> scopes, string RootName)
    {
        _scopes = scopes;
        _streamer = stream;
        _root = RootName;
    }
    /// <summary>
    /// Gets the name of the parsed function.
    /// </summary>
    /// <returns>The function name.</returns>
    public string GetName() => _name;
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
    /// Parses the parameter declarations of the function.
    /// </summary>
    public void ParseDeclaration(){
        _functionAssignationParser = new(_streamer, _scopes, _name);
        _functionAssignationParser.Parse();
    }
    /// <summary>
    /// Parses the body of the function, which contains:
    /// 1. Optional return type bounds declaration (e.g., STD_LOGIC_VECTOR(... downto 0);)
    /// 2. Local declarations (variable, constant) before 'begin'
    /// 3. Sequential statements after 'begin'
    /// </summary>
    public void ParseBodyDeclaration(){
        // First, check for optional return type bounds declaration
        if(_streamer.Current?.Value.ToLower() == _returnType.ToLower()){
            _streamer.Expect(KEYWORDS); // return type (e.g., std_logic_vector)
            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                // Parse return type bounds
                ParseReturnTypeBounds();
            }
            _streamer.Expect(OPERATORS); // ;
        }
        // Parse local declarations (variable, constant) before 'begin'
        while(_streamer.Current?.Value.ToLower() != "begin"){
            string declType = _streamer.Current?.Value.ToLower() ?? string.Empty;
            if(declType == "variable" || declType == "constant"){
                _streamer.Expect(KEYWORDS); // variable/constant
                ParseLocalDeclaration();
            }
            else{
                // Unknown declaration type - might be an error
                break;
            }
        }
        _streamer.Expect(KEYWORDS); // begin
        _dataStructuresParser = new(_streamer, _scopes, _name, string.Empty, true);
        _dataStructuresParser.ParseSequentialBody();
    }
    /// <summary>
    /// Parses return type bounds like ((A'length + B'length) - 1 downto 0).
    /// </summary>
    private void ParseReturnTypeBounds(){
        _streamer.Expect(PARENTHESIS); // (
        int depth = 1;
        while(depth > 0){
            if(_streamer.Current?.Type == PARENTHESIS){
                if(_streamer.Current?.Value == "(") depth++;
                else if(_streamer.Current?.Value == ")") depth--;
                if(depth == 0) break;
                _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == IDENTIFIERS){
                _streamer.Expect(IDENTIFIERS);
            }
            else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                _streamer.MoveNext(); // Handle A'length, etc.
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
    /// Parses a local variable or constant declaration within a function.
    /// Handles multiple variables on same line and expression bounds.
    /// </summary>
    private void ParseLocalDeclaration(){
        // Parse variable/constant names (can be comma-separated)
        while(_streamer.Current?.Type == IDENTIFIERS){
            string varName = _streamer.Current?.Value ?? string.Empty;
            _scopes[_name].Add(varName);
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == ",")
                _streamer.MoveNext();
            else
                break;
        }
        _streamer.Expect(OPERATORS); // :
        string dataType = _streamer.Current?.Value.ToLower() ?? string.Empty;
        // Data type can be keyword or identifier (custom type)
        if(_streamer.Current?.Type == KEYWORDS)
            _streamer.Expect(KEYWORDS);
        else if(_streamer.Current?.Type == IDENTIFIERS)
            _streamer.Expect(IDENTIFIERS);
        // Handle vector types with bounds or custom types with array access
        if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
            ParseVectorBounds();
        }
        // Handle range constraint like "integer range 0 to N"
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "range"){
            _streamer.Expect(KEYWORDS); // range
            // Parse range bounds until semicolon or :=
            while(_streamer.Current?.Value != ";" && _streamer.Current?.Value != ":="){
                _streamer.MoveNext();
            }
        }
        // Handle initialization := value
        if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":="){
            _streamer.Expect(OPERATORS); // :=
            ParseInitializationValue();
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Checks if the given data type is a vector type that requires bounds.
    /// </summary>
    private bool IsVectorType(string dataType)
        => dataType == "std_logic_vector" || dataType == "bit_vector" ||
           dataType == "boolean_vector" || dataType == "integer_vector" ||
           dataType == "signed" || dataType == "unsigned";
    /// <summary>
    /// Parses vector bounds like (31 downto 0), ((DATA_WIDTH + COEFF_WIDTH) - 1 downto 0), or (sig'range).
    /// </summary>
    private void ParseVectorBounds(){
        _streamer.Expect(PARENTHESIS); // (
        int depth = 1;
        while(depth > 0 && _streamer.Current != null){
            if(_streamer.Current?.Type == PARENTHESIS){
                if(_streamer.Current?.Value == "(") depth++;
                else if(_streamer.Current?.Value == ")") depth--;
                if(depth > 0) _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == LITERALS)
                _streamer.MoveNext();
            else if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.MoveNext();
            else if(_streamer.Current?.Type == KEYWORDS)
                _streamer.MoveNext();
            else if(_streamer.Current?.Type == OPERATORS)
                _streamer.MoveNext();
            else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES)
                _streamer.MoveNext();
            else
                break;
        }
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Parses initialization value which can be a literal, identifier, vector attribute, or complex expression.
    /// </summary>
    private void ParseInitializationValue(){
        // Parse expression until we hit semicolon
        while(_streamer.Current?.Value != ";"){
            if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == IDENTIFIERS){
                _streamer.Expect(IDENTIFIERS);
            }
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                // Aggregate like (others => '0') or parenthesized expression
                ParseAggregateExpression();
            }
            else if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";"){
                _streamer.Expect(OPERATORS);
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                // Handle keywords like 'not', 'and', 'or', etc.
                _streamer.Expect(KEYWORDS);
            }
            else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                // Handle vector attributes like vec'length, vec'range
                _streamer.MoveNext();
            }
            else{
                break;
            }
        }
    }
    /// <summary>
    /// Parses aggregate expression like (others => '0').
    /// </summary>
    private void ParseAggregateExpression(){
        _streamer.Expect(PARENTHESIS); // (
        while(_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")"){
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "others"){
                _streamer.Expect(KEYWORDS);
                _streamer.Expect(OPERATORS); // =>
                if(_streamer.Current?.Type == LITERALS)
                    _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            else
                break;
        }
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Starts the parsing of the entire function block.
    /// </summary>
    public void Parse(){
        _logger.LogInformation("Parsing function");
        _streamer.Expect(KEYWORDS);
        string funcBaseName = _streamer.Current?.Value ?? string.Empty;
        // Create a unique function scope name by combining root scope and function name
        // For overloaded functions, add a suffix to make scope names unique
        int overloadIndex = 0;
        _name = $"{_root}_{funcBaseName}";
        while(_scopes.ContainsKey(_name)){
            overloadIndex++;
            _name = $"{_root}_{funcBaseName}_{overloadIndex}";
        }
        // Add function to root scope (only once for the base name)
        if(_scopes.ContainsKey(_root) && !_scopes[_root].Contains(funcBaseName)){
            _scopes[_root].Add(funcBaseName);
        }
        // Create function scope
        _scopes.Add(_name, new());
        _streamer.Expect(IDENTIFIERS);
        if(_streamer.Current?.Type == PARENTHESIS){
            // FUNCTION WITH PARAMETERS
            _streamer.Expect(PARENTHESIS);
            ParseDeclaration();
            _streamer.Expect(PARENTHESIS);
        }
        _streamer.Expect(KEYWORDS); // return
        _returnType = _streamer.Current?.Value ?? string.Empty;
        // Return type can be either keyword (std_logic, integer) or identifier (custom type)
        if(_streamer.Current?.Type == KEYWORDS)
            _streamer.Expect(KEYWORDS);
        else if(_streamer.Current?.Type == IDENTIFIERS)
            _streamer.Expect(IDENTIFIERS);
        // Handle vector return type bounds (optional)
        if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
            ParseReturnTypeBounds();
        }
        if(_streamer.Current?.Type == OPERATORS){
            _streamer.Expect(OPERATORS); // ;
            _logger.LogInformation("Single Function Parsed");
            _scopes.Remove(_name);
            _scopes[_root].Remove(_name);
            return;
        }
        // Function with body
        _streamer.Expect(KEYWORDS); // is
        ParseBodyDeclaration();
        // Parse function closure: end [function_name];
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(IDENTIFIERS); // function_name
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("Function Parsed");
    }
}