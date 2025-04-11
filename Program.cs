using System.IO;
using Parser.Models;
using Parser.Repositories;

namespace Parser;

class Program
{
    static void Main(string[] args)
    {
        //Repositories.Parser parser = new("test6.vhdl");
        //parser.ParseV2();
        HashSet<Variable> Variables = new(){
            new("A", '1', Constants.DATATYPES.STD_LOGIC),
            new("Sum", "U", Constants.DATATYPES.STD_LOGIC),
            new("B", '1', Constants.DATATYPES.STD_LOGIC),
            new("Cin", '1', Constants.DATATYPES.STD_LOGIC),
            new("Cout", "U", Constants.DATATYPES.STD_LOGIC),
        };
        Operation operation= new("Sum <= A and B xor Cin;", Variables);
        var result = operation.Execute();
    }
}
