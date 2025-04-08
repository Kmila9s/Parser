using static Parser.Models.Constants;

namespace Parser.Models;
public class Variable {
    public string Name { get; set; } = string.Empty;
    private DATATYPES _dataType = DATATYPES.UNKNOWN;
    public DATATYPES Type { get => _dataType; set{
        _dataType = value;
        switch(value){
            case DATATYPES.STD_LOGIC:
                Value = "U";
            break;
            case DATATYPES.STD_LOGIC_VECTOR:
                Value = "U";
            break;
            default:
                Value = "-";
            break;
        }
    } }
    public int Size { get; set; } = 0;
    public object Value { get; set; } = new();
    public Variable(string Name){ this.Name = Name;}
    public Variable(){ this.Name = string.Empty;}
    public static implicit operator Variable(string Name) => new(Name);
    public override bool Equals(object? obj)
    {
        if(obj is Variable var)
            return var.Name == this.Name;
        if(obj is string varName)
            return varName == this.Name;
        return false;
    }
    public override int GetHashCode()
    {
        return this.Name?.GetHashCode() ?? 0;
    }
    public static bool operator ==(string p, Variable var) => p == var.Name;
    public static bool operator ==(Variable var, string p) => p == var.Name;
    public static bool operator !=(string p, Variable var) => !(p == var.Name);
    public static bool operator !=(Variable var, string p) => !(p == var.Name);
}