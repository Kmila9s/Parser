using System.Text.RegularExpressions;
using Parser.Interfaces;
using Parser.Repositories;

namespace Parser.Models;
public class Operation : IOperation {
    public object Result { get; private set; } = new();
    public object[] Items { get; private set; }
    private string[] _data;
    private int _currentSyntetizeIndex = 0, _currentExecuteIndex = 0;
    public HashSet<Variable> Variables { get; private set; }
    private readonly Regex _variableRegex, _operators, _logicOperators;
    public object Execute(){
        object result = new();
        while(_currentExecuteIndex < Items.Length){
            var item = Items[_currentExecuteIndex];
            if(item is Variable variable){
                if(Items[_currentExecuteIndex + 1] is Operator operation){
                    switch(operation.OperatorType){
                        case Constants.OperatorType.ASSIGNATION:
                            _currentExecuteIndex += 2;
                            variable.Value = Execute();
                            return variable;
                        case Constants.OperatorType.LOGIC_OPERATION:
                            if(Items[_currentExecuteIndex + 2] is Variable rightVariable){
                                result = new Variable("Temp", operation.Execute(variable, rightVariable), rightVariable.Type);
                                _currentExecuteIndex += 2;
                            }
                        break;
                    }
                }
            }else if(item is List<object> collection){

            }else if(item is Operator operation){
                if(Items[_currentExecuteIndex + 1] is Variable rightVariable){
                    Variable leftVariable = (Variable)result;
                    result = operation.Execute(leftVariable, rightVariable);
                    _currentExecuteIndex += 2;
                }
            }
            _currentExecuteIndex++;
        }
        return result;
    }
    public object Execute(Variable leftSideOperator, Variable rightSideOperator) => throw new NotImplementedException();
    public Operation(string RawData, HashSet<Variable> Variables){
        RegexOptions optionsCompiled = RegexOptions.Compiled;
        this.Variables = Variables;
        _variableRegex = new(Constants.IDENTIFIERS, optionsCompiled);
        _operators = new Regex(Constants.OPERATORS, optionsCompiled);
        _logicOperators = new Regex(Constants.LOGIC_OPERATORS, optionsCompiled);
        Tokenizer tokenizer = new(string.Empty);
        _data = tokenizer.TokenizeLineReturn(RawData);
        Items = Syntetize().ToArray();
    }
    public List<object> Syntetize(){
        List<object> items = new();
        while(_currentSyntetizeIndex < _data.Length){
            var currentItem = _data[_currentSyntetizeIndex];
            object item = new();
            if(_operators.IsMatch(currentItem.ToLower()) || _logicOperators.IsMatch(currentItem.ToLower())){
                item = new Operator(currentItem);
            }else if(_variableRegex.IsMatch(currentItem) && Variables.Contains(currentItem)){
                item = Variables.First(p => p.Name.ToLower().Equals(currentItem.ToLower()));
            }else if(currentItem.Equals("(")){
                _currentSyntetizeIndex ++;
                item = Syntetize();
            }else if(currentItem.Equals(")")){
                return items;
            }
            items.Add(item);
            _currentSyntetizeIndex++;
        }
        return items;
    }
}