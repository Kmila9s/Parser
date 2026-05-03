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
public sealed class Diagnostic
{
    /// <summary>Severity bucket — drives Monaco marker colour + list grouping.</summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>1-based line number in the source file.</summary>
    public int Line { get; init; }

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
    public static Diagnostic FromException(ParserException ex) => new()
    {
        Severity    = DiagnosticSeverity.Error,
        Line        = Math.Max(1, ex.Line),
        StartColumn = Math.Max(1, ex.Column),
        EndColumn   = Math.Max(1, ex.Column) + Math.Max(1, ex.Context?.Length ?? 1),
        Code        = ex.ErrorCode?.ToString() ?? "PARSE-ERROR",
        Message     = ex.Message
    };
}
