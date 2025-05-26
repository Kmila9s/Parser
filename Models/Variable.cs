using System.Collections.Specialized;
using static Parser.Models.Constants;

namespace Parser.Models;
public class Variable {
    public string Name { get; set; } = string.Empty;
    private DATATYPES _dataType = DATATYPES.UNKNOWN;
    public DATATYPES Type { get => _dataType; set{
        _dataType = value;
        switch(value){
            case DATATYPES.STD_LOGIC:
                if(Value == null)
                    Value = "U";
            break;
            case DATATYPES.STD_LOGIC_VECTOR:
                if(Value == null)
                    Value = "U";
            break;
            default:
                Value = Value;
            break;
        }
    } }
    public static DATATYPES GetDatatype(string type) =>
        type.ToLower().Equals("std_logic") ?
            DATATYPES.STD_LOGIC :
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
        Convert.ToString(this.Value ?? string.Empty) ?? string.Empty;
    private int _size = 0;
    public int Size { get => _size; set{
        _size = value;
        if (this._dataType == DATATYPES.STD_LOGIC_VECTOR)
        {
            string arr = new('U', _size);
                if (_value == null)
                    _value = arr;
                else
                {
                    if (value != 0)
                    {
                        if (Convert.ToString(_value)?.Length != value)
                            throw new IndexOutOfRangeException();    
                    }
                    _size = Convert.ToString(_value)?.Length ?? value;
                }    
        }
    } }
    private object? _value = null;
    public object Value { get => _value ?? new(); set {
        switch (_dataType)
            {
                case DATATYPES.STD_LOGIC:
                    if (value == null)
                        _value = "U";
                    else
                        _value = value.ToString()?.Replace("\"", "").Replace("'", "") ?? throw new Exception("Malformed variable");
                    break;
                case DATATYPES.STD_LOGIC_VECTOR:
                    if (value == null)
                        _value = "U";
                    else
                        _value = value.ToString()?.Replace("\"", "").Replace("'", "") ?? throw new Exception("Malformed variable");
                    break;
                default:
                    _value = value;
                    break;
            }
    } }
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
    public override int GetHashCode()
    {
        return this.Name?.GetHashCode() ?? 0;
    }
    public static bool operator ==(string p, Variable var) => p.ToLower() == var.Name.ToLower();
    public static bool operator ==(Variable var, string p) => p.ToLower() == var.Name.ToLower();
    public static bool operator !=(string p, Variable var) => !(p.ToLower() == var.Name.ToLower());
    public static bool operator !=(Variable var, string p) => !(p.ToLower() == var.Name.ToLower());
}