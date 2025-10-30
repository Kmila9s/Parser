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
    private readonly ILogger<ArchitectureParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ArchitectureParser>();
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
    /// Parses the body of the function, which contains local declarations and sequential statements.
    /// </summary>
    public void ParseBodyDeclaration(){
        if(_streamer.Current?.Value.ToString() != "begin"){
            // NOT EMPTY IS DECLARATION
            string returnType = _streamer.Current?.Value.ToString() ?? string.Empty;
            _streamer.Expect(KEYWORDS);
            if(!returnType.ToLower().Equals(_returnType.ToLower()))
                throw new ParserException(SYNTAX_ERROR);
            if(returnType.ToLower().Equals("std_logic_vector")){
                VectorParser vectorParser = new(_streamer, _scopes, _name);
                vectorParser.ParseBody();
            }
            _streamer.Expect(OPERATORS); // ;
            
        }
        _streamer.Expect(KEYWORDS); // begin
        _dataStructuresParser = new(_streamer, _scopes, _name, string.Empty, true);
        _dataStructuresParser.Parse();

    }
    /// <summary>
    /// Starts the parsing of the entire function block.
    /// </summary>
    public void Parse(){
        _logger.LogInformation("Parsing function");
        _streamer.Expect(KEYWORDS);
        _name = _streamer.Current?.Value ?? string.Empty;
        if(_scopes.ContainsKey(_root) && !_scopes[_root].Contains(_name)){
            _scopes[_root].Add(_name);
            _scopes.Add(_name, new());
        }
        _streamer.Expect(IDENTIFIERS);
        if(_streamer.Current?.Type == PARENTHESIS){
            // FUNCTION WITH PARAMETERS
            _streamer.Expect(PARENTHESIS);
            ParseDeclaration();
            _streamer.Expect(PARENTHESIS);
        }
        _streamer.Expect(KEYWORDS);
        _returnType = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(KEYWORDS); // RETURN DATATYPE
        if(_streamer.Current?.Type == OPERATORS){
            _streamer.Expect(OPERATORS);
            _logger.LogInformation("Single Function Parsed");
            _scopes.Remove(_name);
            _scopes[_root].Remove(_name);
            return;
        }
        // Function with body
        _streamer.Expect(KEYWORDS); // is
        ParseBodyDeclaration();
        _logger.LogInformation("Function Parsed");
    }
}