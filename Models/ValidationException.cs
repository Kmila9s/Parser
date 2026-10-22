namespace Parser.Models;

/// <summary>
/// A <see cref="ParserException"/> raised specifically by a semantic VALIDATION
/// rule (e.g. missing ';', assign to input port, type/width mismatch) as opposed
/// to a synthesis/parse failure on complex-but-valid input.
/// </summary>
/// <remarks>
/// It behaves exactly like a <see cref="ParserException"/> for every consumer
/// (editor markers, <c>kmila check</c> exit codes, tests catching
/// <see cref="ParserException"/>). The distinction only matters where the engine
/// deliberately "blackboxes" a construct it cannot synthesise — most notably the
/// generate-block skip-on-failure in <c>Architecture</c>: a genuine validation
/// rejection must surface to the user, while a synthesis-complexity failure is
/// skipped. Throwing this subtype lets that site re-throw validation errors and
/// swallow only the rest.
/// </remarks>
public sealed class ValidationException : ParserException
{
    /// <summary>Message + 0-based token location (converted to 1-based like the base).</summary>
    public ValidationException(string message, Token? token) : base(message, token) { }

    /// <summary>Message + explicit 1-based line/column.</summary>
    public ValidationException(string message, int line, int col) : base(message, line, col) { }
}
