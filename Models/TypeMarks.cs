namespace Parser.Models;

/// <summary>Signedness of a vector's numeric interpretation (numeric_std / std_logic_arith).</summary>
public enum Signedness { None, Unsigned, Signed }

/// <summary>
/// The single rule over declared type marks (<see cref="Variable.TypeMark"/>):
/// which types are resolved (several drivers allowed, IEEE 1164 resolution)
/// and which vector types are numerically signed or unsigned.
/// </summary>
public static class TypeMarks
{
    private static readonly HashSet<string> Unresolved = new(StringComparer.OrdinalIgnoreCase)
    {
        "std_ulogic", "std_ulogic_vector", "bit", "bit_vector", "boolean", "boolean_vector",
        "integer", "natural", "positive", "integer_vector", "real", "time", "character", "string",
        "u_signed", "u_unsigned", "unresolved_signed", "unresolved_unsigned",
    };

    private static readonly HashSet<string> SignedMarks = new(StringComparer.OrdinalIgnoreCase)
    {
        "signed", "u_signed", "unresolved_signed",
    };

    private static readonly HashSet<string> UnsignedMarks = new(StringComparer.OrdinalIgnoreCase)
    {
        "unsigned", "u_unsigned", "unresolved_unsigned",
    };

    /// <summary>
    /// True when an object of <paramref name="v"/>'s type may have several drivers:
    /// std_logic, std_logic_vector, signed, unsigned (and their subtypes). An
    /// enumeration or a type mark known to be unresolved is not; an unknown
    /// mark is treated as resolved (no false «several sources» error).
    /// </summary>
    public static bool IsResolved(Variable v) =>
        v.EnumTypeName is null && (v.TypeMark is null || !Unresolved.Contains(v.TypeMark));

    private static readonly HashSet<string> BitVectorMarks = new(StringComparer.OrdinalIgnoreCase)
    {
        "std_logic_vector", "std_ulogic_vector", "bit_vector",
    };

    /// <summary>
    /// True for the closely related one-dimensional vectors of bits (std_logic_vector,
    /// std_ulogic_vector, bit_vector, signed, unsigned and their unresolved forms): a
    /// type conversion between two of them keeps the bits.
    /// </summary>
    public static bool IsBitVector(string typeMark) =>
        BitVectorMarks.Contains(typeMark) || SignedMarks.Contains(typeMark) || UnsignedMarks.Contains(typeMark);

    /// <summary>The numeric interpretation of a vector of <paramref name="typeMark"/>.</summary>
    public static Signedness SignednessOf(string? typeMark) =>
        typeMark is null ? Signedness.None
        : SignedMarks.Contains(typeMark) ? Signedness.Signed
        : UnsignedMarks.Contains(typeMark) ? Signedness.Unsigned
        : Signedness.None;
}
