using System.IO;
using Parser.Models;
using Parser.Repositories;

namespace Parser;

/// <summary>
/// Test runner for the VHDL Parser module.
/// Runs all 20 VHDL test files through the ParseV2 pipeline and reports pass/fail results.
/// Supports single-file debugging via command-line argument.
/// </summary>
class Program
{
    /// <summary>
    /// Entry point for the Parser test runner.
    /// </summary>
    /// <param name="args">Optional: a single VHDL file path to parse in debug mode.</param>
    static void Main(string[] args)
    {
        // Test single file for debugging
        if (args.Length > 0)
        {
            try
            {
                Repositories.Parser parser = new(args[0]);
                parser.ParseV2();
                Console.WriteLine($"✓ {args[0]} - PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ {args[0]} - {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            return;
        }

        // Test all VHDL files
        string[] testFiles = {
            "test1.vhdl", "test2.vhdl", "test3.vhdl", "test4.vhdl", "test5.vhdl",
            "test6.vhdl", "test7.vhdl", "test8.vhdl", "test9.vhdl", "test10.vhdl",
            "test11.vhdl", "test12.vhdl", "test13.vhdl", "test14.vhdl", "test15.vhdl",
            "test16.vhdl", "test17.vhdl", "test18.vhdl", "test19.vhdl", "test20.vhdl"
        };

        var results = new List<string>();

        // Suppress logging by redirecting console temporarily
        var originalOut = Console.Out;

        foreach (var testFile in testFiles)
        {
            try
            {
                // Suppress log output
                Console.SetOut(TextWriter.Null);
                Repositories.Parser parser = new(testFile);
                parser.ParseV2();
                Console.SetOut(originalOut);
                results.Add($"✓ {testFile} - PASSED");
            }
            catch (NotImplementedException ex)
            {
                Console.SetOut(originalOut);
                results.Add($"✗ {testFile} - NOT IMPLEMENTED: {ex.Message}");
            }
            catch (ParserException ex)
            {
                Console.SetOut(originalOut);
                results.Add($"✗ {testFile} - PARSER ERROR: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.SetOut(originalOut);
                results.Add($"✗ {testFile} - ERROR: {ex.Message}");
            }
        }

        Console.WriteLine("\n========== VHDL Parser Test Results ==========\n");
        foreach (var result in results)
        {
            Console.WriteLine(result);
        }
        Console.WriteLine("\n===============================================");

        int passed = results.Count(r => r.Contains("PASSED"));
        int total = results.Count;
        Console.WriteLine($"\nTotal: {passed}/{total} tests passed");
    }
}
