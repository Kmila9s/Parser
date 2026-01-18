using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;
namespace Parser.Repositories;

/// <summary>
/// A parser for VHDL 'package' and 'package body' blocks.
/// </summary>
public class PackageParser : IParseStructures{
    private TokenStream _streamer;
    private readonly ILogger<ArchitectureParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ArchitectureParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private string _name = string.Empty, _entityName = string.Empty;
    private FunctionParser _functionParser = default!;
    private GenericParser _genericParser = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageParser"/> class.
    /// </summary>
    /// <param name="streamer">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    public PackageParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes){
        _scopes = scopes;
        _streamer = streamer;
    }
    /// <summary>
    /// Gets the name of the parsed package.
    /// </summary>
    /// <returns>The package name.</returns>
    public string GetName() => _name;
    /// <summary>
    /// Gets the dictionary of scopes.
    /// </summary>
    /// <returns>The dictionary of scopes.</returns>
    public Dictionary<string, HashSet<Variable>> GetScopes() => _scopes;
    /// <summary>
    /// Checks if a variable exists within the current package's scope.
    /// </summary>
    /// <param name="variable">The name of the variable to check.</param>
    /// <returns>True if the variable exists, otherwise false.</returns>
    public bool ExistsVariable(string variable)
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    /// <summary>
    /// Parses the declarations within a package (e.g., functions, generics, constants).
    /// </summary>
    public void ParseDeclaration(){
        while(_streamer.Current?.Value.ToLower() != "end"){
            switch(_streamer.Current?.Value.ToLower()){
                case "function":
                    _functionParser = new(_streamer, _scopes, _name);
                    _functionParser.Parse();
                break;
                case "generic":
                    _genericParser = new(_streamer, _scopes, _name);
                    _genericParser.Parse();
                break;
                case "end":
                    // Default case
                break;
                case "constant":
                    ParseConstantDeclaration();
                break;
                case "type":
                    ParseTypeDeclaration();
                break;
                case "subtype":
                    ParseSubtypeDeclaration();
                break;
                case "procedure":
                    ParseProcedureDeclaration();
                break;
                case "signal":
                    ParseSignalDeclaration();
                break;
                default:
                    throw new ParserException(SYNTAX_ERROR);
            }
        }
    }
    /// <summary>
    /// Parses a constant declaration in a package.
    /// </summary>
    private void ParseConstantDeclaration(){
        _streamer.Expect(KEYWORDS); // constant
        string constName = _streamer.Current?.Value ?? string.Empty;
        _scopes[_name].Add(constName);
        _streamer.Expect(IDENTIFIERS); // constant name
        _streamer.Expect(OPERATORS); // :
        // Parse type - could be simple type or vector type with bounds
        ParseTypeSpecification();
        // Optional initialization
        if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":="){
            _streamer.Expect(OPERATORS); // :=
            ParseExpressionUntilSemicolon();
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses a signal declaration in a package.
    /// </summary>
    private void ParseSignalDeclaration(){
        _streamer.Expect(KEYWORDS); // signal
        string sigName = _streamer.Current?.Value ?? string.Empty;
        _scopes[_name].Add(sigName);
        _streamer.Expect(IDENTIFIERS); // signal name
        _streamer.Expect(OPERATORS); // :
        ParseTypeSpecification();
        if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":="){
            _streamer.Expect(OPERATORS); // :=
            ParseExpressionUntilSemicolon();
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses a type declaration (enumeration, record, array, etc.).
    /// </summary>
    private void ParseTypeDeclaration(){
        _streamer.Expect(KEYWORDS); // type
        string typeName = _streamer.Current?.Value ?? string.Empty;
        _scopes[_name].Add(typeName);
        _streamer.Expect(IDENTIFIERS); // type name
        _streamer.Expect(KEYWORDS); // is
        // Determine what kind of type this is
        string typeKind = _streamer.Current?.Value.ToLower() ?? string.Empty;
        if(typeKind == "record"){
            ParseRecordType();
        }
        else if(typeKind == "array"){
            ParseArrayType();
        }
        else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
            // Enumeration type: type state_t is (IDLE, RUN, STOP);
            ParseEnumerationType();
        }
        else{
            // Other types - skip until semicolon
            while(_streamer.Current?.Value != ";"){
                _streamer.MoveNext();
            }
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses a record type definition.
    /// </summary>
    private void ParseRecordType(){
        _streamer.Expect(KEYWORDS); // record
        while(_streamer.Current?.Value.ToLower() != "end"){
            // Parse each record field - field names can be keywords like 'buffer', 'range', etc.
            string fieldName = _streamer.Current?.Value ?? string.Empty;
            // Accept both identifiers and keywords as field names
            if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == KEYWORDS)
                _streamer.Expect(KEYWORDS);
            _streamer.Expect(OPERATORS); // :
            ParseTypeSpecification();
            _streamer.Expect(OPERATORS); // ;
        }
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // record
        // Optional type name after 'end record'
        if(_streamer.Current?.Type == IDENTIFIERS){
            _streamer.Expect(IDENTIFIERS);
        }
    }
    /// <summary>
    /// Parses an array type definition.
    /// </summary>
    private void ParseArrayType(){
        _streamer.Expect(KEYWORDS); // array
        _streamer.Expect(PARENTHESIS); // (
        // Parse array bounds - can be complex (natural range <>, 0 to MAX, etc.)
        int depth = 1;
        while(depth > 0){
            if(_streamer.Current?.Type == PARENTHESIS){
                if(_streamer.Current?.Value == "(") depth++;
                else if(_streamer.Current?.Value == ")") depth--;
                if(depth == 0) break;
            }
            _streamer.MoveNext();
        }
        _streamer.Expect(PARENTHESIS); // )
        _streamer.Expect(KEYWORDS); // of
        // Parse element type
        ParseTypeSpecification();
    }
    /// <summary>
    /// Parses an enumeration type definition.
    /// </summary>
    private void ParseEnumerationType(){
        _streamer.Expect(PARENTHESIS); // (
        while(_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")"){
            if(_streamer.Current?.Type == IDENTIFIERS){
                // Add enumeration literal to scope
                _scopes[_name].Add(_streamer.Current.Value);
                _streamer.Expect(IDENTIFIERS);
            }
            else if(_streamer.Current?.Type == SEPARATORS && _streamer.Current?.Value == ","){
                _streamer.MoveNext();
            }
            else{
                break;
            }
        }
        _streamer.Expect(PARENTHESIS); // )
    }
    /// <summary>
    /// Parses a subtype declaration.
    /// Format: subtype name is [resolution_function] base_type [constraint];
    /// </summary>
    private void ParseSubtypeDeclaration(){
        _streamer.Expect(KEYWORDS); // subtype
        string subtypeName = _streamer.Current?.Value ?? string.Empty;
        _scopes[_name].Add(subtypeName);
        _streamer.Expect(IDENTIFIERS); // subtype name
        _streamer.Expect(KEYWORDS); // is
        // There may be a resolution function name before the type
        // e.g., "subtype resolved_logic is resolve_bus std_logic"
        // Parse until semicolon
        while(_streamer.Current?.Value != ";"){
            if(_streamer.Current?.Type == KEYWORDS)
                _streamer.MoveNext();
            else if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.MoveNext();
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                // Parse bounds or constraints in parentheses
                int depth = 1;
                _streamer.MoveNext();
                while(depth > 0){
                    if(_streamer.Current?.Type == PARENTHESIS){
                        if(_streamer.Current?.Value == "(") depth++;
                        else if(_streamer.Current?.Value == ")") depth--;
                        if(depth == 0) break;
                    }
                    _streamer.MoveNext();
                }
                _streamer.Expect(PARENTHESIS); // )
            }
            else if(_streamer.Current?.Type == OPERATORS)
                _streamer.MoveNext();
            else if(_streamer.Current?.Type == LITERALS)
                _streamer.MoveNext();
            else
                break;
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses a procedure declaration (header only in package, body in package body).
    /// </summary>
    private void ParseProcedureDeclaration(){
        _streamer.Expect(KEYWORDS); // procedure
        string procName = _streamer.Current?.Value ?? string.Empty;
        _scopes[_name].Add(procName);
        _streamer.Expect(IDENTIFIERS); // procedure name
        // Optional parameter list
        if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
            _streamer.Expect(PARENTHESIS); // (
            // Parse parameters - skip to closing )
            int depth = 1;
            while(depth > 0){
                if(_streamer.Current?.Type == PARENTHESIS){
                    if(_streamer.Current?.Value == "(") depth++;
                    else if(_streamer.Current?.Value == ")") depth--;
                    if(depth == 0) break;
                }
                _streamer.MoveNext();
            }
            _streamer.Expect(PARENTHESIS); // )
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses a type specification (type name with optional bounds/constraints).
    /// </summary>
    private void ParseTypeSpecification(){
        // Type can be: simple identifier, qualified name, or keyword (std_logic, integer, etc.)
        if(_streamer.Current?.Type == KEYWORDS || _streamer.Current?.Type == IDENTIFIERS){
            _streamer.MoveNext();
        }
        // Handle vector bounds like (7 downto 0) or (natural range <>)
        if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
            int depth = 1;
            _streamer.MoveNext();
            while(depth > 0){
                if(_streamer.Current?.Type == PARENTHESIS){
                    if(_streamer.Current?.Value == "(") depth++;
                    else if(_streamer.Current?.Value == ")") depth--;
                    if(depth == 0) break;
                }
                _streamer.MoveNext();
            }
            _streamer.Expect(PARENTHESIS); // )
        }
        // Handle 'range' constraint like integer range 0 to 255
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "range"){
            _streamer.Expect(KEYWORDS); // range
            // Parse range bounds
            while(_streamer.Current?.Value != ";" && _streamer.Current?.Value != ")" &&
                  _streamer.Current?.Value != ":=" && _streamer.Current?.Value.ToLower() != "end"){
                _streamer.MoveNext();
            }
        }
    }
    /// <summary>
    /// Parses an expression until semicolon.
    /// </summary>
    private void ParseExpressionUntilSemicolon(){
        int depth = 0;
        while(_streamer.Current?.Value != ";" || depth > 0){
            if(_streamer.Current?.Type == PARENTHESIS){
                if(_streamer.Current?.Value == "(") depth++;
                else if(_streamer.Current?.Value == ")") depth--;
            }
            _streamer.MoveNext();
        }
    }
    /// <summary>
    /// Parses the declarations within a package body.
    /// </summary>
    public void ParseBodyDeclaration(){
        while(_streamer.Current?.Value.ToLower() != "end"){
            switch(_streamer.Current?.Value.ToLower()){
                case "function":
                    _functionParser = new(_streamer, _scopes, _name);
                    _functionParser.Parse();
                break;
                case "end":
                    // Default case - will exit the while loop
                break;
                case "procedure":
                    ParseProcedureBody();
                break;
                case "constant":
                    ParseConstantDeclaration();
                break;
                case "type":
                    ParseTypeDeclaration();
                break;
                case "subtype":
                    ParseSubtypeDeclaration();
                break;
                default:
                    throw new ParserException(SYNTAX_ERROR);
            }
        }
    }
    /// <summary>
    /// Parses a procedure body (full implementation in package body).
    /// </summary>
    private void ParseProcedureBody(){
        _streamer.Expect(KEYWORDS); // procedure
        string procName = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(IDENTIFIERS); // procedure name
        // Optional parameter list
        if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
            _streamer.Expect(PARENTHESIS); // (
            int depth = 1;
            while(depth > 0){
                if(_streamer.Current?.Type == PARENTHESIS){
                    if(_streamer.Current?.Value == "(") depth++;
                    else if(_streamer.Current?.Value == ")") depth--;
                    if(depth == 0) break;
                }
                _streamer.MoveNext();
            }
            _streamer.Expect(PARENTHESIS); // )
        }
        _streamer.Expect(KEYWORDS); // is
        // Parse declarations before begin
        while(_streamer.Current?.Value.ToLower() != "begin"){
            if(_streamer.Current?.Value.ToLower() == "variable" || _streamer.Current?.Value.ToLower() == "constant"){
                _streamer.Expect(KEYWORDS); // variable/constant
                _streamer.Expect(IDENTIFIERS); // name
                _streamer.Expect(OPERATORS); // :
                ParseTypeSpecification();
                if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":="){
                    _streamer.Expect(OPERATORS);
                    ParseExpressionUntilSemicolon();
                }
                _streamer.Expect(OPERATORS); // ;
            }
            else{
                break;
            }
        }
        _streamer.Expect(KEYWORDS); // begin
        // Parse procedure body - skip to end
        ParseProcedureStatements();
        _streamer.Expect(KEYWORDS); // end
        // Optional procedure name or 'procedure' keyword after end
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "procedure"){
            _streamer.Expect(KEYWORDS); // procedure
        }
        if(_streamer.Current?.Type == IDENTIFIERS){
            _streamer.Expect(IDENTIFIERS); // procedure name
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses the statements inside a procedure body.
    /// </summary>
    private void ParseProcedureStatements(){
        while(_streamer.Current != null && _streamer.Current?.Value.ToLower() != "end"){
            string keyword = _streamer.Current?.Value.ToLower() ?? string.Empty;
            // Simple statement parsing - handle nested if/for/while/etc.
            if(keyword == "if"){
                ParseNestedBlock("if", "if");
            }
            else if(keyword == "for"){
                ParseNestedBlock("for", "loop");
            }
            else if(keyword == "while"){
                ParseNestedBlock("while", "loop");
            }
            else if(keyword == "case"){
                ParseNestedBlock("case", "case");
            }
            else{
                // Skip tokens until semicolon
                while(_streamer.Current != null && _streamer.Current?.Value != ";"){
                    _streamer.MoveNext();
                }
                if(_streamer.Current != null)
                    _streamer.Expect(OPERATORS); // ;
            }
        }
    }
    /// <summary>
    /// Parses a nested block structure (if/for/while/case) by tracking depth.
    /// </summary>
    private void ParseNestedBlock(string startKeyword, string endKeyword){
        int depth = 1;
        _streamer.MoveNext(); // past start keyword
        while(depth > 0 && _streamer.Current != null){
            string val = _streamer.Current?.Value.ToLower() ?? string.Empty;
            if(val == startKeyword){
                depth++;
                _streamer.MoveNext();
            }
            else if(val == "end"){
                _streamer.MoveNext(); // past 'end'
                if(_streamer.Current == null) break;
                string afterEnd = _streamer.Current?.Value.ToLower() ?? string.Empty;
                if(afterEnd == endKeyword || afterEnd == "loop"){
                    depth--;
                    _streamer.MoveNext(); // past end keyword (if, loop, case)
                    if(depth == 0){
                        if(_streamer.Current != null)
                            _streamer.Expect(OPERATORS); // ;
                        return;
                    }
                }
            }
            else{
                _streamer.MoveNext();
            }
        }
    }
    /// <summary>
    /// Starts the parsing of the package or package body block.
    /// </summary>
    public void Parse(){
        _logger.LogInformation("Parsing Package");
        _streamer.Expect(KEYWORDS);
        if(_streamer.Current?.Type == KEYWORDS){
            _logger.LogInformation("Parsing Body Package");
            _streamer.Expect(KEYWORDS);
            _name = _streamer.Current?.Value ?? string.Empty;
            _streamer.Expect(IDENTIFIERS);
            if(!_scopes.ContainsKey(_name))
                throw new ParserException(MISSING_STRUCTURE);
            _streamer.Expect(KEYWORDS);
            ParseBodyDeclaration();
        }else{
            _logger.LogInformation("Parsing Base Package");
            // BASE PACKAGE DECLARATION
            _name = _streamer.Current?.Value ?? string.Empty;
            _streamer.Expect(IDENTIFIERS);
            if(!_scopes.ContainsKey(_name))
                _scopes.Add(_name, new());
            else
                throw new ParserException(DUPLICATE_DECLARATION);
            _streamer.Expect(KEYWORDS);
            ParseDeclaration();
        }
        _streamer.Expect(KEYWORDS);
        string name = _streamer.Current?.Value ?? string.Empty;
        if(!_name.Equals(name))
            throw new ParserException(MISSING_STRUCTURE);
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("Package parsed");
    }
}