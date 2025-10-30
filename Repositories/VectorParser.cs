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
    private readonly ILogger<VectorParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<VectorParser>();
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
        if(!ExistsVariable(variable ?? string.Empty))
            throw new ParserException(MISSING_DECLARATION);
        _streamer.MoveNext();
    }
    private bool IsVector(string dataType) 
        => dataType.Equals("STD_LOGIC_VECTOR") || dataType.Equals("BIT_VECTOR") ||
            dataType.Equals("BOOLEAN_VECTOR") || dataType.Equals("INTEGER_VECTOR");
    /// <summary>
    /// Parses the body of the vector declaration (e.g., '(7 downto 0)').
    /// </summary>
    public virtual void ParseBody(){
        _streamer.Expect(PARENTHESIS);
        ParseOperation();
        _streamer.Expect(KEYWORDS); // downto
        ParseOperation();
        _streamer.Expect(PARENTHESIS);
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
            else if(_streamer.Current?.Type == IDENTIFIERS)
                if(!ExistsVariable(_streamer.Current.Value))
                    throw new ParserException(MISSING_DECLARATION);
                else
                    _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";")
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToString() != "downto")
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
    /// Parses expressions enclosed in parentheses.
    /// </summary>
    private void ParseParenthesis(){
        _streamer.Expect(PARENTHESIS);
        ParseOperation();
        _streamer.Expect(PARENTHESIS);
    }
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