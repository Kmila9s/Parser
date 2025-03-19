using System.IO;
using Parser.Repositories;

namespace Parser;

class Program
{
    static void Main(string[] args)
    {
        Repositories.Parser parser = new("test6.vhdl");
        parser.ParseV2();
    }
}
