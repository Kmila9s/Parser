namespace Parser.Models;
public class Constants{
    public enum TYPE : ushort{
        KEYWORDS,
        IDENTIFIERS,
        OPERATORS,
        LITERALS,
        QUALIFIED_IDENTIFIERS,
        PARENTHESIS,
        SEPARATORS,
        METHODS,
        TIME_VARIABLES,
        VECTORS_ATTRIBUTES,
        UNKNOWN
    }
    public enum ParseExceptions : int {
        UNKNOWN,
        INITIALIZATION,
        DUPLICATE_DECLARATION,
        MISSING_STRUCTURE,
        DUPLICATE_STRUCTURE,
        MISSING_DECLARATION,
        SYNTAX_ERROR
    }
    public enum DATATYPES : int{
        STD_LOGIC,
        STD_LOGIC_VECTOR,
        RANGE,
        BIT,
        BOOLEAN,
        INTEGER,
        NATURAL,
        CHARACTER,
        UNKNOWN,
    }
    public static DATATYPES GetDatatype(string value){
        value = value.ToLower();
        switch(value){
            case "std_logic":
                return DATATYPES.STD_LOGIC;
            case "std_logic_vector":
                return DATATYPES.STD_LOGIC_VECTOR;
            case "range":
                return DATATYPES.RANGE;
            case "bit":
                return DATATYPES.BIT;
            case "boolean":
                return DATATYPES.BOOLEAN;
            case "INTEGER":
                return DATATYPES.INTEGER;
            case "NATURAL":
                return DATATYPES.NATURAL; 
            default:
                return DATATYPES.UNKNOWN;
        }
    }
    public static List<string> SPECIALS_CHARACTERS = new(){
        "(", ")", "{", "}", "[", "]", ",", ";", "-", "+", "*", "/", "%", "#", "!", "@", "$", "^", "&",
        "=", ":", "<", ">", "?", "|"
    };
    public static HashSet<string> IF_OPERATORS = new(){
        "=", "+", "-", "/", "*"
    };

    public const string 
    KEYWORDS = @"^(entity|port|end|architecture|begin|in|is|out|std_logic|and|or|not|xor|of|use|library|process|when|case|downto|std_logic_vector|others|signal|generic|integer|package|body|for|loop|variable|function|rising_edge|return|if|else|elsif|then|map|boolean|integer|range|to|bit|bit_vector|after|natural|positive|real|string|time|signed|unsigned|character|boolean_vector|integer_vector|std_ulogic|std_ulogic_vector|access|file|record|constant|attribute|group|disconnect|falling_edge)$",
    VECTORS_IDENTIFIER = @".*\s[std_logic_vector|STD_LOGIC_VECTOR]\s?\(.+",
    IDENTIFIERS = @"^[_a-zA-Z][_a-zA-Z0-9]*$",
    QUALIFIED_IDENTIFIERS = @"^[_a-zA-Z]+(\.[_a-zA-Z0-9]+)+$",
    METHODS = @"^\[_a-zA-Z][_a-zA-Z0-9]*([_a-zA-Z][_a-zA-Z0-9]*((\s?,[_a-zA-Z][_a-zA-Z0-9]*\s?)?)+\)",
    LITERALS = "^([0-9]+(\\.[0-9]+)?|\"[0-9]+\"|'[0-9]+'|'.+'|\".+\"|(x|X)\".+\"|true|false)$",
    SEPARATORS = "^(,)$",
    FUNCTIONS = @"^\,?[_a-zA-Z][_a-zA-Z0-9]*\(.+\)\,?$",
    OPERATORS = @"^((\<\=)|\:|\;|(\=\>)|\+|\-|\:\=|\=|\*|\&|\'|\/|\<|\>|mod)$", 
    PARENTHESIS = @"^(\(|\))$",
    SPECIAL_METHODS = @"[_a-zA-Z]+[_a-zA-Z0-9]*\(.+",
    SPECIAL_METHODS2 = @"[_a-zA-Z]+[_a-zA-Z0-9]*\(.+\)",
    TIME_VARIABLES = @"^(fs|ps|ns|us|ms|sec|min|hr)$",
    VECTORS_ATTRIBUTES = @"^[_a-zA-Z][_a-zA-Z0-9]*\'(last_value|last_event|last_active|event|stable|quiet|active|transaction|driving|driving_value|delayed|now|path_name|instance_name|base|high|low|left|right|length|range|reverse_range|ascending)";
}