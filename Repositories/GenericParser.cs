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
    private readonly ILogger<GenericParser> _logger = SharedLogging.Factory.CreateLogger<GenericParser>();
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
                    // Default-value assignment. Earlier this slot only accepted a single
                    // token (literal / identifier / qualified id), which rejected real-
                    // world generics like `TOTAL : integer := BASE * TIMES`. Consume a
                    // full arithmetic expression up to the next `;` or top-level `)`.
                    _streamer.Expect(OPERATORS); // :=
                    ParseDefaultExpression();
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
    /// Consumes the right-hand side of a generic's <c>:=</c> default-value clause,
    /// stopping at the next top-level <c>;</c> or the closing <c>)</c> of the
    /// generic block. Handles arbitrary arithmetic and function-call expressions
    /// (e.g. <c>BASE * TIMES</c>, <c>clog2(DEPTH) + 1</c>), nested parentheses,
    /// time-unit suffixes, and package-qualified identifiers.
    /// </summary>
    /// <remarks>
    /// We don't constant-fold here — the value's numerical result is computed
    /// later by <c>VariableSyntetizer</c> when it sees the generic referenced
    /// from a port-width range expression. This parser pass only needs to
    /// advance the streamer past the whole RHS so the next generic decl
    /// starts at the right token.
    /// </remarks>
    private void ParseDefaultExpression(){
        int depth = 0;
        while(_streamer.Current != null){
            var t = _streamer.Current;
            // Statement / block terminator at depth 0.
            if(depth == 0 && t.Type == OPERATORS && t.Value == ";") return;
            if(depth == 0 && t.Type == PARENTHESIS && t.Value == ")") return;
            // Track parenthesis nesting so we don't mistake an inner `)`
            // for the end of the generic block.
            if(t.Type == PARENTHESIS && t.Value == "(") depth++;
            else if(t.Type == PARENTHESIS && t.Value == ")") depth--;
            _streamer.MoveNext();
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