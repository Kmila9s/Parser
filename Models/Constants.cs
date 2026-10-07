namespace Parser.Models;

/// <summary>
/// Provides constant values, enumerations, and regex patterns used throughout the VHDL parser.
/// This class centralizes all tokenization rules and classification constants.
/// </summary>
public class Constants{
    /// <summary>
    /// Defines the types of tokens that can be identified by the Tokenizer.
    /// Each token in the VHDL source code is classified into one of these categories.
    /// </summary>
    public enum TYPE : ushort{
        /// <summary>Reserved VHDL keywords (entity, architecture, signal, process, etc.).</summary>
        KEYWORDS,
        /// <summary>User-defined names for signals, variables, entities, etc. (e.g., clk, data_in).</summary>
        IDENTIFIERS,
        /// <summary>Mathematical and logical operators (+, -, *, &lt;=, :=, etc.).</summary>
        OPERATORS,
        /// <summary>Constant values: numbers, strings, bit vectors (e.g., '0', "1010", 42).</summary>
        LITERALS,
        /// <summary>Dot-separated identifiers for package references (e.g., work.my_pkg.my_type).</summary>
        QUALIFIED_IDENTIFIERS,
        /// <summary>Opening and closing parentheses: ( and ).</summary>
        PARENTHESIS,
        /// <summary>Comma separator used in lists.</summary>
        SEPARATORS,
        /// <summary>Function/procedure calls with arguments.</summary>
        METHODS,
        /// <summary>Time unit specifiers: fs, ps, ns, us, ms, sec, min, hr.</summary>
        TIME_VARIABLES,
        /// <summary>Signal attributes like 'event, 'length, 'range, 'high, 'low, etc.</summary>
        VECTORS_ATTRIBUTES,
        /// <summary>Token type could not be determined.</summary>
        UNKNOWN
    }
    /// <summary>
    /// Defines custom exception types for parsing errors.
    /// Used by <see cref="ParserException"/> to categorize error conditions.
    /// </summary>
    public enum ParseExceptions : int {
        /// <summary>Unknown or unspecified error.</summary>
        UNKNOWN,
        /// <summary>Error during parser initialization (e.g., file not found).</summary>
        INITIALIZATION,
        /// <summary>Identifier already declared in current scope (e.g., duplicate signal name).</summary>
        DUPLICATE_DECLARATION,
        /// <summary>Required VHDL structure not found (e.g., missing 'end' keyword).</summary>
        MISSING_STRUCTURE,
        /// <summary>Structure already exists (e.g., multiple entities with same name).</summary>
        DUPLICATE_STRUCTURE,
        /// <summary>Reference to undeclared identifier (e.g., using undefined signal).</summary>
        MISSING_DECLARATION,
        /// <summary>General syntax error (unexpected token, invalid construct).</summary>
        SYNTAX_ERROR
    }
    /// <summary>
    /// Defines the supported VHDL data types for variables, signals, and ports.
    /// </summary>
    public enum DATATYPES : int{
        /// <summary>Single-bit logic type with 9 possible values (U, X, 0, 1, Z, W, L, H, -).</summary>
        STD_LOGIC,
        /// <summary>Array of std_logic bits (e.g., std_logic_vector(7 downto 0)).</summary>
        STD_LOGIC_VECTOR,
        /// <summary>Integer subrange type (e.g., range 0 to 255).</summary>
        RANGE,
        /// <summary>Two-valued logic type (0 or 1 only).</summary>
        BIT,
        /// <summary>Boolean type (true or false).</summary>
        BOOLEAN,
        /// <summary>32-bit signed integer type.</summary>
        INTEGER,
        /// <summary>Non-negative integer type (0 and positive values).</summary>
        NATURAL,
        /// <summary>Single character type.</summary>
        CHARACTER,
        /// <summary>Hexadecimal literal representation.</summary>
        HEX,
        /// <summary>Data type could not be determined.</summary>
        UNKNOWN,
    }
    /// <summary>
    /// Defines categories of VHDL operators for expression evaluation.
    /// Used by the Interpreter to determine operator precedence and behavior.
    /// </summary>
    public enum OperatorType : int{
        /// <summary>Assignment operators: &lt;= (signal) and := (variable).</summary>
        ASSIGNATION,
        /// <summary>Logical operators: and, or, xor, nand, nor, xnor, not, sll, srl, sla, sra, rol, ror.</summary>
        LOGIC_OPERATION,
        /// <summary>Arithmetic operators: +, -, *, /, **, mod.</summary>
        ARITH_OPERATION,
        /// <summary>Comparison operators: =, /=, &lt;, &gt;, &lt;=, &gt;=.</summary>
        COMPARATION,
        /// <summary>Operator type could not be determined.</summary>
        UNKNOWN
    }
    /// <summary>
    /// Defines the direction of a port in an entity declaration.
    /// </summary>
    public enum PortType : int {
        /// <summary>Input port - receives data from external source.</summary>
        IN,
        /// <summary>Output port - sends data to external destination.</summary>
        OUT,
        /// <summary>Bidirectional port - can both receive and send data.</summary>
        INOUT,
        /// <summary>Buffer port - output that can be read internally.</summary>
        BUFFER
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
            case "integer":
                return DATATYPES.INTEGER;
            case "natural":
                return DATATYPES.NATURAL;
            default:
                return DATATYPES.UNKNOWN;
        }
    }

    /// <summary>
    /// Canonical names of the standard VHDL types the port parser recognises.
    /// Used by <see cref="FindTypoSuggestion"/> to surface "did you mean …"
    /// when the user writes a near-match like <c>STDLOGIC</c> (missing the
    /// underscore) or <c>STD_LGIC</c> (missing a letter).
    /// </summary>
    /// <remarks>
    /// Comparison is case-insensitive — VHDL identifiers are case-folded.
    /// The set is intentionally broader than <see cref="GetDatatype"/>'s
    /// switch so user typos for types Kmila doesn't yet model internally
    /// (e.g. <c>unsigned</c>, <c>signed</c>) still get a helpful hint.
    /// </remarks>
    public static readonly HashSet<string> KNOWN_TYPE_NAMES = new(StringComparer.OrdinalIgnoreCase)
    {
        "std_logic", "std_logic_vector",
        "bit", "bit_vector",
        "boolean", "integer", "natural", "positive",
        "character", "string",
        "unsigned", "signed",
        "real", "time"
    };

    /// <summary>
    /// Returns the closest known VHDL type name if <paramref name="unknown"/>
    /// is within Levenshtein-distance 2 of one — otherwise null. The caller
    /// uses this to decide whether to surface a "did you mean" hint
    /// (likely typo) or quietly accept the token as a user-defined type.
    /// </summary>
    /// <remarks>
    /// The candidate set is the union of <see cref="KNOWN_TYPE_NAMES"/>
    /// (canonical VHDL + IEEE primitives) and
    /// <see cref="Parser.Services.UserTypeRegistry"/> (types the host has
    /// resolved from <c>use library.package.all</c> clauses through
    /// <c>LibraryCompiler</c>). Hosts that don't populate the registry
    /// effectively fall back to canonical-only suggestions, which is the
    /// historical behaviour.
    /// </remarks>
    /// <param name="unknown">The identifier captured in source.</param>
    /// <returns>The suggested canonical type, or null when no close match exists.</returns>
    public static string? FindTypoSuggestion(string unknown)
    {
        if (string.IsNullOrEmpty(unknown)) return null;
        if (KNOWN_TYPE_NAMES.Contains(unknown)) return null;
        if (Parser.Services.UserTypeRegistry.Instance.IsKnown(unknown)) return null;

        var needle = unknown.ToLowerInvariant();
        string? best = null;
        int bestDist = 3; // strictly less than 3 → distance ≤ 2 → typo
        foreach (var name in KNOWN_TYPE_NAMES)
        {
            int d = LevenshteinDistance(needle, name);
            if (d < bestDist) { bestDist = d; best = name; }
        }
        // User-imported library types live alongside the canonical ones in
        // the suggestion space. We compare against the original casing the
        // library declared so the message reads back the user's chosen
        // identifier rather than a forced lowercase form.
        foreach (var name in Parser.Services.UserTypeRegistry.Instance.All)
        {
            int d = LevenshteinDistance(needle, name.ToLowerInvariant());
            if (d < bestDist) { bestDist = d; best = name; }
        }
        return best;
    }

    /// <summary>
    /// Classic O(n·m) Levenshtein edit distance with a rolling two-row
    /// buffer. Used by <see cref="FindTypoSuggestion"/>; kept internal so
    /// callers don't reach for it directly — there are better libraries
    /// when distance-on-large-strings becomes a hot path.
    /// </summary>
    private static int LevenshteinDistance(string a, string b)
    {
        if (a == b) return 0;
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        int la = a.Length, lb = b.Length;
        var prev = new int[lb + 1];
        var curr = new int[lb + 1];
        for (int j = 0; j <= lb; j++) prev[j] = j;
        for (int i = 1; i <= la; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= lb; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[lb];
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
        "=", "+", "-", "/", "*", "<", ">", "<=", ">=", "/="
    };

    #region Regular Expressions for Token Classification

    /// <summary>
    /// Regular expressions for identifying different VHDL token types.
    /// All patterns are anchored (^ and $) to ensure complete token matching.
    /// </summary>
    /// <remarks>
    /// <para>These regex patterns are used by the Tokenizer to classify each token.</para>
    /// <para>Order of evaluation matters - KEYWORDS should be checked before IDENTIFIERS.</para>
    /// </remarks>
    /// <summary>
    /// Matches all reserved VHDL keywords.
    /// Includes: structural (entity, architecture, component), control flow (if, case, for, while),
    /// types (std_logic, integer, boolean), operators (and, or, xor), and declarations (signal, variable, constant).
    /// </summary>
    public const string KEYWORDS = @"^(entity|port|end|architecture|begin|in|is|out|std_logic|and|or|not|xor|of|use|library|process|when|case|downto|std_logic_vector|others|signal|generic|integer|package|body|for|loop|variable|function|rising_edge|return|if|else|elsif|then|map|boolean|integer|range|to|bit|bit_vector|after|natural|positive|real|string|time|signed|unsigned|character|boolean_vector|integer_vector|std_ulogic|std_ulogic_vector|access|file|record|constant|attribute|group|disconnect|falling_edge|configuration|component|inOut|while|exit|wait|null|type|subtype|procedure|alias|generate|block|assert|report|severity|open|guarded|bus|all|new|impure|pure|shared|nand|nor|xnor|sll|srl|sla|sra|rol|ror|abs|rem|on|until|unaffected|transport|reject|inertial|with|select|label|units|buffer|linkage|literal|postponed|array|next)$";
    /// <summary>
    /// Matches std_logic_vector type declarations with bounds.
    /// Example: "signal foo : std_logic_vector(7 downto 0)"
    /// </summary>
    public const string VECTORS_IDENTIFIER = @".*\s[std_logic_vector|STD_LOGIC_VECTOR]\s?\(.+";
    /// <summary>
    /// Matches valid VHDL identifiers.
    /// Must start with letter or underscore, followed by letters, digits, or underscores.
    /// Examples: "clk", "data_in", "_internal", "counter_8bit"
    /// </summary>
    public const string IDENTIFIERS = @"^[_a-zA-Z][_a-zA-Z0-9]*$";
    /// <summary>
    /// Matches qualified (dot-separated) identifiers for package/library references.
    /// Examples: "work.my_package.my_type", "ieee.std_logic_1164.std_logic"
    /// </summary>
    public const string QUALIFIED_IDENTIFIERS = @"^[_a-zA-Z][_a-zA-Z0-9]*(\.[_a-zA-Z0-9]+)+$";
    /// <summary>
    /// Matches function/procedure calls with arguments.
    /// Example: "conv_integer(signal_a, signal_b)"
    /// </summary>
    public const string METHODS = @"^\[_a-zA-Z][_a-zA-Z0-9]*([_a-zA-Z][_a-zA-Z0-9]*((\s?,[_a-zA-Z][_a-zA-Z0-9]*\s?)?)+\)";
    /// <summary>
    /// Matches decimal or integer literals, including underscored notation.
    /// Examples: "42", "3.14", "1_000_000"
    /// </summary>
    public const string LITERAL_DECIMAL_OR_INTEGER = "[0-9]+(\\.[0-9]+)?|[0-9]+(_[0-9]+)*";
    /// <summary>
    /// Matches double-quoted integer literals.
    /// Example: "\"123\""
    /// </summary>
    public const string LITERAL_QUOTED_INTEGER = "\"[0-9]+\"";
    /// <summary>
    /// Matches single-quoted integer literals.
    /// Example: "'5'"
    /// </summary>
    public const string LITERAL_SINGLE_QUOTED_INTEGER = "'[0-9]+'";
    /// <summary>
    /// Matches single-quoted character literals (std_logic values).
    /// Examples: "'0'", "'1'", "'Z'", "'X'"
    /// </summary>
    public const string LITERAL_STD_LOGIC_CHAR = "'.+'";
    /// <summary>
    /// Matches bit vector literals in quotes.
    /// Examples: "\"1010\"", "'11110000'"
    /// </summary>
    public const string LITERAL_STD_LOGIC_VECTOR = "(\".+\"|'.+')";
    /// <summary>
    /// Matches hexadecimal string literals.
    /// Examples: "x\"FF\"", "X\"DEADBEEF\""
    /// </summary>
    public const string LITERAL_HEX_STRING = "(x|X)\".+\"";
    /// <summary>
    /// Matches boolean literals.
    /// Values: "true", "false"
    /// </summary>
    public const string LITERAL_BOOLEAN = "true|false";
    /// <summary>
    /// Matches std_logic single-bit literals.
    /// Valid values: '0', '1', 'U' (uninitialized), 'Z' (high-impedance)
    /// </summary>
    public const string LITERAL_STD_LOGIC = "('[0|1]'|\"[0|1]\"|'(U|u)'|\"(U|u)\"|'(Z|z)'|\"(Z|z)\")";
    /// <summary>
    /// Combined pattern for all literal types.
    /// </summary>
    public const string LITERALS = $"^({LITERAL_DECIMAL_OR_INTEGER}|{LITERAL_QUOTED_INTEGER}|{LITERAL_SINGLE_QUOTED_INTEGER}|{LITERAL_STD_LOGIC_CHAR}|{LITERAL_STD_LOGIC_VECTOR}|{LITERAL_HEX_STRING}|{LITERAL_BOOLEAN})$";
    /// <summary>
    /// Matches list separators (comma).
    /// </summary>
    public const string SEPARATORS = "^(,)$";
    /// <summary>
    /// Matches function calls with optional leading/trailing commas.
    /// </summary>
    public const string FUNCTIONS = @"^\,?[_a-zA-Z][_a-zA-Z0-9]*\(.+\)\,?$";
    /// <summary>
    /// Matches VHDL operators including assignment, arithmetic, and comparison.
    /// Includes: &lt;=, :=, =&gt;, +, -, *, **, /, &amp;, ', /=, &lt;, &gt;, &gt;=, mod
    /// </summary>
    public const string OPERATORS = @"^((\<\=)|\:|\;|(\=\>)|\+|\-|\:\=|\=|(\*\*)|\*|\&|\'|(\/\=)|\/|\<|(\>\=)|\>|mod)$";
    /// <summary>
    /// Matches parentheses characters.
    /// </summary>
    public const string PARENTHESIS = @"^(\(|\))$";
    /// <summary>
    /// Matches special method calls (function with opening parenthesis).
    /// </summary>
    public const string SPECIAL_METHODS = @"[_a-zA-Z]+[_a-zA-Z0-9]*\(.+";
    /// <summary>
    /// Matches complete special method calls with closing parenthesis.
    /// </summary>
    public const string SPECIAL_METHODS2 = @"[_a-zA-Z]+[_a-zA-Z0-9]*\(.+\)";
    /// <summary>
    /// Matches VHDL time unit specifiers.
    /// fs=femtoseconds, ps=picoseconds, ns=nanoseconds, us=microseconds,
    /// ms=milliseconds, sec=seconds, min=minutes, hr=hours
    /// </summary>
    public const string TIME_VARIABLES = @"^(fs|ps|ns|us|ms|sec|min|hr)$";
    /// <summary>
    /// Matches signal/variable attributes.
    /// Includes timing ('event, 'stable, 'delayed), range ('high, 'low, 'left, 'right, 'length, 'range),
    /// and introspection ('path_name, 'instance_name) attributes.
    /// </summary>
    public const string VECTORS_ATTRIBUTES = @"^[_a-zA-Z][_a-zA-Z0-9]*\'(last_value|last_event|last_active|event|stable|quiet|active|transaction|driving|driving_value|delayed|now|path_name|instance_name|base|high|low|left|right|length|range|reverse_range|ascending)";
    /// <summary>
    /// VHDL assignment operators. Anchored to prevent partial matches.
    /// &lt;= for signal assignment, := for variable assignment.
    /// </summary>
    public const string ASSIGNATION_OPERATORS = @"^((\<\=)|\:\=)$";
    /// <summary>
    /// VHDL comparison operators. Anchored to prevent partial matches.
    /// =, /= (not equal), &lt;, &gt;, &lt;=, &gt;=
    /// </summary>
    public const string COMPARATION_OPERATORS = @"^(=|!=|/=|<|>|<=|>=)$";
    /// <summary>
    /// VHDL logical operators. Anchored to prevent partial matches (e.g., "or" in "memory").
    /// Includes: and, or, xor, nand, nor, xnor, not, abs
    /// Includes shift/rotate operators: sll, srl, sla, sra, rol, ror
    /// </summary>
    public const string LOGIC_OPERATORS = @"^(and|or|xor|nand|nor|not|xnor|abs|sll|srl|sla|sra|rol|ror)$";
    /// <summary>
    /// VHDL arithmetic operators. Anchored to prevent partial matches.
    /// +, -, * (multiply), ** (power), / (divide), mod (modulo)
    /// </summary>
    public const string ARITH_OPERATORS = @"^(\+|\-|\*\*|\*|\/|mod)$";

    #endregion
}
