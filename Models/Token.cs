namespace Parser.Models;

/// <summary>
/// Represents a token extracted from the VHDL source code.
/// </summary>
public class Token {
    /// <summary>
    /// Gets or sets the string value of the token.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the type of the token.
    /// </summary>
    public Constants.TYPE Type { get; set; } = Constants.TYPE.UNKNOWN;
    /// <summary>
    /// Gets or sets the 0-based line number where the token was found.
    /// </summary>
    public int Line { get; set; }
    /// <summary>
    /// The 1-based source line of the token — the convention of every line kept on
    /// a model (<c>Operation.SourceLine</c>, branch condition lines, diagnostics).
    /// </summary>
    public int SourceLine => Line + 1;
    /// <summary>
    /// Gets or sets the column number where the token begins.
    /// </summary>
    public int Col { get; set; }
    /// <summary>
    /// True for a selected-name suffix token such as <c>.lo</c> — what the
    /// tokenizer leaves after <c>m(i)</c> in <c>m(i).lo</c>.
    /// </summary>
    public bool IsFieldSelector => IsFieldSelectorText(Value);

    /// <summary>The <see cref="IsFieldSelector"/> rule on raw token text.</summary>
    public static bool IsFieldSelectorText(string value) =>
        value.Length > 1 && value[0] == '.' && (char.IsLetter(value[1]) || value[1] == '_');

    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> class.
    /// </summary>
    /// <param name="Value">The string value of the token.</param>
    /// <param name="Type">The type of the token.</param>
    public Token(string Value, Constants.TYPE Type)
    {
        this.Value = Value;
        this.Type = Type;
    }
}