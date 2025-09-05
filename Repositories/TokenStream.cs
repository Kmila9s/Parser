using Microsoft.Extensions.Logging;
using Parser.Models;

namespace Parser.Repositories;

public class TokenStream
{
    private readonly List<Token> _tokens = new();
    private int _currentPosition = 0, _rollBack = 0;
    private readonly ILogger<TokenStream> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<TokenStream>();
    public TokenStream(List<Token> Tokens)
    {
        _tokens = Tokens;
    }
    public Token? Current => (_currentPosition < _tokens.Count) ? _tokens[_currentPosition] : null;
    public void MoveNext() => _currentPosition++;
    public void SetRollBack() => _rollBack = _currentPosition;
    public void RollBack() => _currentPosition = _rollBack;
    public bool Match(Constants.TYPE ExpectedType)
    {
        if (Current != null && Current.Type == ExpectedType)
        {
            MoveNext();
            return true;
        }
        return false;
    }
    public bool EndStream() => _currentPosition >= _tokens.Count;
    public void Expect(Constants.TYPE ExpectedType)
    {
        if (!Match(ExpectedType))
        {
            if (Current?.Type == Constants.TYPE.UNKNOWN)
            {
                _logger.LogCritical($"Unknown token please check it out, skipping for simplicity: {Current?.Value}");
                MoveNext();
                return;
            }

            throw new StreamerException($"Expected type: {ExpectedType}, found: {Current?.Type} -> {Current?.Value} at Line: {Current?.Line + 1}:{Current?.Col + 1}");
        }
    }
    internal class StreamerException : Exception
    {
        public StreamerException(string message) : base(message) { }
        public override string ToString() => $"[VHDL Debugger Exception] : {this.Message}";
    }
}