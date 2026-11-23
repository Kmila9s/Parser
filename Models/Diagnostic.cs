namespace Parser.Models;

/// <summary>
/// Severity of a parser diagnostic. Maps 1:1 to Monaco's marker severity
/// levels (Error = 8, Warning = 4, Info = 2) so the Editor can push these
/// straight into monaco.editor.setModelMarkers.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Hint / informational — not an error, often stylistic.</summary>
    Info = 1,
    /// <summary>Soft problem — parser continues, simulation may still run.</summary>
    Warning = 2,
    /// <summary>Hard problem — parser typically bailed.</summary>
    Error = 3
}

/// <summary>
/// A single structured parser diagnostic. Precise <c>(line, startCol, endCol)</c>
/// coordinates are 1-based so Monaco markers can be built without offset
/// arithmetic. <see cref="Code"/> is a short machine-readable identifier
/// (e.g. "VHD0012") the UI can show in tooltips.
/// </summary>
public sealed record Diagnostic
{
    /// <summary>Severity bucket — drives Monaco marker colour + list grouping.</summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>1-based line number in the source file.</summary>
    public int Line { get; init; }

    /// <summary>The file <see cref="Line"/> is in when it is not the parsed text itself (G82: an elaborated design's child); null otherwise.</summary>
    public string? File { get; init; }

    /// <summary>1-based starting column (inclusive).</summary>
    public int StartColumn { get; init; }

    /// <summary>1-based ending column (exclusive). When unknown, equal to StartColumn + 1.</summary>
    public int EndColumn { get; init; }

    /// <summary>Short identifier for the rule/error (e.g. "VHD0001", "SEMICOLON-MISSING").</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable message, typically a single sentence.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Convenience factory from a <see cref="ParserException"/> — preserves
    /// line + column + message so the existing throw-sites propagate cleanly
    /// into the structured diagnostic stream.
    /// </summary>
    public static Diagnostic FromException(ParserException ex) =>
        Error(ex.Line, ex.Column, ex.Context?.Length ?? 1, ex.ErrorCode?.ToString() ?? "PARSE-ERROR", ex.Message) with { File = ex.File };

    /// <summary>An error diagnostic at a 1-based position (unknown parts default to 1).</summary>
    private static Diagnostic Error(int line, int column, int width, string code, string message) => new()
    {
        Severity    = DiagnosticSeverity.Error,
        Line        = Math.Max(1, line),
        StartColumn = Math.Max(1, column),
        EndColumn   = Math.Max(1, column) + Math.Max(1, width),
        Code        = code,
        Message     = message
    };

    /// <summary>
    /// Source-aware overload. The tokenizer reports <c>Token.Col</c> as a
    /// per-line token index, not a character offset, so the bare
    /// <see cref="FromException(ParserException)"/> can land Monaco markers
    /// on whitespace or unrelated tokens. When the full file content is
    /// available, this overload locates <see cref="ParserException.Context"/>
    /// inside the offending source line and rewrites StartColumn / EndColumn
    /// to the real character range. Falls back to the bare overload when
    /// content is missing, the line is out of range, or the context value
    /// can't be found.
    /// </summary>
    public static Diagnostic FromException(ParserException ex, string? fileContent)
    {
        var basic = FromException(ex);
        // A position in another file (G82) cannot be placed in this content.
        if (string.IsNullOrEmpty(fileContent) || string.IsNullOrEmpty(ex.Context) || ex.Line <= 0 || ex.File is not null)
            return basic;

        var lines = fileContent.Replace("\r\n", "\n").Split('\n');
        if (ex.Line - 1 >= lines.Length) return basic;

        var src = lines[ex.Line - 1];
        int idx = src.IndexOf(ex.Context, StringComparison.Ordinal);
        if (idx < 0) return basic;

        return Error(basic.Line, idx + 1, ex.Context!.Length, basic.Code, basic.Message);
    }

    /// <summary>
    /// Diagnostic for any analysis failure. A <see cref="ParserException"/>
    /// goes through the source-aware overload (its column is placed on the
    /// offending name); any other <see cref="ISourcePosition"/> keeps its
    /// line and column; anything else lands on line 1. When the offending
    /// token is unknown the range is 6 columns wide (Monaco draws nothing for
    /// an empty range).
    /// </summary>
    /// <param name="ex">The failure.</param>
    /// <param name="fileContent">Analyzed source, used to place the column precisely.</param>
    /// <param name="code">Code for failures that are not a <see cref="ParserException"/> (which keeps its own code, or PARSE-ERROR).</param>
    public static Diagnostic FromAnyException(Exception ex, string? fileContent, string code) => ex switch
    {
        ParserException pex => FromException(pex, fileContent),
        ISourcePosition pos => Error(pos.Line, pos.Column, 6, code, ex.Message),
        _                   => Error(1, 1, 6, code, ex.Message),
    };
}
