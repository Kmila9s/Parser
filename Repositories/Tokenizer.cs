using Parser.Models;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Parser.Repositories;

/// <summary>
/// Responsible for reading VHDL source code (from a file or string) and converting it into a sequence of tokens.
/// </summary>
public class Tokenizer{
    private object _lock = new();
    /// <summary>
    /// Gets the last error message that occurred during tokenization.
    /// </summary>
    public string LastError { get; private set; }
    private string _filePath = string.Empty;
    private List<(List<string>, int)> _tokens;
    private List<Token> _typeDictionary;
    private readonly Regex _keywordsRegex, _operatorsRegex, _parenthesisRegex,
    _identifiersRegex, _literalsRegex, _qualifiedIdentifiersRegex, _separatorsRegex,
    _functionsRegex, _timeRegex, _vectorAttributes;
    private int _currentLine = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Tokenizer"/> class.
    /// </summary>
    /// <param name="FilePath">The path to the VHDL file to be tokenized.</param>
    public Tokenizer(string FilePath)
    {
        LastError = "";
        _tokens = new();
        _typeDictionary = new();
        RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Compiled,
        optionsCompiled = RegexOptions.Compiled;
        _keywordsRegex = new(Constants.KEYWORDS, options);
        _operatorsRegex = new(Constants.OPERATORS, optionsCompiled);
        _parenthesisRegex = new(Constants.PARENTHESIS, optionsCompiled);
        _literalsRegex = new(Constants.LITERALS, options);
        _qualifiedIdentifiersRegex = new(Constants.QUALIFIED_IDENTIFIERS, optionsCompiled);
        _identifiersRegex = new(Constants.IDENTIFIERS, optionsCompiled);
        _separatorsRegex = new(Constants.SEPARATORS, optionsCompiled);
        _functionsRegex = new(Constants.FUNCTIONS, optionsCompiled);
        _timeRegex = new(Constants.TIME_VARIABLES, RegexOptions.IgnoreCase);
        _vectorAttributes = new(Constants.VECTORS_ATTRIBUTES, optionsCompiled);
        _filePath = FilePath;
        if (!String.IsNullOrEmpty(_filePath))
            ReadFile(FilePath);
    }
    /// <summary>
    /// Tokenizes a single line of VHDL code provided as a string.
    /// </summary>
    /// <param name="RawData">The raw VHDL code string.</param>
    public void ReadString(string RawData)
    {
        _currentLine = 0;
        TokenizeLine(RawData);
    } 
    /// <summary>
    /// Reads and tokenizes an entire VHDL file.
    /// </summary>
    /// <param name="FilePath">The path to the VHDL file.</param>
    public void ReadFile(string FilePath)
    {
        _currentLine = 0;
        using StreamReader reader = new(FilePath);
        var line = reader.ReadLine();
        if (line != null)
        {
            var begin = DateTime.Now;
            while (line != null)
            {
                TokenizeLine(line.Trim());
                line = reader.ReadLine();
            }
            Evaluate();
            //Console.WriteLine($"Total time: {timeDelta}");
        }
    }
    /// <summary>
    /// Gets the list of classified tokens.
    /// </summary>
    /// <returns>A list of <see cref="Token"/> objects.</returns>
    public List<Token> GetDictionaryType() => _typeDictionary;
    /// <summary>
    /// Performs the tokenization for a single line of code.
    /// </summary>
    /// <param name="line">The line of code to tokenize.</param>
    public void TokenizeLine(string line){
        List<string> tokens = new();
        var preTokens = TokenizeSpaces(line.Trim());
        foreach(var token in preTokens) tokens.AddRange(TokenizeSpecialCharacters(token.Trim()));
        _tokens.Add((tokens, _currentLine ++));
    }
    /// <summary>
    /// Tokenizes a single line and returns the raw string tokens.
    /// </summary>
    /// <param name="line">The line of code to tokenize.</param>
    /// <returns>An array of string tokens.</returns>
    public string[] TokenizeLineReturn(string line){
        List<string> tokens = new();
        var preTokens = TokenizeSpaces(line);
        foreach(var token in preTokens) tokens.AddRange(TokenizeSpecialCharacters(token));
        return tokens.ToArray();
    }
    /// <summary>
    /// Splits a line by spaces and removes comments.
    /// </summary>
    private List<string> TokenizeSpaces(string line){
        try{
            line = line.Trim();
            if(line.StartsWith("--") || line.StartsWith("//")) return new();
            if(String.IsNullOrEmpty(line)) return new();
            if(line.Contains("--")) line = line.Split("--")[0];
            line = line.Replace("\t", "");
            return line
                    .Trim()
                    .Split(" ")
                    .Where(p => !String.IsNullOrWhiteSpace(p))
                    .ToList();
        }catch(Exception ex){
            LastError = ex.Message;
        }
        return new();
    }
    /// <summary>
    /// Splits a token by special characters (e.g., ':=', ';', '(').
    /// </summary>
    private List<string> TokenizeSpecialCharacters(string line){
        try{
            List<string> tokens = new(), separators = new(){ ":=", "<=", "=>"};
            if(line.StartsWith("--")) return new();
            if(String.IsNullOrEmpty(line)) return new();
            foreach(var separator in separators){
                if(line.Contains(separator)){
                    var splitted = line.Split(separator);
                    var begin = splitted[0];
                    var end = splitted[1];
                    tokens.AddRange(TokenizeSpecialCharacters(begin));
                    tokens.Add(separator);
                    tokens.AddRange(TokenizeSpecialCharacters(end));
                    break;
                }
            }
            if(separators.Any(line.Contains)) return tokens;
            // Verify if exists any special character on the string "line"
            if(!Constants.SPECIALS_CHARACTERS.Any(line.Contains)){
                tokens.Add(line);
            }else{
                foreach(var specialCharacter in Constants.SPECIALS_CHARACTERS){
                    if(line.Contains(specialCharacter)){
                        int index = line.IndexOf(specialCharacter);
                        var begin = line.Substring(0, index);
                        string end = string.Empty;
                        if(index < line.Length && (line.Length - index - 1) > 0)
                            end = line.Substring(index + 1, line.Length - index - 1);
                        tokens.AddRange(TokenizeSpecialCharacters(begin));
                        tokens.Add(specialCharacter);
                        if(!String.IsNullOrEmpty(end))
                            tokens.AddRange(TokenizeSpecialCharacters(end));
                        break;
                    }
                }
            }
            
            return tokens;
        }catch(Exception ex){
            LastError = ex.Message;
        }
        return new();
    }
    /// <summary>
    /// Evaluates all raw tokens and classifies them into <see cref="Token"/> objects.
    /// </summary>
    public void Evaluate() => _typeDictionary = EvaluateTokens();
    /// <summary>
    /// Classifies a single raw string token into a <see cref="Token"/> object using regular expressions.
    /// </summary>
    /// <param name="token">The raw string token.</param>
    /// <returns>A classified <see cref="Token"/>.</returns>
    private Token EvaluateToken(string token){
        var evaluationToken = token.ToLower();
        if(_keywordsRegex.IsMatch(evaluationToken))
            return new(token, Constants.TYPE.KEYWORDS);
        else if(_operatorsRegex.IsMatch(evaluationToken))
            return new(token, Constants.TYPE.OPERATORS);
        else if(_parenthesisRegex.IsMatch(evaluationToken))
            return new(token, Constants.TYPE.PARENTHESIS);
        else if(_literalsRegex.IsMatch(evaluationToken))
            return new(token, Constants.TYPE.LITERALS);
        else if(_qualifiedIdentifiersRegex.IsMatch(evaluationToken))
            return new(token, Constants.TYPE.QUALIFIED_IDENTIFIERS);
        else if(_timeRegex.IsMatch(token.ToLower()))
            return new(token, Constants.TYPE.TIME_VARIABLES);
        else if(_identifiersRegex.IsMatch(evaluationToken))
            return new(token, Constants.TYPE.IDENTIFIERS);
        else if(_separatorsRegex.IsMatch(evaluationToken))
            return new(token, Constants.TYPE.SEPARATORS);
        else if(_functionsRegex.IsMatch(evaluationToken))
            return new(token, Constants.TYPE.METHODS);
        else if(_vectorAttributes.IsMatch(evaluationToken))
            return new(token, Constants.TYPE.VECTORS_ATTRIBUTES);
        else
            return new(token, Constants.TYPE.UNKNOWN);
    }
    /// <summary>
    /// Iterates through all tokenized lines and evaluates each token.
    /// </summary>
    /// <returns>A complete list of classified <see cref="Token"/> objects for the entire source.</returns>
    private List<Token> EvaluateTokens(){
        List<Token> evaluation = new();
        _tokens.ForEach(item =>
        {
            var tokens = item.Item1;
            var line = item.Item2;
            int currentCol = 0;
            tokens.ForEach(p =>
            {
                var token = EvaluateToken(p);
                token.Line = line;
                token.Col = currentCol++;
                evaluation.Add(token);
            });
        });
        return evaluation;
    }
}