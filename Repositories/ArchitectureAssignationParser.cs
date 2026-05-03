using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using Parser.Models;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;

namespace Parser.Repositories;

/// <summary>
/// A parser for declarations within a VHDL architecture header, including signals, constants,
/// attribute declarations/specifications, disconnect specifications, and guarded signal support.
/// </summary>
public class ArchitectureAssignationParser : IParseDeclarations{
    private TokenStream _streamer;
    private readonly ILogger<ArchitectureAssignationParser> _logger = SharedLogging.Factory.CreateLogger<ArchitectureAssignationParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private VectorParser _vectorParser = default!;
    private RangeParser _rangeParser = default!;
    private string _name = string.Empty, _entityName = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchitectureAssignationParser"/> class.
    /// </summary>
    /// <param name="stream">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    /// <param name="Name">The name of the architecture scope.</param>
    /// <param name="EntityName">The name of the associated entity scope, used for variable lookups.</param>
    public ArchitectureAssignationParser(TokenStream stream, Dictionary<string, HashSet<Variable>> scopes, string Name, string EntityName = "")
    {
        _streamer = stream;
        _name = Name;
        _scopes = scopes;
        _entityName = EntityName;
    }
    /// <summary>
    /// Starts the parsing of the architecture declarations.
    /// </summary>
    public virtual void Parse(){
        _logger.LogInformation("Parsing Architecture Assignatinos");
        ParseDeclarations();
        _logger.LogInformation("Architecture assignations parsed");
    }
    /// <summary>
    /// Gets the dictionary of scopes.
    /// </summary>
    /// <returns>The dictionary of scopes.</returns>
    public Dictionary<string, HashSet<Variable>> GetScopes() => _scopes;
    /// <summary>
    /// Checks if a variable exists within the current architecture's scope.
    /// </summary>
    /// <param name="variable">The name of the variable to check.</param>
    /// <returns>True if the variable exists, otherwise false.</returns>
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    /// <summary>
    /// Parses signal, constant, and other declarations in the architecture header.
    /// </summary>
    public void ParseDeclarations(){
        while(_streamer.Current?.Type == KEYWORDS &&
            (
                _streamer.Current?.Value == "signal" || _streamer.Current?.Value == "constant" ||
                _streamer.Current?.Value == "attribute" || _streamer.Current?.Value == "group" ||
                _streamer.Current?.Value == "disconnect"
            )
        ){
            string declType = _streamer.Current.Value.ToLower();
            _streamer.Expect(KEYWORDS);

            // Handle different declaration types
            if(declType == "attribute"){
                ParseAttributeDeclaration();
                continue;
            }
            if(declType == "disconnect"){
                ParseDisconnectSpecification();
                continue;
            }

            // Standard signal/constant declaration
            ParseWithComas();
            _streamer.Expect(OPERATORS); // :
            string val = _streamer.Current?.Value.ToUpper() ?? string.Empty;

            _vectorParser = new(_streamer, _scopes, String.IsNullOrEmpty(_entityName) ? _name : _entityName );
            _vectorParser.Parse();

            _rangeParser = new(_streamer, _scopes, String.IsNullOrEmpty(_entityName) ? _name : _entityName );
            _rangeParser.Parse();

            if(!_vectorParser.Parsed && !_rangeParser.Parsed){
                // Type can be keyword (std_logic, integer) or identifier (custom type)
                if(_streamer.Current?.Type == KEYWORDS)
                    _streamer.Expect(KEYWORDS);
                else if(_streamer.Current?.Type == IDENTIFIERS)
                    _streamer.Expect(IDENTIFIERS);
            }

            if(val.ToLower().Equals("integer")){
                _rangeParser = new(_streamer, _scopes, String.IsNullOrEmpty(_entityName) ? _name : _entityName );
                _rangeParser.Parse();
            }
            if(val.ToLower().Equals("time")){
                _streamer.Expect(OPERATORS);
                _streamer.Expect(LITERALS);
                _streamer.Expect(TIME_VARIABLES);
            }
            // Handle 'bus' keyword for guarded signals
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "bus"){
                _streamer.Expect(KEYWORDS);
            }
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current.Value != ";"){
                // Assignment - can be a literal, aggregate expression, or complex expression
                _streamer.Expect(OPERATORS); // :=
                ParseInitializationExpression();
            }
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower().Equals("after")){
                _streamer.Expect(KEYWORDS); // after
                _streamer.Expect(LITERALS);
                _streamer.Expect(TIME_VARIABLES);
            }
            _streamer.Expect(OPERATORS); // ;
        }
    }
    /// <summary>
    /// Parses attribute declarations and specifications.
    /// Format 1: attribute attr_name : type;
    /// Format 2: attribute attr_name of target : class is value;
    /// </summary>
    private void ParseAttributeDeclaration(){
        string attrName = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(IDENTIFIERS); // attribute name

        if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":"){
            // Attribute type declaration: attribute attr_name : type;
            _streamer.Expect(OPERATORS); // :
            _streamer.Expect(KEYWORDS); // type (string, boolean, etc.)
            _streamer.Expect(OPERATORS); // ;
        }
        else if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "of"){
            // Attribute specification: attribute attr_name of target : class is value;
            _streamer.Expect(KEYWORDS); // of
            _streamer.Expect(IDENTIFIERS); // target name (signal name)
            _streamer.Expect(OPERATORS); // :
            _streamer.Expect(KEYWORDS); // class (signal, variable, etc.)
            _streamer.Expect(KEYWORDS); // is
            // Value can be a string literal or other literal
            if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            _streamer.Expect(OPERATORS); // ;
        }
    }
    /// <summary>
    /// Parses disconnect specifications for guarded signals.
    /// Format: disconnect signal_list : type after time_expression;
    /// </summary>
    private void ParseDisconnectSpecification(){
        // disconnect all : std_logic after 10 ns;
        // disconnect sig1, sig2 : std_logic after 5 ns;
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "all"){
            _streamer.Expect(KEYWORDS); // all
        }
        else{
            // Parse signal list
            while(_streamer.Current?.Type == IDENTIFIERS){
                _streamer.Expect(IDENTIFIERS);
                if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == ",")
                    _streamer.MoveNext();
                else
                    break;
            }
        }
        _streamer.Expect(OPERATORS); // :
        _streamer.Expect(KEYWORDS); // type (std_logic, etc.)
        _streamer.Expect(KEYWORDS); // after
        _streamer.Expect(LITERALS); // time value
        _streamer.Expect(TIME_VARIABLES); // ns, ps, etc.
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses comma-separated identifiers in a declaration, adding each as a new variable to the current scope.
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
    /// <summary>
    /// Parses aggregate expressions like (others => '0') used in signal/constant initialization.
    /// </summary>
    private void ParseAggregateExpression(){
        _streamer.Expect(PARENTHESIS); // (
        // Parse aggregate content - handles 'others => value', function args, and expressions
        int depth = 1;
        while(depth > 0 && _streamer.Current != null){
            if(_streamer.Current?.Type == PARENTHESIS){
                if(_streamer.Current?.Value == "("){
                    depth++;
                    _streamer.MoveNext();
                }
                else if(_streamer.Current?.Value == ")"){
                    depth--;
                    if(depth == 0) break;
                    _streamer.MoveNext();
                }
            }
            else if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower() == "others"){
                _streamer.Expect(KEYWORDS); // others
                _streamer.Expect(OPERATORS); // =>
                if(_streamer.Current?.Type == LITERALS){
                    _streamer.Expect(LITERALS);
                }
            }
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == IDENTIFIERS){
                _streamer.Expect(IDENTIFIERS);
            }
            else if(_streamer.Current?.Type == OPERATORS){
                _streamer.Expect(OPERATORS);
            }
            else if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == ","){
                // Handle comma-separated arguments in function calls
                _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                _streamer.Expect(KEYWORDS);
            }
            else{
                break;
            }
        }
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Parses initialization expressions in signal/constant declarations.
    /// Handles literals, aggregates, and expressions with concatenation operators.
    /// </summary>
    private void ParseInitializationExpression(){
        // Continue parsing until we hit a semicolon
        while(_streamer.Current?.Type != OPERATORS || _streamer.Current?.Value != ";"){
            if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseAggregateExpression();
            }
            else if(_streamer.Current?.Type == IDENTIFIERS){
                // Could be a port or signal reference in initialization
                _streamer.Expect(IDENTIFIERS);
            }
            else if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";"){
                // Concatenation operator & or arithmetic operators
                _streamer.Expect(OPERATORS);
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                // Handle 'after' for timing - break and let caller handle it
                if(_streamer.Current.Value.ToLower() == "after")
                    break;
                _streamer.Expect(KEYWORDS);
            }
            else{
                break;
            }
        }
    }
}