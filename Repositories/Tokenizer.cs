using Parser.Models;
using Parser.Services;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Parser.Repositories;

/// <summary>
/// Responsible for reading VHDL source code (from a string) and converting it into a sequence of tokens.
/// </summary>
public class Tokenizer{
    /// <summary>
    /// Gets the last error message that occurred during tokenization.
    /// </summary>
    public string LastError { get; private set; }
    /// <summary>Per-line buckets of raw token strings paired with the originating 0-based line index.</summary>
    private List<(List<string>, int)> _tokens;
    /// <summary>Final classified token list, populated by <see cref="Evaluate"/>.</summary>
    private List<Token> _typeDictionary;
    /// <summary>
    /// Compiled token-classification regexes, one per <see cref="Constants.TYPE"/>.
    /// Order of evaluation in <see cref="Evaluate"/> is significant — operators
    /// and parenthesis are matched before identifiers so symbolic tokens are
    /// not swallowed as names.
    /// </summary>
    /// <remarks>
    /// F-G-08 pass 5 (2026-07-22): hoisted from per-instance fields to
    /// static-readonly. Each Regex here uses <see cref="RegexOptions.Compiled"/>
    /// which IL-emits the state machine on first construction; the
    /// pre-pass-5 shape allocated all 10 regexes per <c>new Tokenizer(...)</c>
    /// call. Because <c>Operation..ctor</c> calls <c>new Tokenizer()</c>
    /// on every concurrent assignment, the elaborated capstone paid
    /// hundreds × 10 compile-to-IL passes before the delta engine even
    /// started. Static fields collapse this to 10 one-shot compilations
    /// per process. Regex objects with <c>Compiled</c> are thread-safe
    /// per Microsoft docs — safe to share.
    /// </remarks>
    private static readonly RegexOptions _regexIgnoreCase = RegexOptions.IgnoreCase | RegexOptions.Compiled;
    private static readonly RegexOptions _regexCompiled = RegexOptions.Compiled;
    private static readonly Regex _keywordsRegex = new(Constants.KEYWORDS, _regexIgnoreCase);
    private static readonly Regex _operatorsRegex = new(Constants.OPERATORS, _regexCompiled);
    private static readonly Regex _parenthesisRegex = new(Constants.PARENTHESIS, _regexCompiled);
    private static readonly Regex _literalsRegex = new(Constants.LITERALS, _regexIgnoreCase);
    private static readonly Regex _qualifiedIdentifiersRegex = new(Constants.QUALIFIED_IDENTIFIERS, _regexCompiled);
    private static readonly Regex _identifiersRegex = new(Constants.IDENTIFIERS, _regexCompiled);
    private static readonly Regex _separatorsRegex = new(Constants.SEPARATORS, _regexCompiled);
    private static readonly Regex _functionsRegex = new(Constants.FUNCTIONS, _regexCompiled);
    private static readonly Regex _timeRegex = new(Constants.TIME_VARIABLES, RegexOptions.IgnoreCase);
    private static readonly Regex _vectorAttributes = new(Constants.VECTORS_ATTRIBUTES, _regexCompiled);
    /// <summary>Current 0-based line index being processed; advanced by <see cref="ReadString"/>.</summary>
    private int _currentLine = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Tokenizer"/> class.
    /// </summary>
    public Tokenizer()
    {
        LastError = "";
        _tokens = new();
        _typeDictionary = new();
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
        line = NormalizeLiterals(line);
        var preTokens = TokenizeSpaces(line.Trim());
        foreach(var token in preTokens) tokens.AddRange(TokenizeSpecialCharacters(token.Trim()));
        _tokens.Add((tokens, _currentLine ++));
    }

    /// <summary>
    /// Compiled regex matching VHDL based-integer literals like
    /// `2#1010#`, `8#777#`, `10#42#`, `16#FF_E0#`. The lexer splits on
    /// `#` as a special character, which would break these literals into
    /// three tokens, so we convert them to their decimal-integer
    /// equivalent before the special-character pass runs. The semantics
    /// are preserved — these are integer literals, and a plain decimal
    /// integer occupies the same VHDL grammar slot.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex BasedLiteralRe =
        new(@"(?<![\w#])(?<base>\d+)#(?<digits>[0-9A-Fa-f_]+)#",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Matches VHDL bit-string literals with a base specifier — <c>B"…"</c>,
    /// <c>O"…"</c>, <c>X"…"</c> (any case). IEEE 1076 allows underscores between
    /// their digits as visual separators (e.g. <c>x"3243_1DAC"</c>).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex BitStringLiteralRe =
        new(@"(?<![\w""])(?<base>[bBoOxX])""(?<digits>[0-9A-Fa-f_]*)""",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Rewrites literal forms the token classifier can't take as written:
    /// based integers (bases 2..16) become their decimal value, and the
    /// separator underscores of bit-string literals are dropped. Both keep
    /// the literal's meaning; this is the single place every consumer of
    /// literal tokens (synthesis, case choices, engine) benefits from.
    /// </summary>
    private static string NormalizeLiterals(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        if (line.Contains('#'))
        {
            line = BasedLiteralRe.Replace(line, m =>
            {
                if (!int.TryParse(m.Groups["base"].Value, out var b) || b < 2 || b > 16) return m.Value;
                var digits = m.Groups["digits"].Value.Replace("_", string.Empty);
                try { return Convert.ToInt64(digits, b).ToString(System.Globalization.CultureInfo.InvariantCulture); }
                catch { return m.Value; }
            });
        }
        if (line.Contains('_') && line.Contains('"'))
        {
            line = BitStringLiteralRe.Replace(line, m =>
                m.Groups["digits"].Value.Contains('_')
                    ? $"{m.Groups["base"].Value}\"{m.Groups["digits"].Value.Replace("_", string.Empty)}\""
                    : m.Value);
        }
        return line;
    }

    /// <summary>
    /// Same as <see cref="TokenizeLine(string)"/> but stamps the produced
    /// tokens with an explicit source-file line index rather than the
    /// internal call-order counter. Used by callers that have already
    /// stripped blank/comment-only lines from the input but still want
    /// downstream diagnostics to reference original file line numbers.
    /// </summary>
    /// <param name="line">The line of code to tokenize.</param>
    /// <param name="sourceLineZeroBased">
    /// 0-based original file line index. The error formatter adds +1
    /// when rendering it as "Line N", so callers should pass the index
    /// (i.e. <c>i</c> from <c>for (int i = 0; ...)</c>) not the 1-based
    /// number.
    /// </param>
    public void TokenizeLineAt(string line, int sourceLineZeroBased){
        List<string> tokens = new();
        line = NormalizeLiterals(line);
        var preTokens = TokenizeSpaces(line.Trim());
        foreach(var token in preTokens) tokens.AddRange(TokenizeSpecialCharacters(token.Trim()));
        _tokens.Add((tokens, sourceLineZeroBased));
        // Keep _currentLine aligned so any later TokenizeLine() call lands
        // beyond the highest seen number rather than reverting to 0.
        if (sourceLineZeroBased >= _currentLine) _currentLine = sourceLineZeroBased + 1;
    }
    /// <summary>
    /// Tokenizes a single line and returns the raw string tokens.
    /// </summary>
    /// <param name="line">The line of code to tokenize.</param>
    /// <returns>An array of string tokens.</returns>
    public string[] TokenizeLineReturn(string line){
        List<string> tokens = new();
        line = NormalizeLiterals(line);
        var preTokens = TokenizeSpaces(line);
        foreach(var token in preTokens) tokens.AddRange(TokenizeSpecialCharacters(token));
        return tokens.ToArray();
    }
    /// <summary>
    /// VHDL string, bit-string and character literals, which are indivisible
    /// tokens (G56): <c>"…"</c> with <c>""</c> as an escaped quote, optionally
    /// glued to a base specifier (<c>x"FF"</c>, VHDL-2008 <c>12ux"F"</c>), and
    /// <c>'c'</c> when the tick does not follow a name (<c>a'event</c>,
    /// <c>t'(…)</c> and <c>s(1)'length</c> are attributes/qualifications).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex LiteralRe =
        new(@"(?<!\w)\d*[uUsS]?[bBoOxXdD]""(?:[^""]|"""")*""|""(?:[^""]|"""")*""|(?<![\w)\]])'.'",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Splits a line on blanks and tabs outside literals (a literal is never
    /// split: <c>"a: b"</c> stays one piece) after removing its comment.
    /// </summary>
    private List<string> TokenizeSpaces(string line){
        try{
            line = line.Trim();
            if(line.StartsWith("//")) return new();
            // Line comment out, but never a `--` inside a string literal (F-G-12a).
            line = VhdlText.StripLineComment(line);
            if(String.IsNullOrEmpty(line)) return new();
            var pieces = new List<string>();
            var piece = new System.Text.StringBuilder();
            int i = 0;
            // Most lines hold no literal: no regex pass then.
            var literals = line.AsSpan().IndexOfAny('"', '\'') < 0
                ? (IEnumerable<System.Text.RegularExpressions.Match>)[]
                : LiteralRe.Matches(line);
            foreach (var literal in literals)
            {
                AppendSplitOnBlanks(line, i, literal.Index, piece, pieces);
                piece.Append(literal.Value);
                i = literal.Index + literal.Length;
            }
            AppendSplitOnBlanks(line, i, line.Length, piece, pieces);
            if (piece.Length > 0) pieces.Add(piece.ToString());
            return pieces;
        }catch(Exception ex){
            LastError = ex.Message;
        }
        return new();
    }

    /// <summary>Appends <c>line[from..to)</c> to the current piece, closing a piece at each blank or tab.</summary>
    private static void AppendSplitOnBlanks(string line, int from, int to, System.Text.StringBuilder piece, List<string> pieces)
    {
        for (int i = from; i < to; i++)
        {
            char c = line[i];
            if (c != ' ' && c != '\t') { piece.Append(c); continue; }
            if (piece.Length > 0) { pieces.Add(piece.ToString()); piece.Clear(); }
        }
    }

    /// <summary>
    /// Splits a token by special characters (e.g., ':=', ';', '(').
    /// </summary>
    private List<string> TokenizeSpecialCharacters(string line){
        try{
            List<string> tokens = new(), separators = new(){ ":=", "<=", "=>", "**", "/=", ">="};
            // F-G-12a (2026-07-18): removed `if(line.StartsWith("--")) return new();`
            // — this was a per-token safety net for the days when
            // TokenizeSpaces couldn't reliably strip line comments; it
            // fired on the recursive split of a string like `"--------"`,
            // dropping every 7-and-shorter dash run and mangling the
            // resulting message text. Comments are now correctly stripped
            // at line level by the string-aware pass in TokenizeSpaces.
            if(String.IsNullOrEmpty(line)) return new();

            // A literal is one token wherever it sits in the piece (Tier 4.2:
            // `"10--"` must not be shredded by the `-` splitter; G56: neither
            // `"a: b"` nor `';'`); the text around it is split as usual.
            var literal = line.AsSpan().IndexOfAny('"', '\'') < 0 ? System.Text.RegularExpressions.Match.Empty : LiteralRe.Match(line);
            if (literal.Success)
            {
                tokens.AddRange(TokenizeSpecialCharacters(line[..literal.Index]));
                tokens.Add(literal.Value);
                tokens.AddRange(TokenizeSpecialCharacters(line[(literal.Index + literal.Length)..]));
                return tokens;
            }
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
    public void Evaluate()
    {
        _typeDictionary = EvaluateTokens();
        // Drop the raw pre-evaluation token buffer — once Evaluate() has
        // built _typeDictionary nobody else reads `_tokens`, but it used
        // to stay alive for the lifetime of the Tokenizer instance
        // (Entity / Architecture), doubling the in-memory cost of every
        // parsed source file.
        _tokens.Clear();
        _tokens.TrimExcess();
    }
    /// <summary>
    /// Classifies a single raw string token into a <see cref="Token"/> object using regular expressions.
    /// </summary>
    /// <param name="token">The raw string token.</param>
    /// <returns>A classified <see cref="Token"/>.</returns>
    private Token EvaluateToken(string token){
        var _profileStopwatch = ParseProfile.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
        var evaluationToken = token.ToLower();
        Token result;
        if(_keywordsRegex.IsMatch(evaluationToken))
            result = new(token, Constants.TYPE.KEYWORDS);
        else if(_operatorsRegex.IsMatch(evaluationToken))
            result = new(token, Constants.TYPE.OPERATORS);
        else if(_parenthesisRegex.IsMatch(evaluationToken))
            result = new(token, Constants.TYPE.PARENTHESIS);
        else if(_literalsRegex.IsMatch(evaluationToken))
            result = new(token, Constants.TYPE.LITERALS);
        else if(_qualifiedIdentifiersRegex.IsMatch(evaluationToken))
            result = new(token, Constants.TYPE.QUALIFIED_IDENTIFIERS);
        else if(_timeRegex.IsMatch(token.ToLower()))
            result = new(token, Constants.TYPE.TIME_VARIABLES);
        else if(_identifiersRegex.IsMatch(evaluationToken))
            result = new(token, Constants.TYPE.IDENTIFIERS);
        else if(_separatorsRegex.IsMatch(evaluationToken))
            result = new(token, Constants.TYPE.SEPARATORS);
        else if(_functionsRegex.IsMatch(evaluationToken))
            result = new(token, Constants.TYPE.METHODS);
        else if(_vectorAttributes.IsMatch(evaluationToken))
            result = new(token, Constants.TYPE.VECTORS_ATTRIBUTES);
        else if(Token.IsFieldSelectorText(token))
            result = new(token, Constants.TYPE.FIELD_SELECTOR);
        else
            result = new(token, Constants.TYPE.UNKNOWN);
        if (_profileStopwatch != null)
        {
            _profileStopwatch.Stop();
            ParseProfile.RecordTokenizerEvaluateToken(_profileStopwatch.ElapsedTicks);
        }
        return result;
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
                // A time unit name is a unit only after an abstract literal (`10 ns`);
                // anywhere else it is an identifier (a label `us:`, a signal `ns`).
                if (token.Type == Constants.TYPE.TIME_VARIABLES && token.Value.All(char.IsLetter)
                    && !(evaluation.Count > 0 && evaluation[^1].Value.Length > 0 && char.IsDigit(evaluation[^1].Value[^1])))
                    token.Type = Constants.TYPE.IDENTIFIERS;
                token.Line = line;
                token.Col = currentCol++;
                evaluation.Add(token);
            });
        });
        return evaluation;
    }
}