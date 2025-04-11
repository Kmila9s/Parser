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
                Value = "-";
            break;
        }
    } }
    private int _size = 0;
    public int Size { get => _size; set{
        _size = value;
        if(this._dataType == DATATYPES.STD_LOGIC_VECTOR){
            var arr = "";
            for(int i = 0; i < _size;i++)
                arr += "U";
            Value = arr;
        }
    } }
    public object Value { get; set; } = new();
    public Variable(string Name){ this.Name = Name;}
    public Variable(){ this.Name = string.Empty;}
    public Variable(string Name, object Value, DATATYPES Type, int Size = 0){
        this.Name = Name;
        this.Value = Value;
        this.Type = Type;
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