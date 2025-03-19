using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;

namespace Parser.Repositories;
public class VectorParser : IParseVectorStructure {
    private readonly TokenStream _streamer;
    private readonly ILogger<VectorParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<VectorParser>();
    private Dictionary<string, HashSet<string>> _scopes;
    private string _name;
    public bool Parsed { get; private set; } = false;
    public VectorParser(TokenStream streamer, Dictionary<string, HashSet<string>> scopes, string name)
    {
        _streamer = streamer;
        _scopes = scopes;
        _name = name;
    }
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
    private bool IsVector(string dataType) 
        => dataType.Equals("STD_LOGIC_VECTOR") || dataType.Equals("BIT_VECTOR") ||
            dataType.Equals("BOOLEAN_VECTOR") || dataType.Equals("INTEGER_VECTOR");
    public virtual void ParseBody(){
        _streamer.Expect(PARENTHESIS);
        if(CheckVariable(_streamer.Current))
            _streamer.Expect(IDENTIFIERS);
        else
            _streamer.Expect(LITERALS);
        if(_streamer.Current?.Type == OPERATORS){
            _streamer.MoveNext();
            if(CheckVariable(_streamer.Current))
                _streamer.Expect(IDENTIFIERS);
            else
                _streamer.Expect(LITERALS);
        }
        _streamer.Expect(KEYWORDS);
        if(CheckVariable(_streamer.Current))
            _streamer.Expect(IDENTIFIERS);
        else
            _streamer.Expect(LITERALS);
        if(_streamer.Current?.Type == OPERATORS){
            _streamer.MoveNext();
            if(CheckVariable(_streamer.Current))
                _streamer.Expect(IDENTIFIERS);
            else
                _streamer.Expect(LITERALS);
        }
        _streamer.Expect(PARENTHESIS);
    }
    protected bool CheckVariable(Token? current) 
        => current?.Type == IDENTIFIERS && ExistsVariable(current.Value);
    public Dictionary<string, HashSet<string>> GetScopes() => _scopes;
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
}