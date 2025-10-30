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
    private readonly ILogger<ArchitectureParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ArchitectureParser>();
    private Dictionary<string, HashSet<Variable>> _scopes = new();
    private ArchitectureAssignationParser _archAssignations = default!;
    private DataStructuresParser _dataStructureParser = default!;
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
        if(_streamer.Current?.Type == IDENTIFIERS){
            if(_scopes.ContainsKey(_streamer.Current.Value))
                throw new ParserException(DUPLICATE_STRUCTURE);
            _name = _streamer.Current.Value;
            _scopes.Add(_name, new());
        }
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS);
        string EntityName = _streamer.Current?.Value ?? string.Empty;
        if(!_scopes.ContainsKey(EntityName))
            throw new ParserException(MISSING_STRUCTURE);
        _entityName = EntityName;
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS);
        ParseHeader();
        _streamer.Expect(KEYWORDS); // begin
    }
    /// <summary>
    /// Parses the header of the architecture, which contains declarations like signals, constants, etc.
    /// </summary>
    public void ParseHeader(){
        if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToLower() == "begin") return;
        ParseBodyHeaderDeclaration();
    }
    /// <summary>
    /// Delegates parsing of different declaration types found in the architecture header.
    /// </summary>
    public void ParseBodyHeaderDeclaration(){
        if(_streamer.Current?.Type == KEYWORDS){
            switch(_streamer.Current.Value){
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
                case "shared":
                case "file":
                case "use":
                case "package":
                case "type":
                case "function":
                    //ParseFunction(EntityName, ArchitectureName);
                    throw new NotImplementedException();
                //break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
    /// <summary>
    /// Parses 'alias' declarations within the architecture header.
    /// </summary>
    private void ParseAliases(){
        while(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value == "alias"){
            _streamer.Expect(KEYWORDS);
            //string val = _streamer.Current.Value;
            _streamer.Expect(IDENTIFIERS);
            /*if(ExistsVariable(val))
                throw new ParserException(DUPLICATE_DECLARATION);
            else
                _scopes[_name].Add(val);*/
            _streamer.Expect(KEYWORDS);
            _streamer.Expect(IDENTIFIERS);
        }
    }
    /// <summary>
    /// Parses the body of the architecture, which contains concurrent statements and processes.
    /// </summary>
    public void ParseBodyDeclaration(){        
        while(_streamer.Current?.Value.ToLower() != "end")
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "process"){
                _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                _dataStructureParser.ParseProcess();
            }else{
                _dataStructureParser = new(_streamer, _scopes, _name, _entityName);
                _dataStructureParser.Parse();
            }
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