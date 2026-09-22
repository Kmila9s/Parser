namespace Parser.Services;

/// <summary>
/// Process-wide catalogue of user-defined VHDL type names the build pipeline
/// has resolved. Populated by the host (typically
/// <c>ProgramBuilder.EnsureLibrariesCompiledForBuildAsync</c>) right before
/// the parser walks the file, from the <see cref="Type"/>-kind symbols
/// returned by <c>LibraryCompiler</c>. Consulted by
/// <see cref="Parser.Models.Constants.FindTypoSuggestion"/> so a user who
/// imports a custom type via <c>use my_lib.my_pkg.all</c> and then misspells
/// it (e.g. <c>coeficient_array_t</c> vs <c>coefficient_array_t</c>) gets a
/// "did you mean" hint instead of an opaque UNKNOWN data type.
/// </summary>
/// <remarks>
/// Singleton-by-convention mirrors <c>FunctionRegistry</c> in
/// <c>Interpreter.Services</c>. Comparison is case-insensitive — VHDL
/// identifiers are case-folded.
/// </remarks>
public sealed class UserTypeRegistry
{
    private static readonly UserTypeRegistry _instance = new();
    /// <summary>Process-wide singleton.</summary>
    public static UserTypeRegistry Instance => _instance;

    private readonly HashSet<string> _names =
        new(StringComparer.OrdinalIgnoreCase);

    private UserTypeRegistry() { }

    /// <summary>Records a user-defined type name. No-op for null / empty.</summary>
    public void Register(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _names.Add(name);
    }

    /// <summary>
    /// True when <paramref name="name"/> matches a registered type, regardless
    /// of casing. Used by <see cref="Parser.Models.Constants.FindTypoSuggestion"/>
    /// to short-circuit suggestions when the user typed an exact match.
    /// </summary>
    public bool IsKnown(string name)
        => !string.IsNullOrEmpty(name) && _names.Contains(name);

    /// <summary>
    /// Every registered type name. Iteration order is not guaranteed.
    /// </summary>
    public IEnumerable<string> All => _names;

    /// <summary>
    /// Drops every entry. Callers (build orchestration code) invoke this at
    /// the start of each build pass so stale entries from a previously-built
    /// project can't leak into the next one's suggestions.
    /// </summary>
    public void Clear() => _names.Clear();
}
