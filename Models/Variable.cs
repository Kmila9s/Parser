using System.Collections.Specialized;
using static Parser.Models.Constants;

namespace Parser.Models;

/// <summary>
/// Represents a VHDL variable, signal, constant, or port.
/// </summary>
public class Variable {
    /// <summary>
    /// Gets or sets the name of the variable.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets whether this is a stub variable (created for undeclared identifiers).
    /// </summary>
    public bool IsStub { get; set; } = false;
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
    public override string ToString() => 
        Convert.ToString(Value ?? string.Empty) ?? string.Empty;
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
                        transformed = "U";
                    else
                        transformed = value.ToString()?.Replace("\"", "").Replace("'", "") ?? throw new Exception("Malformed variable");
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
    public bool IsPort { get; set; } = false;
    public PortType PortType { get; set; } = PortType.IN;
    public Variable(string Name){ this.Name = Name;}
    public Variable(){ this.Name = string.Empty;}
    public Variable(string Name, object Value, DATATYPES Type, int Size = 0){
        this.Name = Name;
        this.Type = Type;
        this.Value = Value;
        this.Size = Size;
    }
    public static implicit operator Variable(string Name) => new(Name);
    public override bool Equals(object? obj)
    {
        if(obj is Variable var)
            return var.Name.ToLower() == this.Name.ToLower();
        if(obj is string varName)
            return varName.ToLower() == this.Name.ToLower();
        return false;
    }
    /// <summary>
    /// Serves as the default hash function.
    /// Returns a case-insensitive hash code to match the Equals behavior for VHDL identifiers.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return this.Name?.ToLower().GetHashCode() ?? 0;
    }
    public static bool operator ==(string p, Variable var) => var?.Name != null && p?.ToLower() == var.Name.ToLower();
    public static bool operator ==(Variable var, string p) => var?.Name != null && p?.ToLower() == var.Name.ToLower();
    public static bool operator ==(Variable var, Variable comp) => var?.Name == comp?.Name;
    public static bool operator !=(Variable var, Variable comp) => var?.Name != comp?.Name;
    public static bool operator !=(string p, Variable var) => !(var?.Name != null && p?.ToLower() == var.Name.ToLower());
    public static bool operator !=(Variable var, string p) => !(var?.Name != null && p?.ToLower() == var.Name.ToLower());
}