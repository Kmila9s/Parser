namespace Parser.Models;

/// <summary>
/// F-G-08 pass 4 (2026-07-17): thrown when a Skip*/Parse* while-loop inside
/// the synthesis descent exceeds its safety cap. Historically these loops
/// were unbounded and could hang the parser indefinitely on pathological
/// input (see Session 22 pass 3 findings on the elaborated Ring 5 capstone).
/// The exception carries enough context to identify the exact site so the
/// underlying bug can be fixed rather than papered over.
/// </summary>
public sealed class ParserSkipOverflowException : ParserException
{
    /// <summary>Name of the Skip* / Parse* method that ran past its cap.</summary>
    public string Site { get; }

    /// <summary>Cap that was exceeded.</summary>
    public int Cap { get; }

    /// <summary>Best-known source line where the loop was operating.</summary>
    public int SourceLine { get; }

    /// <summary>Optional token snippet showing what the loop was stuck on.</summary>
    public string TokenContext { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParserSkipOverflowException"/> class.
    /// </summary>
    /// <param name="site">The parser site (routine) where the safety cap fired.</param>
    /// <param name="cap">The iteration cap that was exceeded.</param>
    /// <param name="sourceLine">The source line the parser was operating on.</param>
    /// <param name="tokenContext">A token snippet showing what the loop was stuck on.</param>
    public ParserSkipOverflowException(string site, int cap, int sourceLine, string tokenContext)
        : base(
            $"Parser {site} exceeded {cap} iterations near source line {sourceLine}. " +
            $"Token context: {tokenContext}. " +
            $"F-G-08 pass 4 safety cap fired — the parser was likely hung on this construct.",
            sourceLine,
            0,
            site)
    {
        Site = site;
        Cap = cap;
        SourceLine = sourceLine;
        TokenContext = tokenContext;
    }
}
