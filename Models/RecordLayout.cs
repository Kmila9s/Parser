namespace Parser.Models;

/// <summary>How a record field's bits encode its value (see <c>Interpreter.Models.FieldCodec</c>).</summary>
public enum FieldKind
{
    /// <summary><c>std_logic</c>/<c>bit</c>: one character.</summary>
    Logic,
    /// <summary>Vectors (<c>std_logic_vector</c>, <c>signed</c>, …) and arrays: the characters as stored.</summary>
    Vector,
    /// <summary><c>integer</c>/<c>natural</c>/ranged integer: two's complement (or unsigned) binary.</summary>
    Integer,
    /// <summary><c>boolean</c>: <c>'1'</c> = true.</summary>
    Boolean,
    /// <summary>Enumeration: binary index of the literal (<see cref="Variable.EnumLiterals"/> of the field's shape).</summary>
    Enum,
    /// <summary>Nested record: the field's own <see cref="RecordLayout"/>.</summary>
    Record,
}

/// <summary>
/// One field of a record: where its bits sit inside the record's MSB-first value
/// string and how they are interpreted.
/// </summary>
/// <param name="Name">Field name as declared.</param>
/// <param name="Offset">Start position in the record value string (0 = leftmost character).</param>
/// <param name="Width">Number of characters the field occupies.</param>
/// <param name="Kind">Encoding of the field's bits.</param>
/// <param name="Shape">
/// Template <see cref="Variable"/> of the field's subtype (type, declared range,
/// element size, nested <see cref="Variable.Layout"/>), created by the same
/// subtype-indication parser as object declarations, so indexing inside a field
/// (<c>r.d(2)</c>, <c>r.mem(i)</c>) reuses the vector/array rules unchanged.
/// </param>
/// <param name="Signed">For <see cref="FieldKind.Integer"/>: two's complement (otherwise unsigned).</param>
public sealed record FieldSlot(
    string Name,
    int Offset,
    int Width,
    FieldKind Kind,
    Variable Shape,
    bool Signed = false);

/// <summary>
/// Bit layout of a record type: fields packed in declaration order, the first
/// field in the leftmost (most significant) characters — the order of a
/// positional aggregate. A record object is stored as one vector of
/// <see cref="Width"/> characters; every field read or write is a span of it.
/// </summary>
public sealed class RecordLayout
{
    /// <summary>Record type name.</summary>
    public string TypeName { get; }

    /// <summary>Fields in declaration order.</summary>
    public IReadOnlyList<FieldSlot> Fields { get; }

    /// <summary>Total width in characters.</summary>
    public int Width { get; }

    /// <summary>Packs <paramref name="fields"/> (offsets are assigned here, in order).</summary>
    public RecordLayout(string typeName, IEnumerable<FieldSlot> fields)
    {
        TypeName = typeName;
        var packed = new List<FieldSlot>();
        int offset = 0;
        foreach (var f in fields)
        {
            packed.Add(f with { Offset = offset });
            offset += f.Width;
        }
        Fields = packed;
        Width = offset;
    }

    /// <summary>
    /// Non-record fields in declaration order, nested records flattened: dotted
    /// path below the record (<c>a.lo</c>), absolute offset and the field. The
    /// single field flattening for views (VCD scopes, waveform rows).
    /// </summary>
    public IEnumerable<(string Path, int Offset, FieldSlot Field)> Leaves(string prefix = "", int baseOffset = 0)
    {
        foreach (var f in Fields)
        {
            string path = prefix.Length == 0 ? f.Name : $"{prefix}.{f.Name}";
            if (f.Kind == FieldKind.Record && f.Shape.Layout is { } nested)
                foreach (var leaf in nested.Leaves(path, baseOffset + f.Offset)) yield return leaf;
            else
                yield return (path, baseOffset + f.Offset, f);
        }
    }

    /// <summary>The field called <paramref name="name"/> (case-insensitive), or null.</summary>
    public FieldSlot? Field(string name) => IndexOf(name) is int i and >= 0 ? Fields[i] : null;

    /// <summary>Position of the field called <paramref name="name"/> (case-insensitive), or -1.</summary>
    public int IndexOf(string name)
    {
        for (int i = 0; i < Fields.Count; i++)
            if (Fields[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
