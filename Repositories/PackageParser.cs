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
    public PackageParser(TokenStream streamer, Dictionary<string, HashSet<string>> scopes, string Name){
        _scopes = scopes;
        _streamer = streamer;
        _name = Name;
    }
    public string GetName() => _name;
    public Dictionary<string, HashSet<string>> GetScopes() => _scopes;
    public bool ExistsVariable(string variable)
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
    public void ParseDeclaration(){
        
    }
    public void ParseBodyDeclaration(){

    }
    public void Parse(){
        _streamer.Expect(KEYWORDS);
        if(_streamer.Current?.Type == KEYWORDS){
            _streamer.Expect(KEYWORDS);
            _name = _streamer.Current?.Value ?? string.Empty;
            _streamer.Expect(IDENTIFIERS);
            if(!_scopes.ContainsKey(_name))
                throw new ParserException(MISSING_STRUCTURE);
            _streamer.Expect(KEYWORDS);
            ParseBodyDeclaration();
        }else{
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
    }
}