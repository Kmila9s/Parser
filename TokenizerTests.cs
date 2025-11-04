using Parser.Repositories;
using Parser.Models;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace Parser.Tests;

public class TokenizerTests
{
    [Theory]
    [InlineData("entity MyCounter is", 3, "entity", "is")] // Simple entity declaration
    [InlineData("signal clk : std_logic; -- This is a clock signal", 5, "signal", ";")] // Declaration with a comment
    [InlineData("a <= b;", 4, "a", ";")] // Simple assignment
    [InlineData("data : out std_logic_vector(7 downto 0);", 11, "data", ";")] // Port declaration with vector
    [InlineData("-- This is a full line comment", 0, null, null)] // A line that is only a comment
    [InlineData("", 0, null, null)] // An empty line
    [InlineData("    ", 0, null, null)] // A line with only whitespace
    public void TokenizeLine_VariousInputs_ShouldProduceCorrectTokens(string vhdlLine, int expectedTokenCount, string? expectedFirstToken, string? expectedLastToken)
    {
        // Arrange
        // We don't need a file path for this test, so we can pass an empty string.
        var tokenizer = new Tokenizer(string.Empty);

        // Act
        tokenizer.ReadString(vhdlLine);
        tokenizer.Evaluate();
        List<Token> tokens = tokenizer.GetDictionaryType();

        // Assert
        Assert.Equal(expectedTokenCount, tokens.Count);

        if (expectedTokenCount > 0)
        {
            Assert.Equal(expectedFirstToken, tokens.First().Value);
            Assert.Equal(expectedLastToken, tokens.Last().Value);
        }
    }
}