namespace Parser.Models;

/// <summary>
/// A failure that knows where in the source it happened. Lets diagnostics
/// keep the position of any analysis error without depending on its type.
/// </summary>
public interface ISourcePosition
{
    /// <summary>1-based line, or 0 when unknown.</summary>
    int Line { get; }

    /// <summary>1-based column, or 0 when unknown.</summary>
    int Column { get; }
}
