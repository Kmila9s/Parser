using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using Parser.Models;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;

namespace Parser.Repositories;

/// <summary>
/// A parser for VHDL 'architecture' blocks. It handles the architecture declaration, header (signals, constants), and body (processes, concurrent statements).
/// </summary>
public class ArchitectureParser : IParseStructures {
    private TokenStream _streamer;
    private readonly ILogger<ArchitectureParser> _logger = SharedLogging.Factory.CreateLogger<ArchitectureParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private ArchitectureAssignationParser _archAssignations = default!;
    private DataStructuresParser _dataStructureParser = default!;
    private FunctionParser _functionParser = default!;
    private string _name = string.Empty, _entityName = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchitectureParser"/> class.
    /// </summary>
    /// <param name="streamer">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    public ArchitectureParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes)
    {
        _streamer = streamer;
        _scopes = scopes;
    }
    /// <summary>
    /// Gets the name of the parsed architecture.
    /// </summary>
    /// <returns>The architecture name.</returns>
    public string GetName() => _name;
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
    /// Parses the architecture's declaration part, including its name and associated entity.
    /// </summary>
    public void ParseDeclaration(){
        _streamer.Expect(KEYWORDS);
        string archName = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS); // of
        string entityName = _streamer.Current?.Value ?? string.Empty;
        if(!_scopes.ContainsKey(entityName))
            throw new ParserException(MISSING_STRUCTURE);
        _entityName = entityName;
        // Use combined name for architecture scope (entity_architecture) to allow same arch name for different entities
        _name = $"{entityName}_{archName}";
        if(_scopes.ContainsKey(_name))
            throw new ParserException(DUPLICATE_STRUCTURE);
        _scopes.Add(_name, new());
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS); // is
        ParseHeader();
        _streamer.Expect(KEYWORDS); // begin
    }
    /// <summary>
    /// Parses the header of the architecture, which contains declarations like signals, constants, functions, etc.
    /// </summary>
    public void ParseHeader(){
        // Keep parsing declarations until we hit 'begin'
        while(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower() != "begin"){
            ParseBodyHeaderDeclaration();
        }
    }
    /// <summary>
    /// Delegates parsing of different declaration types found in the architecture header.
    /// </summary>
    public void ParseBodyHeaderDeclaration(){
        if(_streamer.Current?.Type == KEYWORDS){
            switch(_streamer.Current.Value.ToLower()){
                case "signal":
                case "constant":
                case "attribute":
                case "group":
                case "disconnect":{
                    _archAssignations = new(_streamer, _scopes, _name, _entityName);
                    _archAssignations.Parse();
                }
                break;
                case "alias":
                    ParseAliases();
                break;
                case "function":
                    _functionParser = new(_streamer, _scopes, _name);
                    _functionParser.Parse();
                break;
                case "procedure":
                    // Procedures don't return values, so they don't compose
                    // into expressions and we don't have a synthesiser path
                    // for them yet. Skip the whole declaration up to its
                    // matching `end procedure;` or `end <name>;` so the
                    // parser doesn't bail when AXI BFM-style helper procs
                    // appear in real-world VHDL.
                    SkipProcedureDeclaration();
                break;
                case "component":
                    ParseComponentDeclaration();
                break;
                case "type":
                    ParseTypeDeclaration();
                break;
                case "subtype":
                    ParseSubtypeDeclaration();
                break;
                case "shared":
                case "file":
                case "use":
                case "package":
                    // Skip these for now - just consume until semicolon
                    while(_streamer.Current?.Value != ";"){
                        _streamer.MoveNext();
                    }
                    _streamer.Expect(OPERATORS); // ;
                break;
                default:
                    throw new NotImplementedException($"Unsupported architecture header keyword: {_streamer.Current.Value}");
            }
        }
    }
    /// <summary>
    /// Consumes a procedure declaration without parsing its body. Records
    /// the procedure's name in the architecture scope so call sites resolve.
    /// </summary>
    /// <remarks>
    /// A real <c>ProcedureParser</c> mirroring <c>FunctionParser</c> would
    /// extract parameters and body. For now we just need the parser to
    /// accept procedures without throwing — that's what real fixtures
    /// (test_combinational_tb, AXI BFMs, package bodies with axi_write /
    /// axi_read) require. Body synth is task #18's deeper follow-up.
    /// </remarks>
    private void SkipProcedureDeclaration(){
        _logger.LogInformation("Skipping procedure declaration (body not synthesised)");
        _streamer.Expect(KEYWORDS); // 'procedure'
        string procName = _streamer.Current?.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(procName) && _scopes.ContainsKey(_name) &&
            !_scopes[_name].Contains(procName))
        {
            _scopes[_name].Add(procName);
        }
        // Consume until we see `end procedure;` or `end <procName>;` at
        // matching nesting depth.
        string procLower = procName.ToLower();
        while (_streamer.Current != null)
        {
            string val = _streamer.Current.Value.ToLower();
            if (val == "end")
            {
                _streamer.MoveNext();
                if (_streamer.Current == null) break;
                string nextVal = _streamer.Current.Value.ToLower();
                if (nextVal == "procedure" || nextVal == procLower)
                {
                    _streamer.MoveNext(); // 'procedure' or <procName>
                    if (_streamer.Current?.Type == IDENTIFIERS) _streamer.MoveNext();
                    if (_streamer.Current?.Value == ";") _streamer.MoveNext();
                    return;
                }
                continue;  // some other end (end if/loop/case/...) — keep scanning
            }
            _streamer.MoveNext();
        }
    }

    /// <summary>
    /// Parses a type declaration in architecture header.
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
            // Enumeration type
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
    /// Parses a subtype declaration in architecture header.
    /// </summary>
    private void ParseSubtypeDeclaration(){
        _streamer.Expect(KEYWORDS); // subtype
        string subtypeName = _streamer.Current?.Value ?? string.Empty;
        _scopes[_name].Add(subtypeName);
        _streamer.Expect(IDENTIFIERS); // subtype name
        _streamer.Expect(KEYWORDS); // is
        // Parse base type and optional constraint
        ParseTypeSpecification();
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses a record type definition.
    /// </summary>
    private void ParseRecordType(){
        _streamer.Expect(KEYWORDS); // record
        while(_streamer.Current?.Value.ToLower() != "end"){
            // Field names can be keywords like 'buffer', 'range', etc.
            string fieldName = _streamer.Current?.Value ?? string.Empty;
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
        ParseTypeSpecification();
    }
    /// <summary>
    /// Parses an enumeration type definition.
    /// </summary>
    private void ParseEnumerationType(){
        _streamer.Expect(PARENTHESIS); // (
        while(_streamer.Current?.Type != PARENTHESIS || _streamer.Current?.Value != ")"){
            if(_streamer.Current?.Type == IDENTIFIERS){
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
    /// Parses a type specification (type name with optional bounds/constraints).
    /// </summary>
    private void ParseTypeSpecification(){
        if(_streamer.Current?.Type == KEYWORDS || _streamer.Current?.Type == IDENTIFIERS){
            _streamer.MoveNext();
        }
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
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "range"){
            _streamer.Expect(KEYWORDS); // range
            while(_streamer.Current?.Value != ";" && _streamer.Current?.Value != ")" &&
                  _streamer.Current?.Value != ":=" && _streamer.Current?.Value.ToLower() != "end"){
                _streamer.MoveNext();
            }
        }
    }
    /// <summary>
    /// Parses 'alias' declarations within the architecture header.
    /// Format: alias name : type is expression;
    /// </summary>
    private void ParseAliases(){
        while(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "alias"){
            _streamer.Expect(KEYWORDS); // alias
            string aliasName = _streamer.Current?.Value ?? string.Empty;
            _scopes[_name].Add(aliasName);
            _streamer.Expect(IDENTIFIERS); // alias name
            // Optional type specification
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":"){
                _streamer.Expect(OPERATORS); // :
                // Parse type (may be complex like std_logic_vector(7 downto 0))
                ParseTypeSpecification();
            }
            _streamer.Expect(KEYWORDS); // is
            // Parse expression until semicolon
            while(_streamer.Current?.Value != ";"){
                _streamer.MoveNext();
            }
            _streamer.Expect(OPERATORS); // ;
        }
    }
    /// <summary>
    /// Parses a component declaration in the architecture header.
    /// </summary>
    private void ParseComponentDeclaration(){
        _logger.LogInformation("Parsing component declaration");
        _streamer.Expect(KEYWORDS); // component
        string componentName = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(IDENTIFIERS); // component name
        // Optional 'is' keyword
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "is"){
            _streamer.Expect(KEYWORDS); // is
        }
        // Parse optional generic declarations
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "generic"){
            ParseComponentGeneric();
        }
        // Parse port declarations
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "port"){
            ParseComponentPort();
        }
        // Parse end component
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // component
        // Optional component name after 'end component'
        if(_streamer.Current?.Type == IDENTIFIERS){
            _streamer.Expect(IDENTIFIERS);
        }
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation($"Component {componentName} declaration parsed");
    }
    /// <summary>
    /// Parses the generic block within a component declaration.
    /// </summary>
    private void ParseComponentGeneric(){
        _streamer.Expect(KEYWORDS); // generic
        _streamer.Expect(PARENTHESIS); // (
        // Parse generic parameters - simplified parsing that skips to closing )
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
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses the port block within a component declaration.
    /// </summary>
    private void ParseComponentPort(){
        _streamer.Expect(KEYWORDS); // port
        _streamer.Expect(PARENTHESIS); // (
        // Parse port parameters - simplified parsing that skips to closing )
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
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses the body of the architecture, which contains concurrent statements, processes, and component instantiations.
    /// </summary>
    public void ParseBodyDeclaration(){
        while(_streamer.Current?.Value.ToLower() != "end"){
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "process"){
                _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                _dataStructureParser.ParseProcess();
            }
            else if(_streamer.Current?.Type == IDENTIFIERS){
                // Check if this is a generate statement (label : for/if ... generate)
                if(IsGenerateStatement()){
                    ParseGenerateStatement();
                }
                // Check if this is a labeled process (label : process ...)
                else if(IsLabeledProcess()){
                    ParseLabeledProcess();
                }
                // Check if this is a block statement (label : block ...)
                else if(IsBlockStatement()){
                    ParseBlockStatement();
                }
                // Check if this is a component instantiation (label : component_name ...)
                else if(IsComponentInstantiation()){
                    ParseComponentInstantiation();
                }
                else{
                    _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                    _dataStructureParser.Parse();
                }
            }
            else{
                _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                _dataStructureParser.Parse();
            }
        }
    }
    /// <summary>
    /// Checks if the current position represents a labeled process (label : process ...).
    /// </summary>
    private bool IsLabeledProcess(){
        _streamer.SetRollBack();
        _streamer.MoveNext(); // past label
        bool isLabeledProcess = _streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":";
        if(isLabeledProcess){
            _streamer.MoveNext(); // past :
            isLabeledProcess = _streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "process";
        }
        _streamer.RollBack();
        return isLabeledProcess;
    }
    /// <summary>
    /// Parses a labeled process like "label : process(clk) begin ... end process label;".
    /// </summary>
    private void ParseLabeledProcess(){
        _logger.LogInformation("Parsing labeled process");
        string label = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(IDENTIFIERS); // label
        _streamer.Expect(OPERATORS); // :
        _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
        _dataStructureParser.ParseProcess();
        // Optional label after end process
        if(_streamer.Current?.Type == IDENTIFIERS && _streamer.Current?.Value == label){
            _streamer.Expect(IDENTIFIERS);
        }
    }
    /// <summary>
    /// Checks if the current position represents a block statement (label : block ...).
    /// </summary>
    private bool IsBlockStatement(){
        _streamer.SetRollBack();
        _streamer.MoveNext(); // past label
        bool isBlock = _streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":";
        if(isBlock){
            _streamer.MoveNext(); // past :
            isBlock = _streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "block";
        }
        _streamer.RollBack();
        return isBlock;
    }
    /// <summary>
    /// Parses a block statement (label : block (guard) begin ... end block label;).
    /// </summary>
    private void ParseBlockStatement(){
        _logger.LogInformation("Parsing block statement");
        string label = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(IDENTIFIERS); // label
        _streamer.Expect(OPERATORS); // :
        _streamer.Expect(KEYWORDS); // block
        // Optional guard condition in parentheses
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
            _streamer.Expect(PARENTHESIS); // )
        }
        // Optional declarations before begin - parse them to add signals to scope
        while(_streamer.Current?.Value.ToLower() != "begin"){
            if(_streamer.Current?.Type == KEYWORDS){
                string kw = _streamer.Current.Value.ToLower();
                if(kw == "signal" || kw == "constant"){
                    // Use ArchitectureAssignationParser to properly parse and add to scope
                    _archAssignations = new(_streamer, _scopes, _name, _entityName);
                    _archAssignations.Parse();
                }
                else if(kw == "type" || kw == "subtype"){
                    // Skip type declarations to semicolon
                    while(_streamer.Current?.Value != ";"){
                        _streamer.MoveNext();
                    }
                    _streamer.Expect(OPERATORS); // ;
                }
                else break;
            }
            else break;
        }
        _streamer.Expect(KEYWORDS); // begin
        // Parse block body (recursive call to ParseBodyDeclaration doesn't work, so parse manually)
        ParseBlockBody(label);
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // block
        // Optional label
        if(_streamer.Current?.Type == IDENTIFIERS && _streamer.Current?.Value == label){
            _streamer.Expect(IDENTIFIERS);
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses the body of a block statement until 'end'.
    /// </summary>
    private void ParseBlockBody(string label){
        while(_streamer.Current?.Value.ToLower() != "end"){
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "process"){
                _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                _dataStructureParser.ParseProcess();
            }
            else if(_streamer.Current?.Type == IDENTIFIERS){
                // Check for labeled constructs
                if(IsLabeledProcess()){
                    ParseLabeledProcess();
                }
                else if(IsBlockStatement()){
                    ParseBlockStatement();
                }
                else{
                    _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                    _dataStructureParser.Parse();
                }
            }
            else{
                _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                _dataStructureParser.Parse();
            }
        }
    }
    /// <summary>
    /// Checks if the current position represents a generate statement (label : for/if ... generate).
    /// </summary>
    private bool IsGenerateStatement(){
        _streamer.SetRollBack();
        _streamer.MoveNext(); // past label
        bool isGenerate = _streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":";
        if(isGenerate){
            _streamer.MoveNext(); // past :
            string keyword = _streamer.Current?.Value.ToLower() ?? string.Empty;
            isGenerate = _streamer.Current?.Type == KEYWORDS && (keyword == "for" || keyword == "if");
        }
        _streamer.RollBack();
        return isGenerate;
    }
    /// <summary>
    /// Parses a generate statement (for-generate or if-generate).
    /// </summary>
    private void ParseGenerateStatement(){
        _logger.LogInformation("Parsing generate statement");
        string label = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(IDENTIFIERS); // label
        _streamer.Expect(OPERATORS); // :

        string genType = _streamer.Current?.Value.ToLower() ?? string.Empty;
        if(genType == "for"){
            ParseForGenerate(label);
        }
        else if(genType == "if"){
            ParseIfGenerate(label);
        }
    }
    /// <summary>
    /// Parses a for-generate statement.
    /// </summary>
    private void ParseForGenerate(string label){
        _streamer.Expect(KEYWORDS); // for
        string iterVar = _streamer.Current?.Value ?? string.Empty;
        _scopes[_name].Add(iterVar); // Add iterator to scope temporarily
        _streamer.Expect(IDENTIFIERS); // iterator variable
        _streamer.Expect(KEYWORDS); // in
        // Parse range expression until 'generate'
        ParseGenerateRange();
        _streamer.Expect(KEYWORDS); // generate
        // Parse optional declarative region (signal, constant, etc.)
        ParseGenerateDeclarations();
        // Parse concurrent statements in the generate region
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "begin"){
            _streamer.Expect(KEYWORDS); // begin
        }
        // Parse generate body
        ParseGenerateBody();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // generate
        // Optional label after end generate
        if(_streamer.Current?.Type == IDENTIFIERS){
            _streamer.Expect(IDENTIFIERS);
        }
        _streamer.Expect(OPERATORS); // ;
        // Remove iterator variable from scope (it's local to this generate)
        _scopes[_name].Remove(iterVar);
    }
    /// <summary>
    /// Parses an if-generate statement.
    /// </summary>
    private void ParseIfGenerate(string label){
        _streamer.Expect(KEYWORDS); // if
        // Parse condition expression until 'generate'
        ParseGenerateCondition();
        _streamer.Expect(KEYWORDS); // generate
        // Parse optional declarative region
        ParseGenerateDeclarations();
        // Parse concurrent statements in the generate region
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "begin"){
            _streamer.Expect(KEYWORDS); // begin
        }
        // Parse generate body
        ParseGenerateBody();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // generate
        // Optional label after end generate
        if(_streamer.Current?.Type == IDENTIFIERS){
            _streamer.Expect(IDENTIFIERS);
        }
        _streamer.Expect(OPERATORS); // ;
    }
    /// <summary>
    /// Parses the range expression in a for-generate until 'generate' keyword.
    /// </summary>
    private void ParseGenerateRange(){
        while(_streamer.Current?.Type != KEYWORDS || _streamer.Current?.Value.ToLower() != "generate"){
            if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            else if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == KEYWORDS)
                _streamer.Expect(KEYWORDS);
            else if(_streamer.Current?.Type == OPERATORS)
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseGenerateParenthesis();
            }
            else
                break;
        }
    }
    /// <summary>
    /// Parses the condition expression in an if-generate until 'generate' keyword.
    /// </summary>
    private void ParseGenerateCondition(){
        while(_streamer.Current?.Type != KEYWORDS || _streamer.Current?.Value.ToLower() != "generate"){
            if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            else if(_streamer.Current?.Type == IDENTIFIERS)
                _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == KEYWORDS)
                _streamer.Expect(KEYWORDS);
            else if(_streamer.Current?.Type == OPERATORS)
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseGenerateParenthesis();
            }
            else
                break;
        }
    }
    /// <summary>
    /// Parses parenthesized expressions within generate statements.
    /// </summary>
    private void ParseGenerateParenthesis(){
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
    /// <summary>
    /// Parses optional declarations at the beginning of a generate statement.
    /// </summary>
    private void ParseGenerateDeclarations(){
        while(_streamer.Current?.Type == KEYWORDS){
            string keyword = _streamer.Current.Value.ToLower();
            if(keyword == "signal" || keyword == "constant" || keyword == "variable"){
                _archAssignations = new(_streamer, _scopes, _name, _entityName);
                _archAssignations.Parse();
            }
            else if(keyword == "begin"){
                break;
            }
            else{
                break;
            }
        }
    }
    /// <summary>
    /// Parses the body of a generate statement (concurrent statements, processes, components).
    /// </summary>
    private void ParseGenerateBody(){
        while(_streamer.Current?.Value.ToLower() != "end"){
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "process"){
                _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                _dataStructureParser.ParseProcess();
            }
            else if(_streamer.Current?.Type == IDENTIFIERS){
                // Nested generate or component instantiation
                if(IsGenerateStatement()){
                    ParseGenerateStatement();
                }
                else if(IsLabeledProcess()){
                    ParseLabeledProcess();
                }
                else if(IsComponentInstantiation()){
                    ParseComponentInstantiation();
                }
                else{
                    _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                    _dataStructureParser.Parse();
                }
            }
            else{
                _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                _dataStructureParser.Parse();
            }
        }
    }
    /// <summary>
    /// Checks if the current position represents a component instantiation (label : component ...).
    /// </summary>
    private bool IsComponentInstantiation(){
        // Peek ahead: if we see identifier : identifier (where second identifier is not an assignment operator)
        // This is a simplified check - look for the pattern "label : component_name"
        _streamer.SetRollBack();
        _streamer.MoveNext(); // past label
        bool isInstantiation = _streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ":";
        if(isInstantiation){
            _streamer.MoveNext(); // past :
            string keyword = _streamer.Current?.Value.ToLower() ?? string.Empty;
            // Exclude structural keywords that can appear after a label
            bool isStructuralKeyword = keyword == "process" || keyword == "for" || keyword == "if" ||
                                       keyword == "in" || keyword == "out" || keyword == "inout" ||
                                       keyword == "block" || keyword == "postponed";
            isInstantiation = _streamer.Current?.Type == IDENTIFIERS ||
                              (_streamer.Current?.Type == KEYWORDS && !isStructuralKeyword);
        }
        _streamer.RollBack();
        return isInstantiation;
    }
    /// <summary>
    /// Parses a component or direct-entity instantiation. Handles three real-
    /// world syntaxes:
    ///   <c>inst : Comp port map(...);</c>
    ///   <c>inst : entity work.Sub port map(...);</c>
    ///   <c>inst : entity work.Sub(arch_name) port map(...);</c>
    /// </summary>
    private void ParseComponentInstantiation(){
        _logger.LogInformation("Parsing component / entity instantiation");
        string label = _streamer.Current?.Value ?? string.Empty;
        _streamer.Expect(IDENTIFIERS); // label
        _streamer.Expect(OPERATORS); // :

        string componentName;

        // VHDL-93 direct entity instantiation: `entity <library>.<EntityName>[(arch_name)]`.
        if (_streamer.Current?.Type == KEYWORDS &&
            _streamer.Current?.Value.ToLower() == "entity")
        {
            _streamer.MoveNext(); // 'entity'

            // Library / entity name. Could be a single QUALIFIED_IDENTIFIERS
            // token like `work.Sub` or two IDENTIFIERS separated by `.`.
            if (_streamer.Current?.Type == QUALIFIED_IDENTIFIERS)
            {
                componentName = _streamer.Current?.Value ?? string.Empty;
                _streamer.MoveNext();
            }
            else
            {
                componentName = _streamer.Current?.Value ?? string.Empty;
                if (_streamer.Current?.Type == IDENTIFIERS) _streamer.MoveNext();
                if (_streamer.Current?.Value == ".")
                {
                    _streamer.MoveNext(); // '.'
                    if (_streamer.Current?.Type == IDENTIFIERS)
                    {
                        componentName = (componentName ?? string.Empty) + "." +
                                        (_streamer.Current?.Value ?? string.Empty);
                        _streamer.MoveNext();
                    }
                }
            }

            // Optional architecture suffix: `(rtl)` after the entity name.
            if (_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "(")
            {
                _streamer.MoveNext(); // '('
                if (_streamer.Current?.Type == IDENTIFIERS) _streamer.MoveNext();
                _streamer.Expect(PARENTHESIS); // ')'
            }
        }
        else
        {
            componentName = _streamer.Current?.Value ?? string.Empty;
            _streamer.Expect(IDENTIFIERS); // component name
        }
        // Parse optional generic map / port map. Value-based lookahead — the
        // tokenizer sometimes classifies the follow-up `generic`/`port`/`map`
        // after `entity work.X` as IDENTIFIERS rather than KEYWORDS depending
        // on context (audit F-G-05, 2026-07-09). Match on value only and use
        // MoveNext() so both classifications resolve.
        static bool IsWord(Token? tok, string word) =>
            tok != null && string.Equals(tok.Value, word, StringComparison.OrdinalIgnoreCase);

        if (IsWord(_streamer.Current, "generic")) {
            _streamer.MoveNext(); // 'generic'
            if (IsWord(_streamer.Current, "map")) _streamer.MoveNext(); // 'map'
            ParsePortMapContents();
        }
        if (IsWord(_streamer.Current, "port")) {
            _streamer.MoveNext(); // 'port'
            if (IsWord(_streamer.Current, "map")) _streamer.MoveNext(); // 'map'
            ParsePortMapContents();
        }
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation($"Component instantiation {label} : {componentName} parsed");
    }
    /// <summary>
    /// Parses the contents of a port map or generic map (the part inside parentheses).
    /// </summary>
    private void ParsePortMapContents(){
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
    /// <summary>
    /// Starts the parsing of the entire architecture block.
    /// </summary>
    public void Parse(){
        _logger.LogInformation("Parsing Architecture");
        ParseDeclaration();
        ParseBodyDeclaration();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(OPERATORS);
        _logger.LogInformation("Architecture Parsed");
    }
}