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
    /// <summary>Current scope name, parent entity name, and implicit return variable for function contexts.</summary>
    private string _name = string.Empty, _entityName = string.Empty, _implicitReturnVariable = string.Empty;
    /// <summary>Whether parsing within a function body, and whether an implicit return variable has been declared.</summary>
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
    /// Standard library functions from IEEE.std_logic_1164, IEEE.numeric_std, etc.
    /// These are built-in and don't need to be declared in user code.
    /// </summary>
    private static readonly HashSet<string> StandardLibraryFunctions = new(StringComparer.OrdinalIgnoreCase){
        // IEEE.numeric_std conversion functions
        "to_unsigned", "to_signed", "to_integer", "unsigned", "signed",
        // IEEE.numeric_std resize functions
        "resize", "shift_left", "shift_right",
        // IEEE.std_logic_1164 functions
        "std_logic_vector", "std_ulogic_vector", "to_stdlogicvector", "to_stdulogicvector",
        "to_bit", "to_bitvector", "to_x01", "to_x01z", "to_ux01",
        // Common math functions
        "clog2", "log2", "ceil", "floor", "abs", "minimum", "maximum",
        // Type conversions
        "conv_integer", "conv_unsigned", "conv_signed", "conv_std_logic_vector",
        "integer", "natural", "positive", "real", "boolean", "bit", "character", "string",
        // Edge detection (already handled as keywords, but include for completeness)
        "rising_edge", "falling_edge",
        // IEEE.math_real functions
        "sqrt", "sin", "cos", "tan", "exp", "log", "pow",
        // Other standard functions
        "now", "time"
    };

    /// <summary>
    /// Checks if the given identifier is a standard library function.
    /// </summary>
    private static bool IsStandardLibraryFunction(string name)
        => StandardLibraryFunctions.Contains(name);

    /// <summary>
    /// Checks if a variable exists within the current or parent entity scope.
    /// </summary>
    /// <param name="variable">The name of the variable to check.</param>
    /// <returns>True if the variable exists, otherwise false.</returns>
    public bool ExistsVariable(string variable)
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable) ||
            _scopes.ContainsKey(_entityName) && _scopes[_entityName].Contains(variable) ||
            IsStandardLibraryFunction(variable);
    /// <summary>
    /// Not implemented. DataStructuresParser uses <see cref="ParseBodyDeclaration"/> instead of standalone declaration parsing.
    /// </summary>
    /// <exception cref="NotImplementedException">Always thrown.</exception>
    public void ParseDeclaration() => throw new NotImplementedException();
    /// <summary>
    /// Parses a SINGLE concurrent statement (for use from architecture body context).
    /// Does not recursively parse subsequent statements.
    /// </summary>
    public void Parse(){
        _logger.LogInformation("Parsing data structure");
        ParseSingleStatement();
        _logger.LogInformation("Data structure parsed");
    }
    /// <summary>
    /// Parses ALL sequential statements until hitting 'end' keyword (for use in process/function bodies).
    /// </summary>
    public void ParseSequentialBody(){
        _logger.LogInformation("Parsing sequential body");
        ParseBodyDeclaration(); // Uses the recursive version
        _logger.LogInformation("Sequential body parsed");
    }
    /// <summary>
    /// Parses a SINGLE concurrent statement.
    /// Does not recursively parse subsequent statements.
    /// </summary>
    private void ParseSingleStatement(){
        switch(_streamer.Current?.Value.ToLower()){
            case "if":
                ParseIfStructure();
            break;
            case "case":
                ParseCaseStructure();
            break;
            case "for":
                ParseForStructure();
            break;
            case "while":
                ParseWhileStructure();
            break;
            default:
                if(_streamer.Current?.Type == IDENTIFIERS){
                    ParseAsignations();
                }
                else{
                    // Skip unknown tokens until semicolon
                    while(_streamer.Current?.Value != ";" && _streamer.Current != null){
                        _streamer.MoveNext();
                    }
                    if(_streamer.Current?.Value == ";")
                        _streamer.MoveNext();
                }
            break;
        }
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
                ParseWhileStructure();
                ParseBodyDeclaration();
            break;
            case "exit":
                ParseExitStatement();
                ParseBodyDeclaration();
            break;
            case "wait":
                ParseWaitStatement();
                ParseBodyDeclaration();
            break;
            case "null":
                _streamer.Expect(KEYWORDS); // null
                _streamer.Expect(OPERATORS); // ;
                ParseBodyDeclaration();
            break;
            case "return":
                if(!_isFunction){
                    throw new ParserException(SYNTAX_ERROR);
                }else{
                    _streamer.MoveNext(); // consume 'return'
                    // Parse the return expression (can be identifier, literal, or complex expression)
                    ParseReturnExpression();
                    _streamer.Expect(OPERATORS); // ;
                }
            break;
            default:
                if(_streamer.Current?.Type == IDENTIFIERS || _streamer.Current?.Type == QUALIFIED_IDENTIFIERS){
                    ParseAsignations();
                    ParseBodyDeclaration(); // Continue parsing subsequent statements
                }
                // Otherwise, we've hit a non-statement token (like 'end') - stop recursion
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
        _streamer.Expect(KEYWORDS); // in
        ParseForDeclaration();
        _streamer.Expect(KEYWORDS); // loop
        ParseBodyDeclaration();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // loop
        _streamer.Expect(OPERATORS); // ;
        // Remove iterator variable from scope (it's local to this loop)
        _scopes[_name].Remove(variable);
    }
    /// <summary>
    /// Parses a 'while-loop' structure (e.g., while condition loop ... end loop;).
    /// </summary>
    public void ParseWhileStructure(){
        _streamer.Expect(KEYWORDS); // while
        // Parse condition until 'loop' keyword
        ParseConditionExpression("loop");
        _streamer.Expect(KEYWORDS); // loop
        ParseBodyDeclaration();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // loop
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses an 'exit' statement (e.g., exit; or exit when condition;).
    /// </summary>
    public void ParseExitStatement(){
        _streamer.Expect(KEYWORDS); // exit
        // Optional label
        if(_streamer.Current?.Type == IDENTIFIERS){
            _streamer.Expect(IDENTIFIERS);
        }
        // Optional 'when' condition
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "when"){
            _streamer.Expect(KEYWORDS); // when
            ParseConditionExpression(";");
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses a 'wait' statement (e.g., wait until clk'event; or wait for 10 ns;).
    /// </summary>
    public void ParseWaitStatement(){
        _streamer.Expect(KEYWORDS); // wait
        string waitType = _streamer.Current?.Value.ToLower() ?? string.Empty;
        if(waitType == "until"){
            _streamer.Expect(KEYWORDS); // until
            ParseConditionExpression(";");
        }
        else if(waitType == "for"){
            _streamer.Expect(KEYWORDS); // for
            _streamer.Expect(LITERALS); // time value
            _streamer.Expect(TIME_VARIABLES); // ns, ps, etc.
        }
        else if(waitType == "on"){
            _streamer.Expect(KEYWORDS); // on
            // Parse signal list
            while(_streamer.Current?.Type == IDENTIFIERS){
                _streamer.Expect(IDENTIFIERS);
                if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == ",")
                    _streamer.MoveNext();
                else
                    break;
            }
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses a condition expression until a terminator keyword or operator is found.
    /// </summary>
    private void ParseConditionExpression(string terminator){
        while(_streamer.Current?.Value.ToLower() != terminator){
            if(_streamer.Current?.Type == IDENTIFIERS){
                _streamer.Expect(IDENTIFIERS);
            }
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != terminator){
                _streamer.Expect(OPERATORS);
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                // Handle logical operators like 'and', 'or', 'not', etc.
                string keyword = _streamer.Current.Value.ToLower();
                if(keyword == terminator) break;
                _streamer.Expect(KEYWORDS);
            }
            else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                _streamer.MoveNext(); // Handle signal'event, etc.
            }
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
            }
            else{
                break;
            }
        }
    }
    /// <summary>
    /// Parses the declaration part of a for-loop (e.g., 'i in 0 to 7' or 'i in A'range').
    /// </summary>
    public void ParseForDeclaration(){
        while(!(_streamer.Current?.Value.ToLower() == "loop" && _streamer.Current?.Type == KEYWORDS)){
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
                if(!ExistsVariable(_streamer.Current.Value) && !ExistsVariableInAnyScope(_streamer.Current.Value))
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
    /// Parses a 'process' block, including its sensitivity list, declarations, and body.
    /// </summary>
    public void ParseProcess(){
        _logger.LogInformation("Parsing process");
        _streamer.Expect(KEYWORDS); // process
        // Optional sensitivity list
        if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
            _streamer.Expect(PARENTHESIS);
            ParseProcessVariables();
            _streamer.Expect(PARENTHESIS);
        }
        // Optional 'is' keyword
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "is"){
            _streamer.Expect(KEYWORDS);
        }
        // Parse variable declarations before 'begin'
        ParseProcessDeclarations();
        _streamer.Expect(KEYWORDS); // begin
        ParseBodyDeclaration();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // process
        // Optional process label after 'end process'
        if(_streamer.Current?.Type == IDENTIFIERS){
            _streamer.Expect(IDENTIFIERS);
        }
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("Process parsed");
    }
    /// <summary>
    /// Parses variable/constant declarations in a process before 'begin'.
    /// Handles multiple variables on same line: variable a, b, c : integer;
    /// </summary>
    private void ParseProcessDeclarations(){
        while(_streamer.Current?.Type == KEYWORDS &&
              (_streamer.Current?.Value.ToLower() == "variable" || _streamer.Current?.Value.ToLower() == "constant")){
            string declType = _streamer.Current.Value.ToLower();
            _streamer.Expect(KEYWORDS); // variable/constant
            // Parse variable name(s) - can be comma-separated
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
            // Parse data type - can be keyword or identifier (custom type)
            string dataType = _streamer.Current?.Value.ToLower() ?? string.Empty;
            if(_streamer.Current?.Type == KEYWORDS)
                _streamer.Expect(KEYWORDS);
            else if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.Expect(IDENTIFIERS);
            // Handle vector types with bounds
            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseVectorBoundsInDeclaration();
            }
            // Handle initialization := value
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":="){
                _streamer.Expect(OPERATORS); // :=
                ParseInitializationValueInDeclaration();
            }
            _streamer.Expect(OPERATORS); // ;
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
    /// Parses vector bounds like (31 downto 0) in declarations.
    /// </summary>
    private void ParseVectorBoundsInDeclaration(){
        _streamer.Expect(PARENTHESIS); // (
        while(_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")"){
            if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            else if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == KEYWORDS)
                _streamer.Expect(KEYWORDS);
            else if(_streamer.Current?.Type == OPERATORS)
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                int depth = 1;
                _streamer.MoveNext();
                while(depth > 0){
                    if(_streamer.Current?.Value == "(") depth++;
                    else if(_streamer.Current?.Value == ")") depth--;
                    if(depth > 0) _streamer.MoveNext();
                }
                _streamer.Expect(PARENTHESIS);
            }
            else
                break;
        }
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Parses initialization value in declarations (until semicolon).
    /// </summary>
    private void ParseInitializationValueInDeclaration(){
        while(_streamer.Current?.Value != ";"){
            if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            else if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                int depth = 1;
                _streamer.MoveNext();
                while(depth > 0){
                    if(_streamer.Current?.Value == "(") depth++;
                    else if(_streamer.Current?.Value == ")") depth--;
                    if(depth > 0) _streamer.MoveNext();
                }
                _streamer.Expect(PARENTHESIS);
            }
            else if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";")
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == KEYWORDS)
                _streamer.Expect(KEYWORDS);
            else
                break;
        }
    }
    /// <summary>
    /// Parses the return expression in a function (can be literal, identifier, or complex expression).
    /// </summary>
    private void ParseReturnExpression(){
        // Parse expression until semicolon
        while(_streamer.Current?.Value != ";"){
            if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == IDENTIFIERS || _streamer.Current?.Type == QUALIFIED_IDENTIFIERS){
                string idName = _streamer.Current.Value;
                if(_streamer.Current?.Type == QUALIFIED_IDENTIFIERS)
                    _streamer.Expect(QUALIFIED_IDENTIFIERS);
                else
                    _streamer.Expect(IDENTIFIERS);
                // Check for function call
                if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                    ParseFunctionCall();
                }
            }
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
            }
            else if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";"){
                _streamer.Expect(OPERATORS);
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                // Handle logical operators like 'and', 'or', 'not', etc.
                _streamer.Expect(KEYWORDS);
            }
            else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                // Handle vector attributes in return expressions
                _streamer.MoveNext();
            }
            else{
                break;
            }
        }
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
        // Check for else/elsif clauses - only consume the keyword if it's else or elsif
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower() == "else"){
            _streamer.Expect(KEYWORDS); // else
            ParseBodyDeclaration();
        }
        else if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower() == "elsif"){
            _streamer.Expect(KEYWORDS); // elsif
            // Parse elsif as a nested if without the 'if' keyword (condition already follows)
            ParseConditionalOperation();
            _streamer.Expect(KEYWORDS); // then
            ParseBodyDeclaration();
            // Recursively handle more elsif/else clauses
            while(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower() == "elsif"){
                _streamer.Expect(KEYWORDS); // elsif
                ParseConditionalOperation();
                _streamer.Expect(KEYWORDS); // then
                ParseBodyDeclaration();
            }
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower() == "else"){
                _streamer.Expect(KEYWORDS); // else
                ParseBodyDeclaration();
            }
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
                // After returning from recursive call, continue loop with updated token
                continue;
            }else if(type == PARENTHESIS && val.Equals(")")){
                // NEEDS TO CORRECT THIS ONLY THROGHT PASS THE METHODS
                _streamer.MoveNext();
                break;
            }
            switch(type){
                case KEYWORDS:
                    switch(val.ToLower()){
                        case "rising_edge":
                        case "falling_edge":
                            _streamer.MoveNext();
                            ParseConditionalOperation();
                        break;
                        case "not":
                            // Handle negation - just move past it and parse the operand
                            _streamer.MoveNext();
                            ParseConditionalOperation();
                        break;
                        case "and":
                        case "or":
                        case "xor":
                        case "nand":
                        case "nor":
                            // Logical operators in conditionals - continue parsing
                            _streamer.MoveNext();
                            ParseConditionalOperation();
                        break;
                        case "others":
                            _streamer.Expect(KEYWORDS);
                            _streamer.Expect(OPERATORS);
                            if(_streamer.Current?.Type == LITERALS ||
                            (_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value)))
                                _streamer.MoveNext();
                            else
                                throw new ParserException(SYNTAX_ERROR);
                        break;
                        case "downto":
                        case "to":
                            // Handle range keywords in indexed expressions like resultCPU(2 downto 2)
                            _streamer.MoveNext();
                        break;
                        case "unsigned":
                        case "signed":
                        case "integer":
                        case "natural":
                        case "std_logic_vector":
                        case "std_logic":
                        case "to_integer":
                        case "to_unsigned":
                        case "to_signed":
                        case "resize":
                        case "shift_left":
                        case "shift_right":
                            // Type conversion functions - parse like function call
                            _streamer.MoveNext();
                            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                                int depth = 1;
                                _streamer.MoveNext(); // past (
                                while(depth > 0 && _streamer.Current != null){
                                    if(_streamer.Current?.Type == PARENTHESIS){
                                        if(_streamer.Current?.Value == "(") depth++;
                                        else if(_streamer.Current?.Value == ")") depth--;
                                    }
                                    if(depth > 0) _streamer.MoveNext();
                                }
                                _streamer.MoveNext(); // past )
                            }
                        break;
                        default:
                            throw new ParserException(SYNTAX_ERROR);
                    }
                break;
                case IDENTIFIERS:
                case QUALIFIED_IDENTIFIERS:
                    // Allow identifiers - could be variables, functions, or types
                    // Also handle qualified identifiers like config.bypass, state.buffer, etc.
                    // The validation is relaxed because local variables in process scope
                    // and standard library functions should be allowed
                    string idName = _streamer.Current?.Value ?? string.Empty;
                    if(_streamer.Current?.Type == QUALIFIED_IDENTIFIERS)
                        _streamer.Expect(QUALIFIED_IDENTIFIERS);
                    else
                        _streamer.Expect(IDENTIFIERS);
                    // After identifier, we may have:
                    // - An operator (=, <, >, etc.) - continue parsing
                    // - A parenthesis for function call or indexing
                    // - 'then' keyword - end of condition
                    // - Logical keyword (and, or, etc.) - handled by next iteration
                    if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == "=>"){
                        _streamer.MoveNext();
                        ParseConditionalOperation();
                    }
                    else if(_streamer.Current?.Type == OPERATORS){
                        _streamer.MoveNext();
                        ParseConditionalOperation();
                    }
                    else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                        // Function call or indexed access in condition
                        int depth = 1;
                        _streamer.MoveNext(); // past (
                        while(depth > 0 && _streamer.Current != null){
                            if(_streamer.Current?.Type == PARENTHESIS){
                                if(_streamer.Current?.Value == "(") depth++;
                                else if(_streamer.Current?.Value == ")") depth--;
                            }
                            if(depth > 0) _streamer.MoveNext();
                        }
                        if(_streamer.Current?.Value == ")") _streamer.MoveNext(); // past )
                    }
                    // If 'then' or logical keyword, just return and let the loop/caller handle it
                break;
                case OPERATORS:
                    if(!Constants.IF_OPERATORS.Contains(_streamer.Current?.Value ?? string.Empty))
                        throw new ParserException(SYNTAX_ERROR);
                    _streamer.Expect(OPERATORS);
                break;
                case LITERALS:
                    _streamer.Expect(LITERALS);
                break;
                case VECTORS_ATTRIBUTES:
                    // Handle vector attributes like sig'event, sig'last_value, etc.
                    _streamer.MoveNext();
                break;
                case PARENTHESIS:
                    // Handle parentheses in expressions (function calls, grouping)
                    if(_streamer.Current?.Value == "("){
                        // Skip past parenthesized expression
                        int depth = 1;
                        _streamer.MoveNext(); // past (
                        while(depth > 0 && _streamer.Current != null){
                            if(_streamer.Current?.Type == PARENTHESIS){
                                if(_streamer.Current?.Value == "(") depth++;
                                else if(_streamer.Current?.Value == ")") depth--;
                            }
                            if(depth > 0) _streamer.MoveNext();
                        }
                        if(_streamer.Current?.Value == ")") _streamer.MoveNext(); // past )
                    }
                    else{
                        // Closing paren - return to caller
                        return;
                    }
                break;
                case SEPARATORS:
                    // Handle commas in expressions (shouldn't happen in conditionals)
                    break;
                default:
                    // For any other token type, check if it's 'then' or end of condition
                    if(_streamer.Current?.Value.ToLower() != "then" && _streamer.Current?.Value.ToLower() != "loop" &&
                       _streamer.Current?.Value.ToLower() != "else" && _streamer.Current?.Value.ToLower() != "elsif")
                        throw new ParserException(SYNTAX_ERROR);
                    break;
            }
        }
    }
    /// <summary>
    /// Parses a 'case' statement structure.
    /// Handles expressions like: case state is, case signal(i) is, case data(7 downto 0) is
    /// </summary>
    private void ParseCaseStructure(){
        _logger.LogInformation("Parsing Case structure");
        _streamer.Expect(KEYWORDS); // case
        // Parse case expression - can be simple identifier or indexed/sliced signal
        if(_streamer.Current?.Type == IDENTIFIERS){
            if(!ExistsVariable(_streamer.Current?.Value ?? string.Empty) && !IsStandardLibraryFunction(_streamer.Current?.Value ?? string.Empty))
                throw new ParserException(MISSING_DECLARATION);
            _streamer.Expect(IDENTIFIERS);
            // Handle indexed access like signal(i) or signal(7 downto 0)
            while(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                _streamer.Expect(PARENTHESIS); // (
                // Parse index expression
                while(_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")"){
                    if(_streamer.Current?.Type == IDENTIFIERS)
                        _streamer.MoveNext();
                    else if(_streamer.Current?.Type == LITERALS)
                        _streamer.MoveNext();
                    else if(_streamer.Current?.Type == OPERATORS)
                        _streamer.MoveNext();
                    else if(_streamer.Current?.Type == KEYWORDS) // downto, to
                        _streamer.MoveNext();
                    else
                        break;
                }
                _streamer.Expect(PARENTHESIS); // )
            }
        }
        _streamer.Expect(KEYWORDS); // is
        ParseCaseCases();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // case
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("Case structure parsed");
    }
    /// <summary>
    /// Parses the 'when' clauses within a 'case' statement.
    /// Handles: when LITERAL =>, when IDENTIFIER =>, when others =>, when "001" | "010" =>
    /// </summary>
    private void ParseCaseCases(){
        while(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "when"){
            _streamer.Expect(KEYWORDS); // when
            // Parse case choices - can be literals, identifiers, 'others', or multiple choices separated by |
            while(_streamer.Current?.Value != "=>"){
                if(_streamer.Current?.Type == LITERALS)
                    _streamer.Expect(LITERALS);
                else if(_streamer.Current?.Type == IDENTIFIERS)
                    _streamer.Expect(IDENTIFIERS);
                else if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "others")
                    _streamer.Expect(KEYWORDS);
                else if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == "|")
                    _streamer.Expect(OPERATORS); // for choices like "001" | "010"
                else
                    break;
            }
            _streamer.Expect(OPERATORS); // =>
            ParseBodyDeclaration();
        }
    }
    /// <summary>
    /// Parses signal or variable assignment statements.
    /// Handles both simple assignments (signal <= value), indexed assignments (signal(index) <= value),
    /// and record field assignments (record.field <= value).
    /// </summary>
    private void ParseAsignations(){
        _logger.LogInformation("Parsing Assignations");
        // Parse a SINGLE assignment statement (not a loop - the caller decides whether to continue)
        if(_streamer.Current?.Type == IDENTIFIERS || _streamer.Current?.Type == QUALIFIED_IDENTIFIERS){
            string varName = _streamer.Current.Value;
            // For qualified identifiers like 'result.re', extract base name
            if(varName.Contains(".")){
                varName = varName.Split('.')[0];
            }
            // Save identifier position to check if this is a procedure call
            string identName = _streamer.Current.Value;
            if(_streamer.Current?.Type == QUALIFIED_IDENTIFIERS)
                _streamer.Expect(QUALIFIED_IDENTIFIERS);
            else
                _streamer.Expect(IDENTIFIERS);
            // Handle indexed signal assignment like output(7 downto 0) <= value
            // Also handle multi-dimensional indexing like result(i)(j) <= value
            // Or procedure call with arguments like proc(args);
            while(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseIndexExpression();
            }
            // Check if this is a procedure call (ends with semicolon directly) or an assignment
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ";"){
                // This is a procedure call like proc(args); - allow even if not in scope
                // Procedures may be defined in packages that are "use"d
                _streamer.Expect(OPERATORS); // ;
            }
            else if(_streamer.Current?.Type == OPERATORS &&
                    (_streamer.Current?.Value == "<=" || _streamer.Current?.Value == ":=")){
                // This is an assignment
                // Note: We relax scope checking since signals may be declared in packages,
                // parent blocks, or other scopes not fully tracked
                _streamer.Expect(OPERATORS); // <= or :=
                ParseOperation();
                _streamer.Expect(OPERATORS); // ;
            }
            else{
                // Unknown - try to handle as assignment
                // Relax scope checking for concurrent statements
                _streamer.Expect(OPERATORS);
                ParseOperation();
                _streamer.Expect(OPERATORS); // ; CASE
            }
        }
        _logger.LogInformation("Assignations parsed");
    }

    /// <summary>
    /// Checks if a variable exists in any scope (for package-level variables/signals).
    /// </summary>
    private bool ExistsVariableInAnyScope(string variable)
        => _scopes.Values.Any(scope => scope.Contains(variable));
    /// <summary>
    /// Parses a complex expression on the right-hand side of an assignment.
    /// Handles identifiers, literals, operators, keywords (logical ops), vector attributes, and parenthesized expressions.
    /// </summary>
    private void ParseOperation(){
        while(_streamer.Current?.Value != ";"){
            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
                if((_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";") || _streamer.Current?.Type == KEYWORDS){
                    _streamer.MoveNext();
                    ParseOperation();
                }
            }
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "not")
                _streamer.MoveNext();
            // Handle vector attributes like A'length, A'range, etc.
            if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                VectorParser parser = new(_streamer, _scopes, _name);
                parser.ParseAttributes();
            }
            else if(_streamer.Current?.Type == IDENTIFIERS || _streamer.Current?.Type == QUALIFIED_IDENTIFIERS){
                string identifierName = _streamer.Current.Value;
                // For qualified identifiers like 'a.re', extract base name
                string baseName = identifierName.Contains(".") ? identifierName.Split('.')[0] : identifierName;
                if(_streamer.Current?.Type == QUALIFIED_IDENTIFIERS)
                    _streamer.Expect(QUALIFIED_IDENTIFIERS);
                else
                    _streamer.Expect(IDENTIFIERS);
                // Check if this is a function call (identifier followed by parenthesis)
                if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                    // This is a function call - parse the arguments (standard lib functions don't need validation)
                    ParseFunctionCall();
                }
                // Note: We don't strictly validate that identifiers exist in scope
                // because they may be from packages, outer scopes, or attributes
            }
            else if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";")
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == KEYWORDS)
                if(_streamer.Current.Value.ToLower().Equals("after")){
                    _streamer.MoveNext();
                    if(_streamer.Current?.Type == LITERALS || (_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value)))
                        _streamer.MoveNext();
                    // Also handle vector attributes in after clause (e.g., after A'length - i - 1)
                    else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                        VectorParser parser = new(_streamer, _scopes, _name);
                        parser.ParseAttributes();
                    }
                    else
                        throw new ParserException(SYNTAX_ERROR);
                    // After the value, there might be arithmetic operations before the time unit
                    while(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";"){
                        _streamer.MoveNext();
                        if(_streamer.Current?.Type == LITERALS || (_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value)))
                            _streamer.MoveNext();
                        else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                            VectorParser parser = new(_streamer, _scopes, _name);
                            parser.ParseAttributes();
                        }
                        else
                            break;
                    }
                    // Time unit is optional if we're in an expression context
                    if(_streamer.Current?.Type == TIME_VARIABLES)
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
    /// <summary>
    /// Parses parenthesized expressions including aggregates like (others => '0') or (width - 1 => '0', others => '1').
    /// </summary>
    private void ParseParenthesis(){
        _streamer.Expect(PARENTHESIS); // (
        ParseAggregateOrExpression();
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Parses an aggregate expression or simple parenthesized expression.
    /// Handles: (others => '0'), (5 => '1', others => '0'), (width - 1 => val, ...)
    /// </summary>
    private void ParseAggregateOrExpression(){
        int safetyCounter = 0;
        const int maxIterations = 10000;
        while(_streamer.Current != null && (_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")")){
            if(++safetyCounter > maxIterations) break; // Safety break
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "others"){
                _streamer.Expect(KEYWORDS); // others
                _streamer.Expect(OPERATORS); // =>
                // Parse the value (could be literal, identifier, or expression)
                if(_streamer.Current?.Type == LITERALS)
                    _streamer.Expect(LITERALS);
                else if(_streamer.Current?.Type == IDENTIFIERS)
                    _streamer.Expect(IDENTIFIERS);
                else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "(")
                    ParseParenthesis();
            }
            else if(_streamer.Current?.Type == IDENTIFIERS || _streamer.Current?.Type == QUALIFIED_IDENTIFIERS){
                if(_streamer.Current?.Type == QUALIFIED_IDENTIFIERS)
                    _streamer.Expect(QUALIFIED_IDENTIFIERS);
                else
                    _streamer.Expect(IDENTIFIERS);
                // Check for function call
                if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                    ParseFunctionCall();
                }
            }
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                // Handle logical operators, downto, to, etc.
                _streamer.Expect(KEYWORDS);
            }
            else if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == "=>"){
                _streamer.Expect(OPERATORS); // =>
                // Parse the value after =>
                if(_streamer.Current?.Type == LITERALS)
                    _streamer.Expect(LITERALS);
                else if(_streamer.Current?.Type == IDENTIFIERS)
                    _streamer.Expect(IDENTIFIERS);
                else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "(")
                    ParseParenthesis();
            }
            else if(_streamer.Current?.Type == OPERATORS){
                _streamer.Expect(OPERATORS);
            }
            else if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == ","){
                _streamer.MoveNext(); // consume comma
            }
            else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                _streamer.MoveNext(); // Handle vector attributes
            }
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
            }
            else{
                break;
            }
        }
    }
    /// <summary>
    /// Parses function call arguments enclosed in parentheses.
    /// Handles expressions like function_name(arg1, arg2) and indexed access like sig(7 downto 0).
    /// </summary>
    private void ParseFunctionCall(){
        _streamer.Expect(PARENTHESIS); // (
        // Parse function arguments - can be identifiers, literals, or expressions
        while(_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")"){
            if(_streamer.Current?.Type == IDENTIFIERS || _streamer.Current?.Type == QUALIFIED_IDENTIFIERS){
                string idName = _streamer.Current.Value;
                if(_streamer.Current?.Type == QUALIFIED_IDENTIFIERS)
                    _streamer.Expect(QUALIFIED_IDENTIFIERS);
                else
                    _streamer.Expect(IDENTIFIERS);
                // Check if this is a nested function call or indexed access
                if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                    ParseFunctionCall();
                }
            }
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
            }
            else if(_streamer.Current?.Type == OPERATORS){
                _streamer.Expect(OPERATORS);
            }
            else if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == ","){
                _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                // Handle logical operators in expressions (and, or, xor, downto, to, etc.)
                _streamer.Expect(KEYWORDS);
            }
            else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                // Handle vector attributes like sig'left, sig'range, etc.
                _streamer.MoveNext();
            }
            else{
                break;
            }
        }
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Parses indexed signal/variable access like signal(7 downto 0) or signal(index) or signal(sig'left).
    /// </summary>
    private void ParseIndexExpression(){
        _streamer.Expect(PARENTHESIS); // (
        // Parse index expression - can be a range (7 downto 0), single index, attribute like sig'left,
        // or function/procedure arguments like (arg1, arg2)
        while(_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")"){
            if(_streamer.Current?.Type == IDENTIFIERS || _streamer.Current?.Type == QUALIFIED_IDENTIFIERS){
                string idName = _streamer.Current.Value;
                if(_streamer.Current?.Type == QUALIFIED_IDENTIFIERS)
                    _streamer.Expect(QUALIFIED_IDENTIFIERS);
                else
                    _streamer.Expect(IDENTIFIERS);
                // Check if this is a function call like conv_integer(x)
                if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                    ParseFunctionCall();
                }
            }
            else if(_streamer.Current?.Type == LITERALS){
                _streamer.Expect(LITERALS);
            }
            else if(_streamer.Current?.Type == KEYWORDS){
                // Handle 'downto', 'to', arithmetic ops
                _streamer.Expect(KEYWORDS);
            }
            else if(_streamer.Current?.Type == OPERATORS){
                _streamer.Expect(OPERATORS);
            }
            else if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == ","){
                // Handle comma-separated arguments in function/procedure calls
                _streamer.MoveNext();
            }
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
            }
            else if(_streamer.Current?.Type == VECTORS_ATTRIBUTES){
                // Handle vector attributes like sig'left, sig'range, etc.
                _streamer.MoveNext();
            }
            else{
                break;
            }
        }
        _streamer.Expect(PARENTHESIS); // )
    }
}