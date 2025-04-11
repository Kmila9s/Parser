using static Parser.Models.Constants.TYPE;
using Microsoft.Extensions.Logging;
using Parser.Models;
namespace Parser.Repositories;
public class Parser{
    private TokenStream _streamer;
    private readonly ILogger<Parser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Parser>();
    private Dictionary<string, List<string>> _ports = new(), _archDeclarations = new(), 
    _generics = new(), _packages = new();
    public Dictionary<string, HashSet<Variable>> Scopes { get; private set; } = new();
    private List<string> _entities = new(), _architectures = new();
    private Tokenizer _tokenizer;
    private DateTime _start = DateTime.Now, _end;
    public Parser(Tokenizer tokenizer)
    {
        _tokenizer = tokenizer;
        var temp = tokenizer.GetDictionaryType();
        if(temp.Count <= 0) throw new Exception("Tokenizer must be initialized");
        _streamer = new(tokenizer.GetDictionaryType());
    }
    public Dictionary<string, HashSet<Variable>> GetScopes() => Scopes;
    public Parser(string FilePath){
        _tokenizer = new(FilePath);
        _streamer = new(_tokenizer.GetDictionaryType());
    }
    public void ParseV2(){
        Console.WriteLine("");
        _logger.LogInformation("Starting parsing");
        if(_streamer.Current?.Value.ToLower() == "library")
            ParseLibrariesUsage();
        else
            _logger.LogWarning("Libraries not detected");
        
        EntityParser entityParser = new(_streamer, Scopes);
        ArchitectureParser architectureParser = new(_streamer, Scopes, entityParser.Entity);
        PackageParser packageParser = new(_streamer, Scopes);
        while(!_streamer.EndStream())
            switch(_streamer.Current?.Value.ToLower()){
                case "entity":
                    entityParser.Parse();
                break;
                case "architecture":
                    architectureParser.Parse();
                break;
                case "package":
                    packageParser.Parse();
                break;
                default:
                    throw new NotImplementedException();
                //break;
            }
        _logger.LogInformation("Parse completed");
        _end = DateTime.Now;
        _logger.LogInformation($"Total time: {(_end - _start).TotalMilliseconds} ms");
    }
    public void Parse(){
        Console.WriteLine("");
        _logger.LogInformation("Starting parsing");
        if(_streamer.Current?.Value.ToLower() == "library")
            ParseLibrariesUsage();
        else
            _logger.LogWarning("Libraries not detected");
        while(!_streamer.EndStream())
            switch(_streamer.Current?.Value.ToLower()){
                case "entity":
                    ParseEntity();
                break;
                case "architecture":
                    ParseArchitecture();
                break;
                case "package":
                    ParsePackage();
                break;
            }
        _logger.LogInformation("Parse completed");
        _end = DateTime.Now;
        _logger.LogInformation($"Total time: {(_end - _start).TotalMilliseconds} ms");
    }
    private void ParsePackage(){
        _streamer.Expect(KEYWORDS);
        string name = _streamer.Current?.Value ?? string.Empty;
        bool isFirst = _streamer.Current?.Type != KEYWORDS;
        if(_streamer.Current?.Type != KEYWORDS)
            _streamer.Expect(IDENTIFIERS);
        else{
            _streamer.Expect(KEYWORDS); // body
            name = _streamer.Current?.Value ?? string.Empty;
            _streamer.Expect(IDENTIFIERS);
        }
        if(!_packages.ContainsKey(name))
            if(isFirst)
                _packages.Add(name, new());
            else
                throw new Exception("Package not declared in this scope");
        else
            if(isFirst)
                throw new Exception("Package already declared in this scope");
        _streamer.Expect(KEYWORDS); // is
        ParsePackageBody("", isFirst);
        _streamer.Expect(KEYWORDS); //end
        _streamer.Expect(IDENTIFIERS); // name
        _streamer.Expect(OPERATORS); // ;
    }
    private void ParsePackageBody(string PackageName, bool IsFirst){
        if(IsFirst)
            ParseFunction();
        else
            ParsePackageBody("", true);

    }
    private void ParseLibrariesUsage(){
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(OPERATORS);
        while(_streamer.Current?.Value.ToLower().Equals("use") ?? false){
            ParseUseLibrary();
            if(_streamer.Current?.Type == OPERATORS && (_streamer.Current?.Value.Equals(";") ?? false))
                _streamer.MoveNext();
        }
    }
    private void ParseUseLibrary(){
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(QUALIFIED_IDENTIFIERS);
    }
    private void ParseFunction(string EntityName = "", string ArchitectureName = "", string PackageName = ""){
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(PARENTHESIS);
        ParseFunctionPortDeclaration("--- NAME ---");
        _streamer.Expect(PARENTHESIS);
        _streamer.Expect(KEYWORDS); // RETURN
        _streamer.Expect(KEYWORDS); // DATATYPE
        _streamer.Expect(OPERATORS); // ;
    }
    private void ParseFunctionPortDeclaration(string FunctionName){
        while(_streamer.Current?.Type == IDENTIFIERS){
            ParseFunctionPortsWithComas(FunctionName);
            _streamer.Expect(OPERATORS);
            _streamer.Expect(KEYWORDS);
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value == ";"){
                _streamer.Expect(OPERATORS);
            }
        }
    }
    private void ParseFunctionPortsWithComas(string FunctionName){
        while(_streamer.Current?.Type == IDENTIFIERS){
            /*if(!_archDeclarations.ContainsKey(ArchitectureName))
                throw new Exception($"Entity not declared: {ArchitectureName}");
            if(ExistsVariable(_streamer.Current.Value, EntityName, ArchitectureName))
                throw new Exception($"Asignation already declared on architecture: {ArchitectureName}-{_streamer.Current.Value}");
            _archDeclarations[ArchitectureName].Add(_streamer.Current.Value);*/
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current.Type == SEPARATORS && _streamer.Current.Value.Equals(","))
                _streamer.MoveNext();
            else
                break;   
        }
    }
    private void ParseArchitecture(){
        var (EntityName, ArchitectureName) = ParseArchitectureDeclaration();
        while(_streamer.Current?.Value.ToLower() != "end")
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "process")
                ParseProcess(EntityName, ArchitectureName);
            else
                ParseDataStructures(EntityName, ArchitectureName);
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(OPERATORS);
    }
    private void ParseDataStructures(string EntityName, string ArchitectureName = ""){
        switch(_streamer.Current?.Value.ToLower()){
            case "if":
                ParseIfStructure(EntityName, ArchitectureName);
                ParseDataStructures(EntityName, ArchitectureName);
            break;
            case "case":
                ParseCaseStructure(EntityName, ArchitectureName);
                ParseDataStructures(EntityName, ArchitectureName);
            break;
            case "for":
            break;
            case "while":
            break;
            case "exit":
            break;
            case "wait":
            break;
            case "return":
            break;
            case "null":
            break;
            default:
                if(_streamer.Current?.Type == IDENTIFIERS){
                    ParseAsignations(EntityName, ArchitectureName);
                    ParseDataStructures(EntityName, ArchitectureName);
                }
            break;
        }
    }
    private void ParseIfStructure(string EntityName, string ArchitectureName = ""){
        _streamer.Expect(KEYWORDS);
        ParseConditionalOperation(EntityName, ArchitectureName);
        _streamer.Expect(KEYWORDS); // then
        ParseDataStructures(EntityName, ArchitectureName);
        if(_streamer.Current?.Type == KEYWORDS){
            string value = _streamer.Current.Value;
            _streamer.Expect(KEYWORDS);
            if(value == "else")
                ParseDataStructures(EntityName, ArchitectureName);
            else if(value == "elif")
                ParseIfStructure(EntityName, ArchitectureName);
        }
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // if
        _streamer.Expect(OPERATORS); // ;
    }
    private void ParseConditionalOperation(string EntityName, string ArchitectureName = ""){
        while(_streamer.Current?.Value != "then"){
            var type = _streamer.Current?.Type ?? UNKNOWN;
            var val = _streamer.Current?.Value ?? string.Empty;
            if(type == PARENTHESIS && val.Equals("(")){
                _streamer.MoveNext();
                ParseConditionalOperation(EntityName, ArchitectureName);
            }else if(type == PARENTHESIS && val.Equals(")")){
                _streamer.MoveNext();
                break;
            }
            switch(type){
                case KEYWORDS:
                    switch(val){
                        case "rising_edge":
                        case "falling_edge":
                            _streamer.MoveNext();
                            ParseConditionalOperation(EntityName, ArchitectureName);
                        break;
                        case "and":
                        case "or":
                        case "xor":
                        break;
                        case "others":
                            _streamer.Expect(KEYWORDS);
                            _streamer.Expect(OPERATORS);
                            if(_streamer.Current?.Type == LITERALS ||
                            (_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value, EntityName, ArchitectureName)))
                                _streamer.MoveNext();
                            else
                                throw new Exception("Literal or identifier expected");
                        break;
                        default:
                            throw new Exception("Expecting other type of keyword");
                    }
                break;
                case IDENTIFIERS:
                    _streamer.Expect(IDENTIFIERS);
                    if(_streamer.Current?.Type == SEPARATORS &&
                    _streamer.Current?.Value == "=>" || _streamer.Current?.Type == OPERATORS)
                        _streamer.MoveNext();
                    else 
                        throw new Exception("Expected an operator or separator");
                    ParseConditionalOperation(EntityName, ArchitectureName);
                break;
            } 
        }
    }
    private void ParseCaseStructure(string EntityName, string ArchitectureName = ""){
        _streamer.Expect(KEYWORDS);
        if(!ExistsVariable(_streamer.Current?.Value ?? string.Empty, EntityName, ArchitectureName))
            throw new Exception($"Port not defined: {_streamer.Current?.Value}");
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS); // is
        ParseCaseCases(EntityName, ArchitectureName);
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // case
        _streamer.Expect(OPERATORS); // ;
    }
    private void ParseCaseCases(string EntityName, string ArchitectureName = ""){
        while(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "when"){
            _streamer.Expect(KEYWORDS);
            if(_streamer.Current?.Type == LITERALS || _streamer.Current?.Value.ToLower() == "others")
                _streamer.MoveNext();
            else
                throw new Exception($"Others, Literal or Identifier expected");
            _streamer.Expect(OPERATORS);
            ParseDataStructures(EntityName, ArchitectureName);
        }
    }
    private bool ExistsVariable(string varName, string EntityName, string ArchitectureName = "") =>
        (
            _ports.ContainsKey(EntityName) &&
            _ports[EntityName].Exists(p => p.Equals(varName))
        ) ||
        (
            !String.IsNullOrEmpty(ArchitectureName) && 
            _archDeclarations[ArchitectureName].Exists(p => p.Equals(varName))
        ) ||
        (
            _generics.ContainsKey(EntityName) &&
            _generics[EntityName].Exists(p => p.Equals(varName))
        );
    private void ParseAsignations(string EntityName, string ArchitectureName = ""){
        while(_streamer.Current?.Type == IDENTIFIERS){
            string varName = _streamer.Current.Value;
            if(!ExistsVariable(varName, EntityName, ArchitectureName))
                throw new Exception($"Identifier not declared in this scope: {varName}");
            _streamer.Expect(IDENTIFIERS);
            _streamer.Expect(OPERATORS);
            ParseOperation(EntityName, ArchitectureName);
            _streamer.Expect(OPERATORS); // ; CASE 
        }
    }
    private void ParseOperation(string EntityName, string ArchitectureName = ""){
        while(_streamer.Current?.Value != ";" || _streamer.Current?.Type != PARENTHESIS){
            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis(EntityName, ArchitectureName);
                if((_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";") || _streamer.Current?.Type == KEYWORDS){
                    _streamer.MoveNext();
                    ParseOperation(EntityName, ArchitectureName);
                }
            }
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "not")
                _streamer.MoveNext();
            if(_streamer.Current?.Type == IDENTIFIERS)
                if(!ExistsVariable(_streamer.Current.Value, EntityName, ArchitectureName))
                    throw new Exception($"Identifier not declared in this scope: {_streamer.Current.Value}");
                else
                    _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";")
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == KEYWORDS)
                if(_streamer.Current.Value.ToLower().Equals("after")){
                    _streamer.MoveNext();
                    if(_streamer.Current?.Type == LITERALS || (_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value, EntityName, ArchitectureName)))
                        _streamer.MoveNext();
                    else
                        throw new Exception("Identifier or literal expected");
                    _streamer.Expect(TIME_VARIABLES);
                }else
                    _streamer.Expect(KEYWORDS); // CASES LIKE XOR, AND, OR
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "(")
                continue;
            else
                break;
        }
    }
    private void ParseParenthesis(string EntityName, string ArchitectureName = ""){
        _streamer.Expect(PARENTHESIS);
        ParseOperation(EntityName, ArchitectureName);
        _streamer.Expect(PARENTHESIS);
    }
    private void ParseProcess(string EntityName, string ArchitectureName = ""){
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(PARENTHESIS);
        ParseProcessVariables(EntityName);
        _streamer.Expect(PARENTHESIS);
        _streamer.Expect(KEYWORDS); // begin
        ParseDataStructures(EntityName, ArchitectureName);
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // process
        _streamer.Expect(OPERATORS); // ;
    }
    private void ParseProcessVariables(string EntityName){
        if(_streamer.Current?.Type == PARENTHESIS) return; // NOTHING INTO PARENTEHISIS FROM THE PROCESS
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!ExistsVariable(_streamer.Current?.Value ?? string.Empty, EntityName))
                throw new Exception($"Port not decalred on: {EntityName}");
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current?.Type == SEPARATORS)
                _streamer.MoveNext();
                
        }
    }
    private (string, string) ParseArchitectureDeclaration(){
        _streamer.Expect(KEYWORDS);
        string ArchitectureName = string.Empty;
        if(_streamer.Current?.Type == IDENTIFIERS){
            if(_architectures.Exists(p => p.Equals(_streamer.Current.Value)))
                throw new Exception($"Architecture already defined: {_streamer.Current.Value}");
            ArchitectureName = _streamer.Current.Value;
            _architectures.Add(ArchitectureName);
            _archDeclarations.Add(ArchitectureName, new());
        }
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS);
        string EntityName = _streamer.Current?.Value ?? string.Empty;
        if(_streamer.Current?.Type == IDENTIFIERS && !_entities.Exists(p => p.Equals(_streamer.Current.Value)))
            throw new Exception($"Entity not defined: {_streamer.Current.Value}");    
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS);
        ParseArchitectureHeader(EntityName, ArchitectureName);
        _streamer.Expect(KEYWORDS); // begin
        return (EntityName, ArchitectureName);
    }
    private void ParseArchitectureHeader(string EntityName, string ArchitectureName){
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower() == "begin") return;
        ParseSingleArchitectureAsignations(EntityName, ArchitectureName);
    }
    private void ParseSingleArchitectureAsignations(string EntityName, string ArchitectureName){
        if(_streamer.Current?.Type == KEYWORDS){
            switch(_streamer.Current.Value){
                case "signal":
                case "constant":
                case "attribute":
                case "group":
                case "disconnect":
                    ParseSingleArchitectureAssignations(EntityName, ArchitectureName);
                break;
                case "type":
                break;
                case "alias":
                    ParseAliases(EntityName, ArchitectureName);
                break;
                case "shared":
                break;
                case "file":
                break;
                case "use":
                break;
                case "package":
                break;
                case "function":
                    ParseFunction(EntityName, ArchitectureName);
                break;
                default:
                    throw new Exception("");
            }
        }
    }
    private void ParseSinglePackageAssignations(string PackageName){
        if(_streamer.Current?.Type == KEYWORDS){
            switch(_streamer.Current.Value){
                case "signal":
                case "constant":
                case "attribute":
                case "group":
                case "disconnect":
                    ParseSinglePackageAssignations(PackageName);
                break;
                case "type":
                break;
                case "alias":
                    ParseAliases("", "");
                break;
                case "shared":
                break;
                case "file":
                break;
                case "use":
                break;
                case "package":
                break;
                case "function":
                    ParseFunction("");
                break;
                default:
                    throw new Exception("");
            }
        }
    }
    private void ParseAliases(string EntityName, string ArchitectureName){
        while(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value == "alias"){
            _streamer.Expect(KEYWORDS);
            _streamer.Expect(IDENTIFIERS);
            _streamer.Expect(KEYWORDS);
            _streamer.Expect(IDENTIFIERS);
        }
    }
    private void ParseSingleArchitectureAssignations(string EntityName, string ArchitectureName){
        while(_streamer.Current?.Type == KEYWORDS && 
            (
                _streamer.Current?.Value == "signal" || _streamer.Current?.Value == "constant" ||
                _streamer.Current?.Value == "attribute" || _streamer.Current?.Value == "group" ||
                _streamer.Current?.Value == "disconnect"
            )
        ){
            _streamer.Expect(KEYWORDS); 
            ParseArchitectureIdentifiersWithComas(EntityName, ArchitectureName);
            _streamer.Expect(OPERATORS);
            string val = _streamer.Current?.Value.ToUpper() ?? string.Empty;
            _streamer.Expect(KEYWORDS);
            if(val.Equals("STD_LOGIC_VECTOR") || val.Equals("BIT_VECTOR") ||
                val.Equals("BOOLEAN_VECTOR") || val.Equals("INTEGER_VECTOR")
            )
                ParseVectorBody(EntityName, ArchitectureName);
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower().Equals("range"))
                ParseRangeBody();
            if(val.ToLower().Equals("time")){
                _streamer.Expect(OPERATORS);
                _streamer.Expect(LITERALS);
                _streamer.Expect(TIME_VARIABLES);
            }
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current.Value != ";"){
                // Asignation
                _streamer.Expect(OPERATORS);
                _streamer.Expect(LITERALS);
            }
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower().Equals("after")){
                _streamer.Expect(KEYWORDS); // after
                _streamer.Expect(LITERALS);
                _streamer.Expect(TIME_VARIABLES);
            }
            _streamer.Expect(OPERATORS); // ;
        }
    }
    private void ParseArchitectureIdentifiersWithComas(string EntityName, string ArchitectureName){
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!_archDeclarations.ContainsKey(ArchitectureName))
                throw new Exception($"Entity not declared: {ArchitectureName}");
            if(ExistsVariable(_streamer.Current.Value, EntityName, ArchitectureName))
                throw new Exception($"Asignation already declared on architecture: {ArchitectureName}-{_streamer.Current.Value}");
            _archDeclarations[ArchitectureName].Add(_streamer.Current.Value);
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current.Type == SEPARATORS && _streamer.Current.Value.Equals(","))
                _streamer.MoveNext();
            else
                break;   
        }
    }
    private void ParseGenericAssignations(string EntityName){
        while(_streamer.Current?.Type == IDENTIFIERS){
            ParseGenericIdentifiersWithComas(EntityName);
            _streamer.Expect(OPERATORS);
            string val = _streamer.Current?.Value.ToUpper() ?? string.Empty;
            _streamer.Expect(KEYWORDS);
            if(val.Equals("STD_LOGIC_VECTOR") || val.Equals("BIT_VECTOR") ||
                val.Equals("BOOLEAN_VECTOR") || val.Equals("INTEGER_VECTOR")
            )
                ParseVectorBody(EntityName);
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower().Equals("range"))
                ParseRangeBody();
            if(val.ToLower().Equals("time")){
                _streamer.Expect(OPERATORS);
                _streamer.Expect(LITERALS);
                _streamer.Expect(TIME_VARIABLES);
            }
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current.Value != ";"){
                // Asignation
                _streamer.Expect(OPERATORS);
                _streamer.Expect(LITERALS);
            }
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower().Equals("after")){
                _streamer.Expect(KEYWORDS); // after
                _streamer.Expect(LITERALS);
                _streamer.Expect(TIME_VARIABLES);
            }
        }
    }
    private void ParseGenericIdentifiersWithComas(string EntityName){
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!_generics.ContainsKey(EntityName))
                _generics.Add(EntityName, new());
            if(ExistsVariable(_streamer.Current.Value, EntityName))
                throw new Exception($"Asignation already declared: {EntityName}-{_streamer.Current.Value}");
            _generics[EntityName].Add(_streamer.Current.Value);
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current.Type == SEPARATORS && _streamer.Current.Value.Equals(","))
                _streamer.MoveNext();
            else
                break;   
        }
    }
    private void ParseEntity(){
        _streamer.Expect(KEYWORDS);
        string EntityName = _streamer.Current?.Value ?? string.Empty;
        if(_entities.Exists(p => p.Equals(EntityName)) || _ports.ContainsKey(EntityName))
            throw new Exception($"Entity already defined: {EntityName}");
        _ports.Add(EntityName, new());
        _entities.Add(EntityName);
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS);
        if(_streamer.Current?.Value.ToLower() == "generic")
            ParseGenericStructure(EntityName);
        if(_streamer.Current?.Value.ToLower() == "port")
            ParsePortDeclaration(EntityName);
        _streamer.Expect(KEYWORDS);
        string ClosureName = _streamer.Current?.Value ?? string.Empty;
        if(String.IsNullOrEmpty(ClosureName) || String.IsNullOrEmpty(EntityName))
            throw new Exception($"Expected Entity name");
        _streamer.Expect(IDENTIFIERS);
        if(!ClosureName.Equals(EntityName))
            throw new Exception($"Expecting same identifier for entity closure, found: {_streamer.Current?.Value}, Expected: {EntityName} ");
        _streamer.Expect(OPERATORS);
    }
    private void ParseGenericStructure(string EntityName, string ArchitectureName = ""){
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(PARENTHESIS);
        while(_streamer.Current?.Type == IDENTIFIERS){
            ParseGenericAssignations(EntityName);
            if(_streamer.Current?.Type == OPERATORS && (_streamer.Current?.Value.Equals(";") ?? false))
                _streamer.MoveNext();
        }
        _streamer.Expect(PARENTHESIS);
        _streamer.Expect(OPERATORS);
    }
    private void ParsePortDeclaration(string EntityName){
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(PARENTHESIS);
        while(_streamer.Current?.Type == IDENTIFIERS){
            ParsePort(EntityName);
            if(_streamer.Current?.Type == OPERATORS && (_streamer.Current?.Value.Equals(";") ?? false))
                _streamer.MoveNext();
        }
        _streamer.Expect(PARENTHESIS);
        _streamer.Expect(OPERATORS);
    }
    private void ParseVectorBody(string EntityName, string ArchitectureName = ""){
        _streamer.Expect(PARENTHESIS);
        if(_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value, EntityName, ArchitectureName))
            _streamer.Expect(IDENTIFIERS);
        else
            _streamer.Expect(LITERALS);
        if(_streamer.Current?.Type == OPERATORS){
            _streamer.MoveNext();
            if(_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value, EntityName, ArchitectureName))
                _streamer.Expect(IDENTIFIERS);
            else
                _streamer.Expect(LITERALS);
        }
        _streamer.Expect(KEYWORDS);
        if(_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value, EntityName, ArchitectureName))
            _streamer.Expect(IDENTIFIERS);
        else
            _streamer.Expect(LITERALS);
        if(_streamer.Current?.Type == OPERATORS){
            _streamer.MoveNext();
            if(_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value, EntityName, ArchitectureName))
                _streamer.Expect(IDENTIFIERS);
            else
                _streamer.Expect(LITERALS);
        }
        _streamer.Expect(PARENTHESIS);
    }
    private void ParseRangeBody(){
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(LITERALS);
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(LITERALS);
    }
    private void ParsePort(string EntityName){
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!_ports.ContainsKey(EntityName))
                throw new Exception($"Entity not declared: {EntityName}");
            if(_ports[EntityName].Exists(p => p.Equals(_streamer.Current.Value)))
                throw new Exception($"Port already declared on entity: {EntityName}-{_streamer.Current.Value}");
            _ports[EntityName].Add(_streamer.Current.Value);
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current.Type == SEPARATORS && _streamer.Current.Value.Equals(",")){
                _streamer.MoveNext();
            }else{
                break;
            }
        }
        _streamer.Expect(OPERATORS);
        _streamer.Expect(KEYWORDS);
        string dataType = (_streamer.Current?.Value ?? string.Empty).ToUpper();
        _streamer.Expect(KEYWORDS);
        if(dataType.Equals("STD_LOGIC_VECTOR") || dataType.Equals("BIT_VECTOR") ||
            dataType.Equals("BOOLEAN_VECTOR") || dataType.Equals("INTEGER_VECTOR")
        )
            ParseVectorBody(EntityName);
    }
}
