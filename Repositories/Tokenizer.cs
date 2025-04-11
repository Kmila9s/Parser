using Parser.Models;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Parser.Repositories;
public class Tokenizer{
    private object _lock = new();
    public string LastError { get; private set; }
    private string _filePath = string.Empty;
    private List<string> _tokens;
    private List<Token> _typeDictionary;
    private readonly Regex _keywordsRegex, _operatorsRegex, _parenthesisRegex,
    _identifiersRegex, _literalsRegex, _qualifiedIdentifiersRegex, _separatorsRegex,
    _functionsRegex, _timeRegex, _vectorAttributes;
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
        if(!String.IsNullOrEmpty(_filePath))
            ReadFile(FilePath);
    }
    public void ReadFile(string FilePath){
        using StreamReader reader = new(FilePath);
        var line = reader.ReadLine();
        if(line != null){
            var begin = DateTime.Now;
            while(line != null){
                TokenizeLine(line);
                line = reader.ReadLine();
            }
            Evaluate();
            var end = DateTime.Now;
            var timeDelta = end - begin;
            //Console.WriteLine($"Total time: {timeDelta}");
        }
    }
    public List<Token> GetDictionaryType() => _typeDictionary;
    public void TokenizeLine(string line){
        List<string> tokens = new();
        var preTokens = TokenizeSpaces(line);
        foreach(var token in preTokens) tokens.AddRange(TokenizeSpecialCharacters(token));
        _tokens.AddRange(tokens);
    }
    public string[] TokenizeLineReturn(string line){
        List<string> tokens = new();
        var preTokens = TokenizeSpaces(line);
        foreach(var token in preTokens) tokens.AddRange(TokenizeSpecialCharacters(token));
        return tokens.ToArray();
    }
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
    public void Evaluate() => _typeDictionary = EvaluateTokens(_tokens);
    private Token EvaluateToken(string token){
        token = token.ToLower();
        if(_keywordsRegex.IsMatch(token))
            return new(token, Constants.TYPE.KEYWORDS);
        else if(_operatorsRegex.IsMatch(token))
            return new(token, Constants.TYPE.OPERATORS);
        else if(_parenthesisRegex.IsMatch(token))
            return new(token, Constants.TYPE.PARENTHESIS);
        else if(_literalsRegex.IsMatch(token))
            return new(token, Constants.TYPE.LITERALS);
        else if(_qualifiedIdentifiersRegex.IsMatch(token))
            return new(token, Constants.TYPE.QUALIFIED_IDENTIFIERS);
        else if(_timeRegex.IsMatch(token.ToLower()))
            return new(token, Constants.TYPE.TIME_VARIABLES);
        else if(_identifiersRegex.IsMatch(token))
            return new(token, Constants.TYPE.IDENTIFIERS);
        else if(_separatorsRegex.IsMatch(token))
            return new(token, Constants.TYPE.SEPARATORS);
        else if(_functionsRegex.IsMatch(token))
            return new(token, Constants.TYPE.METHODS);
        else if(_vectorAttributes.IsMatch(token))
            return new(token, Constants.TYPE.VECTORS_ATTRIBUTES);
        else
            return new(token, Constants.TYPE.UNKNOWN);
    }
    private List<Token> EvaluateTokens(List<string> tokens){
        List<Token> evaluation = new();
        foreach(var token in tokens){
            evaluation.Add(EvaluateToken(token));
        }
        return evaluation;
    }
}