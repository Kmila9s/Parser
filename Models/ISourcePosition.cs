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

/// <summary>
/// A positioned failure that can be moved to another file and line: an error of an
/// elaborated (flattened) text put back where the user wrote it (G82).
/// </summary>
public interface IRelocatable<out T> : ISourcePosition
{
    /// <summary>The source file of the position when it is not the parsed text itself; null otherwise.</summary>
    string? File { get; }

    /// <summary>This failure located at <paramref name="line"/> of <paramref name="file"/>.</summary>
    T Relocated(string file, int line);
}
