using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;
namespace Parser.Repositories;
public class PackageParser : IParseStructures{
    private TokenStream _streamer;
    private readonly ILogger<ArchitectureParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ArchitectureParser>();
    private Dictionary<string, HashSet<string>> _scopes = new();
    private string _name = string.Empty, _entityName = string.Empty;
    private FunctionParser _functionParser = default!;
    private GenericParser _genericParser = default!;
    public PackageParser(TokenStream streamer, Dictionary<string, HashSet<string>> scopes){
        _scopes = scopes;
        _streamer = streamer;
    }
    public string GetName() => _name;
    public Dictionary<string, HashSet<string>> GetScopes() => _scopes;
    public bool ExistsVariable(string variable)
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    public void ParseDeclaration(){
        while(_streamer.Current?.Value.ToLower() != "end"){
            switch(_streamer.Current?.Value){
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
                case "type":
                case "procedure":
                    throw new NotImplementedException();
                default:
                    throw new ParserException(SYNTAX_ERROR);
            }
        }
    }
    public void ParseBodyDeclaration(){
        while(_streamer.Current?.Value.ToLower() != "end"){
            switch(_streamer.Current?.Value){
                case "function":
                    _functionParser = new(_streamer, _scopes, _name);
                    _functionParser.Parse();
                break;
                case "end":
                    // Default case
                break;
                case "procedure":
                    throw new NotImplementedException();
                default:
                    throw new ParserException(SYNTAX_ERROR);
            }
        }
    }
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