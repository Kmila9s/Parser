using Parser.Models;

namespace Parser.Interfaces;
public interface IOperation {
    object Execute();
    object Execute(Variable leftSideOperator, Variable rightSideOperator);
}