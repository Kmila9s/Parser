using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;
namespace Parser.Repositories;

/// <summary>
/// A parser for sequential VHDL statements like if-then-else, case, for-loop, and signal assignments.
/// It operates within the context of a process, function, or architecture body.
/// </summary>
public class DataStructuresParser : IParseStructures {
    private TokenStream _streamer;
    private readonly ILogger<DataStructuresParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DataStructuresParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private string _name = string.Empty, _entityName = string.Empty, _implicitReturnVariable = string.Empty;
    private bool _isFunction = false, _implicitDeclaration = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataStructuresParser"/> class.
    /// </summary>
    /// <param name="streamer">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    /// <param name="name">The name of the current scope (e.g., process or function name).</param>
    /// <param name="EntityName">The name of the parent entity scope for variable lookups.</param>
    /// <param name="IsFunction">A flag indicating if the parser is operating within a function body.</param>
    public DataStructuresParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes, string name, string EntityName, bool IsFunction = false)
    {
        _streamer = streamer;
        _scopes = scopes;
        _name = name;
        _entityName = EntityName;
        _isFunction = IsFunction;
    }
    /// <summary>
    /// Gets the name of the current scope.
    /// </summary>
    /// <returns>The name of the scope.</returns>
    public string GetName() => _name;
    /// <summary>
    /// Gets the dictionary of scopes.
    /// </summary>
    /// <returns>The dictionary of scopes.</returns>
    public Dictionary<string, HashSet<Variable>> GetScopes() => _scopes;
    /// <summary>
    /// Checks if a variable exists within the current or parent entity scope.
    /// </summary>
    /// <param name="variable">The name of the variable to check.</param>
    /// <returns>True if the variable exists, otherwise false.</returns>
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable) || 
            _scopes.ContainsKey(_entityName) && _scopes[_entityName].Contains(variable);
    public void ParseDeclaration() => throw new NotImplementedException();
    /// <summary>
    /// Starts the parsing of sequential statements.
    /// </summary>
    public void Parse(){
        _logger.LogInformation("Parsing data structure");
        ParseBodyDeclaration();
        _logger.LogInformation("Data structure parsed");
    }
    /// <summary>
    /// Parses the body of a block, delegating to specific methods based on the statement type (if, case, etc.).
    /// </summary>
    public void ParseBodyDeclaration(){
        switch(_streamer.Current?.Value.ToLower()){
            case "if":
                ParseIfStructure();
                ParseBodyDeclaration();
            break;
            case "case":
                ParseCaseStructure();
                ParseBodyDeclaration();
            break;
            case "for":
                ParseForStructure();
                ParseBodyDeclaration();
            break;
            case "while":
            case "exit":
            case "wait":
            case "null":
                throw new NotImplementedException();
            case "return":
                if(!_isFunction){
                    throw new ParserException(SYNTAX_ERROR);
                }else{
                    if(String.IsNullOrEmpty(_implicitReturnVariable) || ExistsVariable(_implicitReturnVariable))
                        throw new ParserException(MISSING_DECLARATION);
                    _streamer.MoveNext();
                    _streamer.Expect(IDENTIFIERS);
                    _streamer.Expect(OPERATORS); // ;
                }
            break;
            default:
                if(_streamer.Current?.Type == IDENTIFIERS){
                    ParseAsignations();
                    ParseBodyDeclaration();
                }
            break;
        }
    }
    /// <summary>
    /// Parses a 'for-loop' structure.
    /// </summary>
    public void ParseForStructure(){
        _streamer.Expect(KEYWORDS); // FOR
        string variable = _streamer.Current?.Value ?? string.Empty;
        if(ExistsVariable(variable))
            throw new ParserException(DUPLICATE_DECLARATION);
        _scopes[_name].Add(variable);
        _streamer.Expect(IDENTIFIERS); // ITERATOR
        _streamer.Expect(KEYWORDS);
        ParseForDeclaration();
        _streamer.Expect(KEYWORDS); // loop
        ParseBodyDeclaration();
        _streamer.Expect(KEYWORDS); // end;
        _streamer.Expect(KEYWORDS); // loop
    }
    /// <summary>
    /// Parses the declaration part of a for-loop (e.g., 'i in 0 to 7').
    /// </summary>
    public void ParseForDeclaration(){
        while(_streamer.Current?.Value != "loop" || _streamer.Current?.Type != PARENTHESIS){
            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
                if((_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";") || _streamer.Current?.Type == KEYWORDS){
                    _streamer.MoveNext();
                    ParseOperation();
                }
            }
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "not")
                _streamer.MoveNext();
            if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                VectorParser parser = new(_streamer, _scopes, _name);
                parser.ParseAttributes();
            }
            else if(_streamer.Current?.Type == IDENTIFIERS)
                if(!ExistsVariable(_streamer.Current.Value))
                    if(_isFunction && !_implicitDeclaration){
                        _implicitDeclaration = true;
                        string variable = _streamer.Current?.Value ?? string.Empty;
                        _streamer.Expect(IDENTIFIERS);
                        _implicitReturnVariable = variable;
                        _scopes[_name].Add(variable);
                    }
                    else
                        throw new ParserException(MISSING_DECLARATION);
                else
                    _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";")
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() != "loop")
                if(_streamer.Current?.Value.ToLower() == "after"){
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
    /// Parses a 'process' block, including its sensitivity list and body.
    /// </summary>
    public void ParseProcess(){
        _logger.LogInformation("Parsing process");
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(PARENTHESIS);
        ParseProcessVariables();
        _streamer.Expect(PARENTHESIS);
        _streamer.Expect(KEYWORDS); // begin
        ParseBodyDeclaration();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // process
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("Process parsed");
    }
    /// <summary>
    /// Parses the sensitivity list of a process.
    /// </summary>
    private void ParseProcessVariables(){
        if(_streamer.Current?.Type == PARENTHESIS) return; // NOTHING INTO PARENTEHISIS FROM THE PROCESS
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!ExistsVariable(_streamer.Current?.Value ?? string.Empty))
                throw new ParserException(MISSING_DECLARATION);
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current?.Type == SEPARATORS)
                _streamer.MoveNext();
                
        }
    }
    /// <summary>
    /// Parses an 'if-then-elsif-else' structure.
    /// </summary>
    private void ParseIfStructure(){
        _logger.LogInformation("Parsing If structure");
        _streamer.Expect(KEYWORDS);
        ParseConditionalOperation();
        _streamer.Expect(KEYWORDS); // then
        ParseBodyDeclaration();
        if(_streamer.Current?.Type == KEYWORDS){
            string value = _streamer.Current.Value;
            _streamer.Expect(KEYWORDS);
            if(value == "else")
                ParseBodyDeclaration();
            else if(value == "elif")
                ParseIfStructure();
        }
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // if
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("If structure parsed");
    }
    /// <summary>
    /// Parses the conditional expression of an 'if' or 'elsif' statement.
    /// </summary>
    private void ParseConditionalOperation(){
        while(_streamer.Current?.Value != "then"){
            var type = _streamer.Current?.Type ?? Constants.TYPE.UNKNOWN;
            var val = _streamer.Current?.Value ?? string.Empty;
            if(type == PARENTHESIS && val.Equals("(")){
                _streamer.MoveNext();
                ParseConditionalOperation();
            }else if(type == PARENTHESIS && val.Equals(")")){
                // NEEDS TO CORRECT THIS ONLY THROGHT PASS THE METHODS                
                _streamer.MoveNext();
                break;
            }
            switch(type){
                case KEYWORDS:
                    switch(val){
                        case "rising_edge":
                        case "falling_edge":
                            _streamer.MoveNext();
                            ParseConditionalOperation();
                        break;
                        case "and":
                        case "or":
                        case "xor":
                            throw new NotImplementedException();
                        case "others":
                            _streamer.Expect(KEYWORDS);
                            _streamer.Expect(OPERATORS);
                            if(_streamer.Current?.Type == LITERALS ||
                            (_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value)))
                                _streamer.MoveNext();
                            else
                                throw new ParserException(SYNTAX_ERROR);
                        break;
                        default:
                            throw new ParserException(SYNTAX_ERROR);
                    }
                break;
                case IDENTIFIERS:
                    if(!ExistsVariable(_streamer.Current?.Value ?? string.Empty))
                        throw new ParserException(MISSING_DECLARATION);
                    _streamer.Expect(IDENTIFIERS);
                    if(_streamer.Current?.Type == SEPARATORS &&
                    _streamer.Current?.Value == "=>" || _streamer.Current?.Type == OPERATORS)
                        _streamer.MoveNext();
                    else if(_streamer.Current?.Type == PARENTHESIS)
                        ParseConditionalOperation();
                    else
                        throw new ParserException(SYNTAX_ERROR);
                    ParseConditionalOperation();
                break;
                case OPERATORS:
                    if(!Constants.IF_OPERATORS.Contains(_streamer.Current?.Value ?? string.Empty))
                        throw new ParserException(SYNTAX_ERROR);
                    _streamer.Expect(OPERATORS);
                break;
                case LITERALS:
                    _streamer.Expect(LITERALS);
                break;
                default:
                    if(_streamer.Current?.Value.ToLower() != "then")
                        throw new NotImplementedException();
                    break;
            } 
        }
    }
    /// <summary>
    /// Parses a 'case' statement structure.
    /// </summary>
    private void ParseCaseStructure(){
        _logger.LogInformation("Parsing Case structure");
        _streamer.Expect(KEYWORDS);
        if(!ExistsVariable(_streamer.Current?.Value ?? string.Empty))
            throw new ParserException(MISSING_DECLARATION);
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS); // is
        ParseCaseCases();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // case
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("Case structure parsed");
    }
    /// <summary>
    /// Parses the 'when' clauses within a 'case' statement.
    /// </summary>
    private void ParseCaseCases(){
        while(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "when"){
            _streamer.Expect(KEYWORDS);
            if(_streamer.Current?.Type == LITERALS || _streamer.Current?.Value.ToLower() == "others")
                _streamer.MoveNext();
            else
                throw new ParserException(SYNTAX_ERROR);
            _streamer.Expect(OPERATORS);
            ParseBodyDeclaration();
        }
    }
    /// <summary>
    /// Parses signal or variable assignment statements.
    /// </summary>
    private void ParseAsignations(){
        _logger.LogInformation("Parsing Assignations");
        while(_streamer.Current?.Type == IDENTIFIERS){
            string varName = _streamer.Current.Value;
            if(!ExistsVariable(varName))
                if(_isFunction && !_implicitDeclaration){
                    // Implicit variable return
                    _implicitDeclaration = true;
                    string variable = _streamer.Current?.Value ?? string.Empty;
                    _scopes[_name].Add(variable);
                    _implicitReturnVariable = variable;
                }else
                    throw new ParserException(MISSING_DECLARATION);
            _streamer.Expect(IDENTIFIERS);
            _streamer.Expect(OPERATORS);

            ParseOperation();
            _streamer.Expect(OPERATORS); // ; CASE 
        }
        _logger.LogInformation("Assignations parsed");
    }
    /// <summary>
    /// Parses a complex expression on the right-hand side of an assignment.
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
            if(_streamer.Current?.Type == IDENTIFIERS)
                if(!ExistsVariable(_streamer.Current.Value))
                    if(_isFunction && !_implicitDeclaration){
                        _implicitDeclaration = true;
                        string variable = _streamer.Current?.Value ?? string.Empty;
                        _streamer.Expect(IDENTIFIERS);
                        _implicitReturnVariable = variable;
                        _scopes[_name].Add(variable);
                    }
                    else
                        throw new ParserException(MISSING_DECLARATION);
                else
                    _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";")
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == KEYWORDS)
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
}