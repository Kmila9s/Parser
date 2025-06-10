namespace Parser.Models;
public class Token {
    public string Value { get; set; } = string.Empty;
    public Constants.TYPE Type { get; set; } = Constants.TYPE.UNKNOWN;
    public int Line { get; set; }
    public int Col { get; set; }
    public Token(string Value, Constants.TYPE Type)
    {
        this.Value = Value;
        this.Type = Type;
    }
}