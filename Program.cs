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
        // 1–20: original baseline.
        // 21–40: curriculum-aligned stress fixtures spanning Diseño de Sistemas
        // Digitales (registers, FSMs, counters, memories, ASM) and Sistemas en
        // Chip (UART/SPI/I²C, PWM, IRQ controller, ADC, CORDIC).
        string[] testFiles = {
            "test1.vhdl",  "test2.vhdl",  "test3.vhdl",  "test4.vhdl",  "test5.vhdl",
            "test6.vhdl",  "test7.vhdl",  "test8.vhdl",  "test9.vhdl",  "test10.vhdl",
            "test11.vhdl", "test12.vhdl", "test13.vhdl", "test14.vhdl", "test15.vhdl",
            "test16.vhdl", "test17.vhdl", "test18.vhdl", "test19.vhdl", "test20.vhdl",
            "test21.vhdl", "test22.vhdl", "test23.vhdl", "test24.vhdl", "test25.vhdl",
            "test26.vhdl", "test27.vhdl", "test28.vhdl", "test29.vhdl", "test30.vhdl",
            "test31.vhdl", "test32.vhdl", "test33.vhdl", "test34.vhdl", "test35.vhdl",
            "test36.vhdl", "test37.vhdl", "test38.vhdl", "test39.vhdl", "test40.vhdl"
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

        // ── Negative tests ──────────────────────────────────────────────
        // Fixtures that SHOULD fail to parse. A "pass" here means the
        // Parser surfaced the expected ParserException; a "fail" means
        // the broken VHDL slipped through and the strictness fix has
        // regressed. Each entry carries a substring that the exception
        // message should contain so we don't accept arbitrary failures.
        var negativeTests = new (string File, string MustContain)[]
        {
            ("test_negative_unknown_type.vhdl",      "STDLOGIC"),
            ("test_negative_missing_port_semi.vhdl", "Missing ';'"),
            ("test_negative_garbage_in_body.vhdl",   "Unexpected token '$'")
        };

        var negResults = new List<string>();
        foreach (var (file, mustContain) in negativeTests)
        {
            try
            {
                Console.SetOut(TextWriter.Null);
                Repositories.Parser parser = new(file);
                parser.ParseV2();
                Console.SetOut(originalOut);
                // Parser DID NOT throw — that's a regression in strictness.
                negResults.Add($"✗ {file} - REGRESSION: parser accepted invalid VHDL");
            }
            catch (Exception ex) when (ex is ParserException || ex is TokenStream.StreamerException)
            {
                // Both exception types signal a parse rejection. StreamerException
                // is what TokenStream.Expect() throws on type mismatch; ParserException
                // is what higher-level parsers throw for semantic problems. Treat
                // either as a valid rejection so the harness doesn't care which
                // layer caught the bad input.
                Console.SetOut(originalOut);
                if (ex.Message.Contains(mustContain, StringComparison.OrdinalIgnoreCase))
                    negResults.Add($"✓ {file} - REJECTED ({mustContain})");
                else
                    negResults.Add($"✗ {file} - WRONG MESSAGE: \"{ex.Message}\" (expected to contain \"{mustContain}\")");
            }
            catch (Exception ex)
            {
                Console.SetOut(originalOut);
                negResults.Add($"✗ {file} - WRONG EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine("\n========== Negative (must-fail) Results ==========\n");
        foreach (var r in negResults) Console.WriteLine(r);
        Console.WriteLine("\n==================================================");
        int negPassed = negResults.Count(r => r.Contains("REJECTED"));
        Console.WriteLine($"\nNegative: {negPassed}/{negResults.Count} expected rejections");

        // Overall exit: non-zero when any positive failed OR any negative leaked through.
        Environment.Exit((passed == total && negPassed == negResults.Count) ? 0 : 1);
    }
}
