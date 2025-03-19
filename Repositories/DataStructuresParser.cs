using Microsoft.Extensions.Logging;
using Parser.Interfaces;
using static Parser.Models.Constants.TYPE;
using static Parser.Models.Constants.ParseExceptions;
using Parser.Models;
namespace Parser.Repositories;
public class DataStructuresParser : IParseStructures {
    private TokenStream _streamer;
    private readonly ILogger<DataStructuresParser> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DataStructuresParser>();
    private Dictionary<string, HashSet<string>> _scopes = new();
    private string _name = string.Empty, _entityName = string.Empty;
    public DataStructuresParser(TokenStream streamer, Dictionary<string, HashSet<string>> scopes, string name, string EntityName)
    {
        _streamer = streamer;
        _scopes = scopes;
        _name = name;
        _entityName = EntityName;
    }
    public string GetName() => _name;
    public Dictionary<string, HashSet<string>> GetScopes() => _scopes;
    public bool ExistsVariable(string variable) 
        => _scopes.ContainsKey(_name) && _scopes[_name].Contains(variable) || 
            _scopes.ContainsKey(_entityName) && _scopes[_entityName].Contains(variable);
    public void ParseDeclaration() => throw new NotImplementedException();
    public void Parse(){
        _logger.LogInformation("Parsing data structure");
        ParseBodyDeclaration();
        _logger.LogInformation("Data structure parsed");
    }
    
    public void ParseBodyDeclaration(){
        switch(_streamer.Current?.Value.ToLower()){
            case "if":
                ParseIfStructure();
                ParseBodyDeclaration();
            break;
            case "case":
                ParseCaseStructure();
                ParseBodyDeclaration();
            break;
            case "for":
            case "while":
            case "exit":
            case "wait":
            case "return":
            case "null":
                throw new NotImplementedException();
            default:
                if(_streamer.Current?.Type == IDENTIFIERS){
                    ParseAsignations();
                    ParseBodyDeclaration();
                }
            break;
        }
    }
    public void ParseProcess(){
        _logger.LogInformation("Parsing process");
        _streamer.Expect(KEYWORDS);
        _streamer.Expect(PARENTHESIS);
        ParseProcessVariables();
        _streamer.Expect(PARENTHESIS);
        _streamer.Expect(KEYWORDS); // begin
        ParseBodyDeclaration();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // process
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("Process parsed");
    }
    private void ParseProcessVariables(){
        if(_streamer.Current?.Type == PARENTHESIS) return; // NOTHING INTO PARENTEHISIS FROM THE PROCESS
        while(_streamer.Current?.Type == IDENTIFIERS){
            if(!ExistsVariable(_streamer.Current?.Value ?? string.Empty))
                throw new ParserException(MISSING_DECLARATION);
            _streamer.Expect(IDENTIFIERS);
            if(_streamer.Current?.Type == SEPARATORS)
                _streamer.MoveNext();
                
        }
    }
    private void ParseIfStructure(){
        _logger.LogInformation("Parsing If structure");
        _streamer.Expect(KEYWORDS);
        ParseConditionalOperation();
        _streamer.Expect(KEYWORDS); // then
        ParseBodyDeclaration();
        if(_streamer.Current?.Type == KEYWORDS){
            string value = _streamer.Current.Value;
            _streamer.Expect(KEYWORDS);
            if(value == "else")
                ParseBodyDeclaration();
            else if(value == "elif")
                ParseIfStructure();
        }
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // if
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("If structure parsed");
    }
    private void ParseConditionalOperation(){
        while(_streamer.Current?.Value != "then"){
            var type = _streamer.Current?.Type ?? Constants.TYPE.UNKNOWN;
            var val = _streamer.Current?.Value ?? string.Empty;
            if(type == PARENTHESIS && val.Equals("(")){
                _streamer.MoveNext();
                ParseConditionalOperation();
            }else if(type == PARENTHESIS && val.Equals(")")){
                _streamer.MoveNext();
                break;
            }
            switch(type){
                case KEYWORDS:
                    switch(val){
                        case "rising_edge":
                        case "falling_edge":
                            _streamer.MoveNext();
                            ParseConditionalOperation();
                        break;
                        case "and":
                        case "or":
                        case "xor":
                            throw new NotImplementedException();
                        case "others":
                            _streamer.Expect(KEYWORDS);
                            _streamer.Expect(OPERATORS);
                            if(_streamer.Current?.Type == LITERALS ||
                            (_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value)))
                                _streamer.MoveNext();
                            else
                                throw new ParserException(SYNTAX_ERROR);
                        break;
                        default:
                            throw new ParserException(SYNTAX_ERROR);
                    }
                break;
                case IDENTIFIERS:
                    _streamer.Expect(IDENTIFIERS);
                    if(_streamer.Current?.Type == SEPARATORS &&
                    _streamer.Current?.Value == "=>" || _streamer.Current?.Type == OPERATORS)
                        _streamer.MoveNext();
                    else
                        throw new ParserException(SYNTAX_ERROR);
                    ParseConditionalOperation();
                break;
            } 
        }
    }
    private void ParseCaseStructure(){
        _logger.LogInformation("Parsing Case structure");
        _streamer.Expect(KEYWORDS);
        if(!ExistsVariable(_streamer.Current?.Value ?? string.Empty))
            throw new ParserException(MISSING_DECLARATION);
        _streamer.Expect(IDENTIFIERS);
        _streamer.Expect(KEYWORDS); // is
        ParseCaseCases();
        _streamer.Expect(KEYWORDS); // end
        _streamer.Expect(KEYWORDS); // case
        _streamer.Expect(OPERATORS); // ;
        _logger.LogInformation("Case structure parsed");
    }
    private void ParseCaseCases(){
        while(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "when"){
            _streamer.Expect(KEYWORDS);
            if(_streamer.Current?.Type == LITERALS || _streamer.Current?.Value.ToLower() == "others")
                _streamer.MoveNext();
            else
                throw new ParserException(SYNTAX_ERROR);
            _streamer.Expect(OPERATORS);
            ParseBodyDeclaration();
        }
    }
    private void ParseAsignations(){
        _logger.LogInformation("Parsing Assignations");
        while(_streamer.Current?.Type == IDENTIFIERS){
            string varName = _streamer.Current.Value;
            if(!ExistsVariable(varName))
                throw new ParserException(MISSING_DECLARATION);
            _streamer.Expect(IDENTIFIERS);
            _streamer.Expect(OPERATORS);
            ParseOperation();
            _streamer.Expect(OPERATORS); // ; CASE 
        }
        _logger.LogInformation("Assignations parsed");
    }
    private void ParseOperation(){
        while(_streamer.Current?.Value != ";" || _streamer.Current?.Type != PARENTHESIS){
            if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "("){
                ParseParenthesis();
                if((_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";") || _streamer.Current?.Type == KEYWORDS){
                    _streamer.MoveNext();
                    ParseOperation();
                }
            }
            if(_streamer.Current?.Type == KEYWORDS && _streamer.Current?.Value.ToLower() == "not")
                _streamer.MoveNext();
            if(_streamer.Current?.Type == IDENTIFIERS)
                if(!ExistsVariable(_streamer.Current.Value))
                    throw new ParserException(MISSING_DECLARATION);
                else
                    _streamer.Expect(IDENTIFIERS);
            else if(_streamer.Current?.Type == LITERALS)
                _streamer.Expect(LITERALS);
            if(_streamer.Current?.Type == OPERATORS && _streamer.Current?.Value != ";")
                _streamer.Expect(OPERATORS);
            else if(_streamer.Current?.Type == KEYWORDS)
                if(_streamer.Current.Value.ToLower().Equals("after")){
                    _streamer.MoveNext();
                    if(_streamer.Current?.Type == LITERALS || (_streamer.Current?.Type == IDENTIFIERS && ExistsVariable(_streamer.Current.Value)))
                        _streamer.MoveNext();
                    else
                        throw new ParserException(SYNTAX_ERROR);
                    _streamer.Expect(TIME_VARIABLES);
                }else
                    _streamer.Expect(KEYWORDS); // CASES LIKE XOR, AND, OR
            else if(_streamer.Current?.Type == PARENTHESIS && _streamer.Current?.Value == "(")
                continue;
            else
                break;
        }
    }
    private void ParseParenthesis(){
        _streamer.Expect(PARENTHESIS);
        ParseOperation();
        _streamer.Expect(PARENTHESIS);
    }
}