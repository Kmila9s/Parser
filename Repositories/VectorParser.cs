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
    public void ParseAttributes(){
        var variable = _streamer.Current?.Value.Split("'")[0];
        var attribute = _streamer.Current?.Value.Split("'")[1];
        if(!ExistsVariable(variable ?? string.Empty))
            throw new ParserException(MISSING_DECLARATION);
        _streamer.MoveNext();
    }
    private bool IsVector(string dataType) 
        => dataType.Equals("STD_LOGIC_VECTOR") || dataType.Equals("BIT_VECTOR") ||
            dataType.Equals("BOOLEAN_VECTOR") || dataType.Equals("INTEGER_VECTOR");
    public virtual void ParseBody(){
        _streamer.Expect(PARENTHESIS);
        ParseOperation();
        _streamer.Expect(KEYWORDS); // downto
        ParseOperation();
        _streamer.Expect(PARENTHESIS);
    }
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
            if(_streamer.Current?.Type == VECTORS_ATTRIBUTES)
                ParseAttributes();
            else if(_streamer.Current?.Type == IDENTIFIERS)
                if(!ExistsVariable(_streamer.Current.Value))
                    throw new ParserException(MISSING_DECLARATION);
                else
                    _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";")
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == KEYWORDS && _streamer.Current.Value.ToString() != "downto")
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
    private void ParseParenthesis(){
        _streamer.Expect(PARENTHESIS);
        ParseOperation();
        _streamer.Expect(PARENTHESIS);
    }
    protected bool CheckVariable(Token? current) 
        => current?.Type == IDENTIFIERS && ExistsVariable(current.Value);
    public Dictionary<string, HashSet<string>> GetScopes() => _scopes;
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable);
}