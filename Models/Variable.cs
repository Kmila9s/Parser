using System.Collections.Specialized;
using static Parser.Models.Constants;

namespace Parser.Models;

/// <summary>
/// Represents a VHDL variable, signal, constant, or port.
/// </summary>
public class Variable {
    private string _name = string.Empty;
    private string _nameLower = string.Empty;
    /// <summary>
    /// Gets or sets the name of the variable.
    /// </summary>
    /// <remarks>
    /// F-G-08 pass 5 (2026-07-22): backing field + <c>_nameLower</c> cache.
    /// Pre-pass-5 the case-insensitive <see cref="Equals"/> /
    /// <see cref="GetHashCode"/> allocated a fresh <c>ToLower()</c> string per
    /// call. On the elaborated capstone every <c>HashSet&lt;Variable&gt;.Contains</c>
    /// (thousands per delta cycle) took 2–3 allocations. Cache is refreshed
    /// on every <c>Name</c> setter call so late renames stay coherent.
    /// </remarks>
    public string Name
    {
        get => _name;
        set
        {
            _name = value ?? string.Empty;
            _nameLower = _name.ToLowerInvariant();
        }
    }
    /// <summary>
    /// Gets or sets whether this is a stub variable (created for undeclared identifiers).
    /// </summary>
    public bool IsStub { get; set; } = false;

    /// <summary>
    /// True when <see cref="Type"/> is a placeholder, not the object's real type —
    /// the result of a function known only by its signature. Type/width validation
    /// skips such values; evaluation is unaffected (unlike <see cref="IsStub"/>).
    /// </summary>
    public bool HasPlaceholderType { get; set; }

    /// <summary>
    /// Gets or sets the custom type name for record/array types.
    /// Used when the variable is declared with a user-defined type.
    /// v0.15.0: Added to support record type field access.
    /// </summary>
    public string? CustomTypeName { get; set; } = null;

    /// <summary>
    /// The name of the user-defined ENUMERATION type this signal/variable was
    /// declared with (e.g. `state_type`), or null if not an enum. Kept separate
    /// from <see cref="CustomTypeName"/> (which gates record/array handling and the
    /// case numeric/exhaustiveness checks) so enum typing never disturbs those.
    /// Used by the type-precise cross-enum case check (precise-C10).
    /// </summary>
    public string? EnumTypeName { get; set; } = null;

    /// <summary>
    /// Literals of <see cref="EnumTypeName"/> in declaration order; a value's
    /// position in this list is its encoding (record fields, VCD).
    /// </summary>
    public IReadOnlyList<string>? EnumLiterals { get; set; } = null;

    /// <summary>
    /// Bit layout when this object is of a record type (its value is the packed
    /// fields, first field leftmost); null otherwise.
    /// </summary>
    public RecordLayout? Layout { get; set; } = null;

    /// <summary>
    /// For arrays (<see cref="ElementSize"/> &gt; 0): template of one element's
    /// subtype (record layout, vector range), so a path like <c>m(i).lo</c> can
    /// continue past the element. Null for arrays of scalars and non-arrays.
    /// </summary>
    public Variable? ElementShape { get; set; } = null;

    /// <summary>True for a number (integer/natural), false for an enumeration stored as INTEGER.</summary>
    public bool IsInteger => _dataType is DATATYPES.INTEGER or DATATYPES.NATURAL && EnumLiterals is null && EnumTypeName is null;

    /// <summary>
    /// A fresh object with this one's name, current value, type, size and
    /// declared shape — the per-call copy of a function/procedure local.
    /// </summary>
    public Variable CloneShaped()
    {
        var clone = new Variable(Name, Value?.ToString() ?? string.Empty, Type) { Size = Size };
        clone.CopyShapeFrom(this);
        return clone;
    }

    /// <summary>
    /// Copies the declared shape of <paramref name="other"/> — custom/enum type
    /// names, array geometry, declared range and record layout — onto this object.
    /// The single way to give a re-created local (function/procedure frames,
    /// materialised constants) the same indexing/field rules as its declaration.
    /// </summary>
    public void CopyShapeFrom(Variable other)
    {
        CustomTypeName = other.CustomTypeName;
        EnumTypeName = other.EnumTypeName;
        EnumLiterals = other.EnumLiterals;
        ElementSize = other.ElementSize;
        ArrayLength = other.ArrayLength;
        ElementShape = other.ElementShape;
        Layout = other.Layout;
        if (other.HasDeclaredRange)
            SetDeclaredRange(other.LeftBound!.Value, other.RightBound!.Value, other.IsDownto);
    }

    /// <summary>
    /// For variables declared with a user array type (e.g. `signal mem : rom_t`
    /// where `rom_t` is `array(0 to 7) of std_logic_vector(7 downto 0)`), the
    /// per-element width in bits. Stays at 0 for scalar / plain vector signals,
    /// which means "treat indexed access as single-bit" (the legacy path).
    /// When set, indexed reads via DynamicIndex / DeferredIndex extract an
    /// ElementSize-wide slice instead of a single character, matching VHDL
    /// element-access semantics for arrays of vectors.
    /// </summary>
    public int ElementSize { get; set; } = 0;

    /// <summary>
    /// Number of elements in the array (e.g. 8 for `array(0 to 7) of ...`).
    /// 0 for non-array variables.
    /// </summary>
    public int ArrayLength { get; set; } = 0;

    /// <summary>
    /// Declared range left bound (VHDL <c>'left</c>) — the index of the leftmost
    /// element of the declaration, e.g. 7 for <c>(7 downto 0)</c> or 0 for
    /// <c>(0 to 7)</c>. <c>null</c> means the declared range was never recorded;
    /// callers then fall back to the conventional <c>(Size-1 downto 0)</c> form.
    /// Range bug fix (2026-10): stored so index/slice/attribute resolution honours
    /// ascending (<c>to</c>) and non-zero-low-bound ranges instead of assuming
    /// 0-based descending.
    /// </summary>
    public int? LeftBound { get; set; } = null;

    /// <summary>Declared range right bound (VHDL <c>'right</c>). See <see cref="LeftBound"/>.</summary>
    public int? RightBound { get; set; } = null;

    /// <summary>
    /// Declared range direction: <c>true</c> for <c>downto</c> (the default),
    /// <c>false</c> for <c>to</c>. Only meaningful once <see cref="HasDeclaredRange"/>.
    /// </summary>
    public bool IsDownto { get; set; } = true;

    /// <summary>True when an explicit declared range (both bounds) has been recorded.</summary>
    public bool HasDeclaredRange => LeftBound.HasValue && RightBound.HasValue;

    /// <summary>
    /// Records the declared range on this variable. Pass the left bound, right
    /// bound and direction exactly as written in VHDL (e.g. <c>(7, 0, true)</c>
    /// for <c>(7 downto 0)</c>, <c>(0, 7, false)</c> for <c>(0 to 7)</c>).
    /// </summary>
    public void SetDeclaredRange(int left, int right, bool isDownto)
    {
        LeftBound = left;
        RightBound = right;
        IsDownto = isDownto;
    }

    /// <summary>
    /// Maps a VHDL index into a 0-based position in the MSB-first value string.
    /// With a recorded range: descending → <c>left - index</c>; ascending →
    /// <c>index - left</c>. Without one, falls back to <c>valueLength-1-index</c>,
    /// which is byte-identical to the legacy behaviour for <c>(N-1 downto 0)</c>
    /// vectors — so only <c>to</c>/offset ranges change. <paramref name="valueLength"/>
    /// is the current length of the value string the offset indexes into.
    /// </summary>
    public int IndexToOffset(int index, int valueLength) =>
        HasDeclaredRange ? OffsetInRange(LeftBound!.Value, IsDownto, index) : valueLength - 1 - index;

    /// <summary>
    /// Position of <paramref name="index"/> counted from the leftmost element of
    /// a range that starts at <paramref name="left"/> and runs
    /// <c>downto</c> (<paramref name="isDownto"/>) or <c>to</c>. The single
    /// index→position rule for vectors and arrays.
    /// </summary>
    public static int OffsetInRange(int left, bool isDownto, int index) =>
        isDownto ? left - index : index - left;

    /// <summary>
    /// For a VHDL slice with bounds <paramref name="high"/> (numerically larger)
    /// and <paramref name="low"/> (numerically smaller), returns the start
    /// position in the MSB-first value string and the slice width. The start is
    /// whichever bound maps to the smaller storage offset, so the extracted
    /// substring stays in declaration (left-to-right) order for both directions.
    /// </summary>
    public (int startPos, int width) SliceToSpan(int high, int low, int valueLength)
    {
        int width = Math.Abs(high - low) + 1;
        int offHigh = IndexToOffset(high, valueLength);
        int offLow = IndexToOffset(low, valueLength);
        return (Math.Min(offHigh, offLow), width);
    }

    private DATATYPES _dataType = DATATYPES.UNKNOWN;
    /// <summary>
    /// Gets or sets the data type of the variable.
    /// </summary>
    public DATATYPES Type { get => _dataType; set{
        _dataType = value;
        switch(value){
            case DATATYPES.STD_LOGIC:
                if(Value == null)
                    Value = "U";
            break;
            case DATATYPES.STD_LOGIC_VECTOR:
                // Don't initialize _value here - let Size setter handle it
                // If we set _value = "U" here, Size setter will see non-null value
                // and incorrectly calculate size as 1 instead of declared vector width
            break;
            default:
                Value = Value;
            break;
        }
    } }
    /// <summary>
    /// Converts a string representation of a data type to its corresponding DATATYPES enum value.
    /// </summary>
    /// <param name="type">The string value of the data type.</param>
    /// <returns>The <see cref="DATATYPES"/> enum member.</returns>
    public static DATATYPES GetDatatype(string type) {
        string typeLower = type.ToLower();
        return typeLower switch {
            "std_logic" => DATATYPES.STD_LOGIC,
            "std_ulogic" => DATATYPES.STD_LOGIC,
            "signed" => DATATYPES.STD_LOGIC_VECTOR,
            "unsigned" => DATATYPES.STD_LOGIC_VECTOR,
            "std_logic_vector" => DATATYPES.STD_LOGIC_VECTOR,
            "std_ulogic_vector" => DATATYPES.STD_LOGIC_VECTOR,
            "bit_vector" => DATATYPES.STD_LOGIC_VECTOR,
            "boolean_vector" => DATATYPES.STD_LOGIC_VECTOR,
            "integer_vector" => DATATYPES.STD_LOGIC_VECTOR,
            "natural" => DATATYPES.NATURAL,
            "positive" => DATATYPES.NATURAL, // positive is subtype of natural
            "integer" => DATATYPES.INTEGER,
            "bit" => DATATYPES.BIT,
            "boolean" => DATATYPES.BOOLEAN,
            "hex" => DATATYPES.HEX,
            "range" => DATATYPES.RANGE,
            "character" => DATATYPES.CHARACTER,
            "string" => DATATYPES.CHARACTER, // treat string as character array for now
            "time" => DATATYPES.INTEGER, // treat time as integer for simplicity
            "real" => DATATYPES.INTEGER, // treat real as integer for simplicity (limited support)
            _ => DATATYPES.UNKNOWN
        };
    }
    /// <summary>
    /// Returns the string representation of the variable's value.
    /// </summary>
    /// <returns>The value as a string, or an empty string if null.</returns>
    public override string ToString() =>
        Convert.ToString(Value ?? string.Empty) ?? string.Empty;
    /// <summary>Backing field for the <see cref="Size"/> property.</summary>
    private int _size = 0;
    /// <summary>
    /// Gets or sets the size of the variable, typically for vector types.
    /// Setting the size for a STD_LOGIC_VECTOR initializes its value.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException">Thrown if the assigned value's length does not match the size for non-port vectors.</exception>
    public int Size { get => _size; set{
        _size = value;
#if DEBUG
        bool isDebugPort = Name != null && (Name == "data_in" || Name == "data_out");
        if (isDebugPort)
            Console.WriteLine($"[Variable.Size setter] '{Name}' setting _size={value}, _dataType={_dataType}, _value={(object?)_value}");
#endif
        if (_dataType == DATATYPES.STD_LOGIC_VECTOR)
        {
            string arr = new('U', _size);
                if (_value == null)
                {
#if DEBUG
                    if (isDebugPort)
                        Console.WriteLine($"[Variable.Size setter] '{Name}' _value is null, setting Value to arr (length {arr.Length})");
#endif
                    Value = arr;
                }
                else
                {
                    var val = Convert.ToString(Value) == "System.Object" ? "U" : Convert.ToString(Value);
                    // Handle (others => 'X') aggregate pattern
                    if (val != null && val.StartsWith("(others=>"))
                    {
                        // Extract the fill character and expand to size
                        char fillChar = val.Contains("0") ? '0' : (val.Contains("1") ? '1' : 'U');
                        Value = new string(fillChar, value);
                        _size = value;
                    }
                    // Handle (concat:...) expression pattern - couldn't evaluate at parse time
                    else if (val != null && val.StartsWith("(concat:"))
                    {
                        // Use the declared size with undefined value (U)
                        Value = new string('U', value);
                        _size = value;
                    }
                    else if (value != 0)
                    {
                        if (val?.Length != value && !IsPort && val != "U")
                        {
                            // Adjust value to match declared size
                            if (val?.Length > value)
                            {
                                // Truncate
                                Value = val.Substring(val.Length - value);
                            }
                            else if (val?.Length < value)
                            {
                                // Pad with U
                                Value = val.PadLeft(value, 'U');
                            }
                            _size = value;
                        }
                        else
                        {
                            _size = val?.Length ?? value;
#if DEBUG
                            if (isDebugPort)
                                Console.WriteLine($"[Variable.Size setter] '{Name}' setting _size from val.Length={val?.Length} or value={value}, final _size={_size}");
#endif
                        }
                    }
                }
        }
#if DEBUG
        if (isDebugPort)
            Console.WriteLine($"[Variable.Size setter] '{Name}' FINAL _size={_size}");
#endif
    } }
    /// <summary>Backing field for the <see cref="Value"/> property.</summary>
    private object? _value = null;
    /// <summary>
    /// Occurs when the value of the variable changes.
    /// </summary>
    public event Action<Variable>? OnValueChange;
    /// <summary>
    /// Gets or sets the value of the variable.
    /// </summary>
    public object Value
    {
        get => _value ?? new();
        set
        {
            object transformed;
            switch (_dataType)
            {
                case DATATYPES.STD_LOGIC:
                case DATATYPES.STD_LOGIC_VECTOR:
                    if (value == null)
                    {
                        transformed = "U";
                    }
                    // F-G-08 (2026-07-15): fast path — engine-driven writes
                    // land as already-clean strings. Avoid two allocations
                    // (Replace × 2) per assignment when the input has no
                    // quotes to strip. Falls back to the general path only
                    // for source-parsed literals like `"1010"` or `'1'`.
                    else if (value is string s)
                    {
                        if (s.Length == 0 || (s.IndexOf('"') < 0 && s.IndexOf('\'') < 0))
                            transformed = s;
                        else
                            transformed = s.Replace("\"", "").Replace("'", "");
                    }
                    else
                    {
                        transformed = value.ToString()?.Replace("\"", "").Replace("'", "") ?? throw new Exception("Malformed variable");
                    }
                    break;
                default:
                    transformed = value;
                    break;
            }
            if (!Equals(_value, transformed))
            {
                _value = transformed;
                OnValueChange?.Invoke(this);
            }
        }
    }
    /// <summary>
    /// Gets or sets whether this variable represents a port in an entity declaration.
    /// </summary>
    public bool IsPort { get; set; } = false;
    /// <summary>
    /// True when this row came from a <c>constant</c> declaration (architecture-
    /// local or package). Constants share storage with signals but must be
    /// excluded from VCD dumps because they never change over sim-time and
    /// waveform viewers should not carry them as $var entries.
    /// Audit F-G-03 (2026-07-09).
    /// </summary>
    public bool IsConstant { get; set; } = false;
    /// <summary>
    /// Gets or sets the direction of the port (IN, OUT, INOUT, BUFFER).
    /// Only relevant when <see cref="IsPort"/> is true.
    /// </summary>
    public PortType PortType { get; set; } = PortType.IN;
    /// <summary>
    /// Initializes a new instance of the <see cref="Variable"/> class with a specified name.
    /// </summary>
    /// <param name="Name">The name of the variable.</param>
    public Variable(string Name){ this.Name = Name;}
    /// <summary>
    /// Initializes a new instance of the <see cref="Variable"/> class with an empty name.
    /// </summary>
    public Variable(){ this.Name = string.Empty;}
    /// <summary>
    /// Initializes a new instance of the <see cref="Variable"/> class with name, value, type, and optional size.
    /// </summary>
    /// <param name="Name">The name of the variable.</param>
    /// <param name="Value">The initial value.</param>
    /// <param name="Type">The VHDL data type.</param>
    /// <param name="Size">The size for vector types (default 0).</param>
    public Variable(string Name, object Value, DATATYPES Type, int Size = 0){
        this.Name = Name;
        this.Type = Type;
        this.Value = Value;
        this.Size = Size;
    }
    /// <summary>
    /// Allows implicit conversion from a string name to a Variable instance.
    /// </summary>
    /// <param name="Name">The variable name.</param>
    public static implicit operator Variable(string Name) => new(Name);
    /// <summary>
    /// Determines whether the specified object is equal to this variable by comparing names (case-insensitive).
    /// Supports comparison with other <see cref="Variable"/> instances or <see cref="string"/> names.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>true if the names match (case-insensitive); otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if(obj is Variable var)
            return var._nameLower == this._nameLower;
        if(obj is string varName)
            return varName.ToLowerInvariant() == this._nameLower;
        return false;
    }
    /// <summary>
    /// Serves as the default hash function.
    /// Returns a case-insensitive hash code to match the Equals behavior for VHDL identifiers.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return this._nameLower.GetHashCode();
    }
    /// <summary>Case-insensitive equality: string == Variable.</summary>
    public static bool operator ==(string p, Variable var) => var?._nameLower != null && p?.ToLowerInvariant() == var._nameLower;
    /// <summary>Case-insensitive equality: Variable == string.</summary>
    public static bool operator ==(Variable var, string p) => var?._nameLower != null && p?.ToLowerInvariant() == var._nameLower;
    /// <summary>Equality comparison between two Variable instances by name.</summary>
    public static bool operator ==(Variable var, Variable comp) => var?.Name == comp?.Name;
    /// <summary>Inequality comparison between two Variable instances by name.</summary>
    public static bool operator !=(Variable var, Variable comp) => var?.Name != comp?.Name;
    /// <summary>Case-insensitive inequality: string != Variable.</summary>
    public static bool operator !=(string p, Variable var) => !(var?.Name != null && p?.ToLower() == var.Name.ToLower());
    /// <summary>Case-insensitive inequality: Variable != string.</summary>
    public static bool operator !=(Variable var, string p) => !(var?.Name != null && p?.ToLower() == var.Name.ToLower());
}