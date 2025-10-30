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
                if (_value == null)
                {
                    _value = "U";
                }   
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
    public static DATATYPES GetDatatype(string type) =>
        type.ToLower().Equals("std_logic") ?
            DATATYPES.STD_LOGIC :
        type.ToLower().Equals("signed") ?
            DATATYPES.STD_LOGIC_VECTOR :
        type.ToLower().Equals("unsigned") ?
            DATATYPES.STD_LOGIC_VECTOR :
        type.ToLower().Equals("std_logic_vector") ?
            DATATYPES.STD_LOGIC_VECTOR :
        type.ToLower().Equals("natural") ?
            DATATYPES.NATURAL :
        type.ToLower().Equals("integer") ?
            DATATYPES.INTEGER :
        type.ToLower().Equals("bit") ?
            DATATYPES.BIT :
        type.ToLower().Equals("boolean") ?
            DATATYPES.BOOLEAN :
        type.ToLower().Equals("hex") ?
            DATATYPES.HEX :
        type.ToLower().Equals("range") ?
            DATATYPES.RANGE :
        type.ToLower().Equals("character") ?
            DATATYPES.CHARACTER :
            DATATYPES.UNKNOWN ;
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
        if (_dataType == DATATYPES.STD_LOGIC_VECTOR)
        {
            string arr = new('U', _size);
                if (Value == null)
                    Value = arr;
                else
                {
                    var val = Convert.ToString(Value) == "System.Object" ? "U" : Convert.ToString(Value);
                    if (value != 0)
                    {

                        if (val?.Length != value && !IsPort && val != "U")
                            throw new IndexOutOfRangeException();
                    }
                    _size = val?.Length ?? value;
                }    
        }
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
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return this.Name?.GetHashCode() ?? 0;
    }
    public static bool operator ==(string p, Variable var) => p.ToLower() == var.Name.ToLower();
    public static bool operator ==(Variable var, string p) => p.ToLower() == var.Name.ToLower();
    public static bool operator ==(Variable var, Variable comp) => var.Name == comp.Name;
    public static bool operator !=(Variable var, Variable comp) => var.Name != comp.Name;    
    public static bool operator !=(string p, Variable var) => !(p.ToLower() == var.Name.ToLower());
    public static bool operator !=(Variable var, string p) => !(p.ToLower() == var.Name.ToLower());
}