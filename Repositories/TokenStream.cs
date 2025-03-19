using Parser.Models;

namespace Parser.Repositories;
public class TokenStream {
    private readonly List<Token> _tokens = new();
    private int _currentPosition = 0;
    public TokenStream(List<Token> Tokens) {
        _tokens = Tokens;
    }
    public Token? Current => (_currentPosition < _tokens.Count) ? _tokens[_currentPosition] : null;
    public void MoveNext() => _currentPosition++;
    public bool Match(Constants.TYPE ExpectedType){
        if(Current != null && Current.Type == ExpectedType){
            MoveNext();
            return true;
        }
        return false;
    }
    public bool EndStream() => _currentPosition >= _tokens.Count;
    public void Expect(Constants.TYPE ExpectedType){
        if(!Match(ExpectedType)){
            throw new Exception($"Expected type: {ExpectedType}, found: {Current?.Type}");
        }
    }
}