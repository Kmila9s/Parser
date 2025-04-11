using System.Text.RegularExpressions;
using Parser.Interfaces;
using static Parser.Models.Constants;

namespace Parser.Models;
public class Operator : IOperation{
    public OperatorType OperatorType { get; private set; }
    private readonly Regex _assignationRegex, _arithRegex, _logicRegex;
    public string OperatorName { get; private set; } = string.Empty;
    public Operator(string Operator){
        this.OperatorName = Operator;
        _assignationRegex = new(ASSIGNATION_OPERATORS);
        _arithRegex = new(ARITH_OPERATORS);
        _logicRegex = new(LOGIC_OPERATORS);
        this.OperatorType = DefineOperatorType(Operator);
    }
    private OperatorType DefineOperatorType(string Operator) =>
        _assignationRegex.IsMatch(Operator) ? 
            OperatorType.ASSIGNATION : 
        _arithRegex.IsMatch(Operator) ?
            OperatorType.ARITH_OPERATION :
        _logicRegex.IsMatch(Operator) ?
            OperatorType.LOGIC_OPERATION :
            OperatorType.UNKNOWN;
    public object Execute() => throw new NotImplementedException();
    public object Execute(Variable leftSideOperator, Variable rightSideOperator){
        if(leftSideOperator.Type != rightSideOperator.Type)
            throw new ParserException("Datatypes do not match");
        object result = new();
        switch(OperatorType){
            case OperatorType.ASSIGNATION:
                result = rightSideOperator.Value;
            break;
            case OperatorType.LOGIC_OPERATION:
                switch(leftSideOperator.Type){
                    case DATATYPES.STD_LOGIC:
                        result = SolveLogicOperation(leftSideOperator, rightSideOperator);
                    break;
                    case DATATYPES.STD_LOGIC_VECTOR:
                        result = SolveVectorLogicOperation(leftSideOperator, rightSideOperator);
                    break;
                }
                
            break;
            default:
                throw new NotImplementedException();
        }
        return result;
    }
    private object SolveLogicOperation(Variable var1, Variable var2){
        char a = Convert.ToChar(var1.Value);
        char b = Convert.ToChar(var2.Value);
        char result;
        switch (OperatorName.ToLower()) {
            case "and":
                result = (a == '1' && b == '1') ? '1' : '0';
                break;
            case "or":
                result = (a == '1' || b == '1') ? '1' : '0';
                break;
            case "xor":
                result = (a != b) ? '1' : '0';
                break;
            case "nand":
                result = (a == '1' && b == '1') ? '0' : '1';
                break;
            case "nor":
                result = (a == '0' && b == '0') ? '1' : '0';
                break;
            default:
                result = 'U';
            break;
        }
        return result;
    }
    private object SolveVectorLogicOperation(Variable v1, Variable v2){
        if(v1.Size != v2.Size)
            throw new ParserException("Vectors do not match in size");
        string result = "", 
        vectorString1 = (string)v1.Value,
        vectorString2 = (string)v2.Value;
        for(int i = 0; i < v1.Size; i++){
            Variable valueA = new("tempA", vectorString1[i], DATATYPES.STD_LOGIC), 
            valueB = new("tempB", vectorString2[i], DATATYPES.STD_LOGIC);
            result += (string)SolveLogicOperation(valueA, valueB);
        }
        return result;
    }
}