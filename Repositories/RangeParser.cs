using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Microsoft.Extensions.Logging;
using Parser.Models;
namespace Parser.Repositories;
public class RangeParser : VectorParser {
    private readonly TokenStream _streamer;
    private readonly ILogger<RangeParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<RangeParser>();
    private string _name;
    public RangeParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes, string name)
    : base(streamer, scopes, name)
    {
        _streamer = streamer;
        _name = name;
    }
    public override void Parse(){
        _logger.LogInformation("Parsing range");
        string dataType = (_streamer.Current?.Value ?? string.Empty).ToUpper();
        if(IsRange(dataType)){
            _streamer.Expect(KEYWORDS); // RANGE
            ParseBody();
        }
            
        _logger.LogInformation("Range Parsed");
    }
    public override void ParseBody(){
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
        if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";"){
            _streamer.MoveNext();
            if(CheckVariable(_streamer.Current))
                _streamer.Expect(IDENTIFIERS);
            else
                _streamer.Expect(LITERALS);
        }
    }
    private bool IsRange(string dataType) 
        => dataType.ToUpper().Equals("RANGE");
}