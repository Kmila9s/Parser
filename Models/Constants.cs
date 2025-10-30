namespace Parser.Models;

/// <summary>
/// Provides constant values and enumerations used throughout the VHDL parser.
/// </summary>
public class Constants{
    /// <summary>
    /// Defines the types of tokens that can be identified by the Tokenizer.
    /// </summary>
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
    /// <summary>
    /// Defines custom exception types for parsing errors.
    /// </summary>
    public enum ParseExceptions : int {
        UNKNOWN,
        INITIALIZATION,
        DUPLICATE_DECLARATION,
        MISSING_STRUCTURE,
        DUPLICATE_STRUCTURE,
        MISSING_DECLARATION,
        SYNTAX_ERROR
    }
    /// <summary>
    /// Defines the supported VHDL data types.
    /// </summary>
    public enum DATATYPES : int{
        STD_LOGIC,
        STD_LOGIC_VECTOR,
        RANGE,
        BIT,
        BOOLEAN,
        INTEGER,
        NATURAL,
        CHARACTER,
        HEX,
        UNKNOWN,
    }
    /// <summary>
    /// Defines types of operators.
    /// </summary>
    public enum OperatorType : int{
        ASSIGNATION,
        LOGIC_OPERATION,
        ARITH_OPERATION,
        COMPARATION,
        UNKNOWN
    }
    /// <summary>
    /// Defines the direction of a port (input or output).
    /// </summary>
    public enum PortType : int {
        IN,
        OUT
    }
    /// <summary>
    /// Converts a string representation of a data type to its corresponding DATATYPES enum value.
    /// </summary>
    /// <param name="value">The string value of the data type.</param>
    /// <returns>The <see cref="DATATYPES"/> enum member.</returns>
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
    /// <summary>
    /// A list of special characters used for tokenization.
    /// </summary>
    public static List<string> SPECIALS_CHARACTERS = new(){
        "(", ")", "{", "}", "[", "]", ",", ";", "-", "+", "*", "/", "%", "#", "!", "@", "$", "^", "&",
        "=", ":", "<", ">", "?", "|"
    };
    /// <summary>
    /// A set of operators valid within an 'if' statement condition.
    /// </summary>
    public static HashSet<string> IF_OPERATORS = new(){
        "=", "+", "-", "/", "*"
    };

    /// <summary>
    /// Regular expressions for identifying different VHDL token types.
    /// </summary>
    public const string
    KEYWORDS = @"^(entity|port|end|architecture|begin|in|is|out|std_logic|and|or|not|xor|of|use|library|process|when|case|downto|std_logic_vector|others|signal|generic|integer|package|body|for|loop|variable|function|rising_edge|return|if|else|elsif|then|map|boolean|integer|range|to|bit|bit_vector|after|natural|positive|real|string|time|signed|unsigned|character|boolean_vector|integer_vector|std_ulogic|std_ulogic_vector|access|file|record|constant|attribute|group|disconnect|falling_edge)$",
    VECTORS_IDENTIFIER = @".*\s[std_logic_vector|STD_LOGIC_VECTOR]\s?\(.+",
    IDENTIFIERS = @"^[_a-zA-Z][_a-zA-Z0-9]*$",
    QUALIFIED_IDENTIFIERS = @"^[_a-zA-Z]+(\.[_a-zA-Z0-9]+)+$",
    METHODS = @"^\[_a-zA-Z][_a-zA-Z0-9]*([_a-zA-Z][_a-zA-Z0-9]*((\s?,[_a-zA-Z][_a-zA-Z0-9]*\s?)?)+\)",
    LITERAL_DECIMAL_OR_INTEGER = "[0-9]+(\\.[0-9]+)?|[0-9]+(_[0-9]+)*",
    LITERAL_QUOTED_INTEGER = "\"[0-9]+\"",
    LITERAL_SINGLE_QUOTED_INTEGER = "'[0-9]+'",
    LITERAL_STD_LOGIC_CHAR = "'.+'",
    LITERAL_STD_LOGIC_VECTOR = "(\".+\"|'.+')",
    LITERAL_HEX_STRING = "(x|X)\".+\"",
    LITERAL_BOOLEAN = "true|false",
    LITERAL_STD_LOGIC = "('[0|1]'|\"[0|1]\"|'(U|u)'|\"(U|u)\"|'(Z|z)'|\"(Z|z)\")",
    LITERALS = $"^({LITERAL_DECIMAL_OR_INTEGER}|{LITERAL_QUOTED_INTEGER}|{LITERAL_SINGLE_QUOTED_INTEGER}|{LITERAL_STD_LOGIC_CHAR}|{LITERAL_STD_LOGIC_VECTOR}|{LITERAL_HEX_STRING}|{LITERAL_BOOLEAN})$",
    SEPARATORS = "^(,)$",
    FUNCTIONS = @"^\,?[_a-zA-Z][_a-zA-Z0-9]*\(.+\)\,?$",
    OPERATORS = @"^((\<\=)|\:|\;|(\=\>)|\+|\-|\:\=|\=|\*|\&|\'|\/|\<|\>|mod)$",
    PARENTHESIS = @"^(\(|\))$",
    SPECIAL_METHODS = @"[_a-zA-Z]+[_a-zA-Z0-9]*\(.+",
    SPECIAL_METHODS2 = @"[_a-zA-Z]+[_a-zA-Z0-9]*\(.+\)",
    TIME_VARIABLES = @"^(fs|ps|ns|us|ms|sec|min|hr)$",
    VECTORS_ATTRIBUTES = @"^[_a-zA-Z][_a-zA-Z0-9]*\'(last_value|last_event|last_active|event|stable|quiet|active|transaction|driving|driving_value|delayed|now|path_name|instance_name|base|high|low|left|right|length|range|reverse_range|ascending)",
    ASSIGNATION_OPERATORS = @"^[(\<\=)|\:\=]",
    COMPARATION_OPERATORS = @"^[=|<|>|!=]",
    LOGIC_OPERATORS = "(and|or|xor|nand|nor|not)",
    ARITH_OPERATORS = @"(\+|\-|\*|\/|mod)";
}