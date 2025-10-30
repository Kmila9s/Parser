using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Microsoft.Extensions.Logging;
using Parser.Models;
namespace Parser.Repositories;

/// <summary>
/// A parser for VHDL 'range' declarations, inheriting from <see cref="VectorParser"/>.
/// </summary>
public class RangeParser : VectorParser {
    private readonly TokenStream _streamer;
    private readonly ILogger<RangeParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<RangeParser>();
    private string _name;

    /// <summary>
    /// Initializes a new instance of the <see cref="RangeParser"/> class.
    /// </summary>
    /// <param name="streamer">The token stream to process.</param>
    /// <param name="scopes">The dictionary of scopes to populate.</param>
    /// <param name="name">The name of the current scope.</param>
    public RangeParser(TokenStream streamer, Dictionary<string, HashSet<Variable>> scopes, string name)
    : base(streamer, scopes, name)
    {
        _streamer = streamer;
        _name = name;
    }
    /// <summary>
    /// Starts the parsing process. It checks if the current token is the 'range' keyword and parses it if it is.
    /// </summary>
    public override void Parse(){
        _logger.LogInformation("Parsing range");
        string dataType = (_streamer.Current?.Value ?? string.Empty).ToUpper();
        if(IsRange(dataType)){
            _streamer.Expect(KEYWORDS); // RANGE
            ParseBody();
        }
            
        _logger.LogInformation("Range Parsed");
    }
    /// <summary>
    /// Parses the body of the range declaration (e.g., '0 to 255').
    /// </summary>
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