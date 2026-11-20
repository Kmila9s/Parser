# VHDL Parser Module

## Project: Kmila-9s

This module is a core component of **Kmila-9s**, a project officially titled "Aplicación Multiplataforma para el Aprendizaje y Depuración de Código VHDL sin Requerimiento de Hardware Físico" (Multiplatform Application for Learning and Debugging VHDL Code without Requiring Physical Hardware).

**Author:** Ulrich Tamayo Daniel  
**Email:** dulricht1700@alumno.ipn.mx
**Email 2:** danielulrichtamayo@gmail.com
**Version:** 3.0.4 (branch `v3.0.4`)
**Last Updated:** 2026-11-16

> **Docs sync 2026-09-24** — no functional change to this module this cycle; the
> 2026-09-23 feature batch (v1.27.0) is **app-only** (see `App/Kmila/README.md` §19).

---

## 1. Project Overview

The primary goal of this project is to develop a free, open-source, and multiplatform desktop application for the interactive debugging of VHDL code. The application aims to facilitate the learning of digital design by providing a unified environment for simulation and real-time visualization of digital signals, clock cycles, and port states, completely eliminating the need for physical hardware like FPGAs.

### Key Motivations
*   **Accessibility:** Overcoming the financial barrier of expensive FPGA boards and proprietary software licenses.
*   **Pedagogical Improvement:** Offering an intuitive, integrated graphical interface that simplifies the debugging process, unlike the fragmented command-line workflows of tools like GHDL and GTKWave.
*   **Flexibility:** Providing a self-contained application that runs locally on multiple operating systems (Windows, macOS, Linux, Android, iOS) without requiring an internet connection.

This project is built using .NET and MAUI, ensuring a modern and maintainable codebase with broad platform support.

## 2. The Parser Module (v3.0.1)

Since v3.0.1 this project is a **library of lexical building blocks and shared
models**. It no longer contains a structural parser of its own: the
`Parser.Repositories.Parser.ParseV2()` stack (EntityParser, ArchitectureParser,
PackageParser, … — ~4.9k LOC) was **never used by the simulator** and was
removed in the multi-HDL Phase 0 dead-code stage (see
`Documentacion/auditorias/AUDIT_duplicacion_SOLID_2026-11.md`, §A3).

The production front door for VHDL is `Debugger.Repositories.Parser`
(design-unit splitting) feeding `Interpreter.Services.Entity` /
`Architecture` / `VariableSyntetizer` (synthesis of executable objects). That
path consumes the pieces this project provides:

| Area | Types | Used by |
|---|---|---|
| Lexing | `Repositories.Tokenizer`, `Repositories.TokenStream` (+ `StreamerException`) | Interpreter structures, `Operation`, `VariableSyntetizer` |
| Models | `Models.Token`, `Models.Variable`, `Models.Constants` (TYPE, DATATYPES, PortType, operator/literal regexes, `FindTypoSuggestion`) | Interpreter, Debugger, App |
| Errors & diagnostics | `Models.ParserException`, `ValidationException`, `ParserSkipOverflowException`, `Diagnostic` | Interpreter, Debugger, App, CLI |
| Services | `Services.UserTypeRegistry`, `Services.ParseProfile` | App (`ProgramBuilder`, headless/CLI pipelines) |

Tests live in `Tests/Unit/Kmila.Engine.Tests` (tokenizer, user-type registry,
variable ranges, and the former `test*.vhdl` corpus — now parsed through the
production parser, with must-reject cases kept as negative tests).

## 3. Changelog

### Version 3.1.6 (2026-11-20) — declared type mark

*   **`Variable.TypeMark`**: the declared type name (lower case: `std_logic`,
    `signed`, `time`, a user subtype…) next to the storage class `DATATYPES`,
    copied by `CopyShapeFrom`. `TypeMarks.IsResolved` (resolved vs unresolved
    types, for multi-driver checks) and `TypeMarks.SignednessOf` (numeric_std
    sign) are the single rules over it.
*   `Variable` `==`/`!=` compare by name without case, like `Equals` (`SameName`),
    and are null-correct (`v == null` with the `(Variable, string)` overload was
    inverted).

### Version 3.1.4 (2026-11-18) — atomic literals and field selectors

*   **String and character literals are one token (G56).** `"a: b"`, `"x;y"`,
    `"say ""hi"""`, `' '`, `';'` and bit strings with a base (`x"F_F"`) are never
    split on their blanks or punctuation (`Tokenizer.LiteralRe`); `report`/textio
    text comes out exactly as GHDL prints it and `x := "a b";` no longer yields an
    UNKNOWN fragment. Tabs separate tokens like blanks. `Token.IsStringLiteral`.
*   **`FIELD_SELECTOR` token type** for `.lo` after `m(i)` (was UNKNOWN +
    `IsFieldSelector`); `p.lo`/`work.pkg.x` stay one qualified identifier.
*   `Token.IsQualifierMarkText`: the single rule for the `type'` mark of a
    qualified expression.
*   A time unit name (`ns`, `us`, …) is a unit only after an abstract literal
    (`10 ns`); elsewhere it is an identifier (a label `us:` was rejected).

### Version 3.1.3 (2026-11-18) — 1-based token line

*   `Token.SourceLine`: the 1-based line of a token, the single conversion from
    the tokenizer's 0-based `Line` to the convention every model keeps
    (operation lines, branch condition lines, diagnostics).
*   `Variable.HasPlaceholderType`: the type of a value is a placeholder (result
    of a function known only by its signature); type/width checks skip it,
    evaluation is unaffected.

### Version 3.1.2 (2026-11-16) — record layouts

*   `RecordLayout` / `FieldSlot` / `FieldKind`: the bit layout of a record
    type (fields packed first-leftmost, nested records, `Leaves()` as the
    single flattening for views). `Variable.Layout`, `ElementShape` and
    `EnumLiterals` replace the dead `RecordFields` dictionary;
    `Variable.CopyShapeFrom` / `CloneShaped` and `IsInteger` are the single
    way to copy a declared shape and to tell a number from an enumeration.
*   `TokenStream.FromLines` (the one «text → token stream» path) and
    `TokenStream.Peek()`; `Token.IsFieldSelector` for the `.lo` token after
    `m(i)`; `VhdlText.TopLevelSplit` / `MatchingParen`.

### Version 3.1.1 (2026-11-16) — one index rule

*   `Variable.OffsetInRange(left, isDownto, index)`: the single
    index→position rule, used by `IndexToOffset` and by the interpreter's
    array types (`TypeDefinition.SlotOf`).

### Version 3.1.0 (2026-11-16) — positioned errors

*   `ISourcePosition` (Line/Column), implemented by `ParserException` and
    `StreamerException`; `Diagnostic.FromAnyException` turns any analysis
    failure into a diagnostic that keeps its position (no reflection).
*   `ParserException` no longer prints `:0` when the column is unknown;
    `ValidationException` takes the offending name as context.

### Version 3.0.6 (2026-11-16) — FE-8

*   New `Models/VhdlText`: `LineCommentStart` / `StripLineComment` /
    `StripLineComments` / `BlankLineComments` / `MaskStringLiterals`, the
    single string-aware home for VHDL comment and string-literal handling.
    The tokenizer, the Debugger parser and lint, `SourceElaborator`,
    `PackageSubtypeScanner`, `LibraryBuilder`, `VhdlLibraryPreparer`,
    `VhdlBlockBuilder` and the replay services use it instead of their own
    copies (six of which cut `--` inside string literals).

### Version 3.0.4 (2026-11-16) — GAP-PAR-001

*   `Tokenizer.NormalizeLiterals` (was `ConvertBasedLiterals`) also drops the
    separator underscores of bit-string literals with a base specifier —
    `x"3243_1DAC"`, `b"1010_0101"`, `O"7_7"` (IEEE 1076 §15.8). Done at token
    level so every consumer (synthesis, case choices, engine) sees plain
    digits; string literals and identifiers keep their underscores.
    `test40.vhdl` (CORDIC ROM) now parses unmodified.

### Version 3.0.1 (2026-11-15) — multi-HDL Phase 0, dead-code stage

*   Removed the unused V2 structural parser stack (`Parser.ParseV2`, legacy `Parse()`, 12 parser classes, `SharedLogging`, the 6 `Interfaces/*`, `Program.cs`) and dead members (`Constants.GetDatatype`, `METHODS`, `SPECIAL_METHODS*`, `VECTORS_IDENTIFIER`, `IF_OPERATORS`, `TokenStream.EndStream`, `ParserException.UnexpectedToken`, the file-reading `Tokenizer` constructor/`ReadFile`).
*   Project is now a plain library: no `Microsoft.Extensions.Logging*` or `xunit` package references (xunit was leaking into the app heads).
*   Tests and the `test*.vhdl` corpus moved to `Tests/Unit/Kmila.Engine.Tests`. `test40.vhdl` is tracked as known gap GAP-PAR-001 (hex literal with underscores rejected by the production parser).

### Documentation re-sync (2026-08-03, branch `v1.3.0`)

*   Updated the header stamp to the current branch (`v1.3.0`) and date. Re-checked the class reference against the on-disk tree: added `ParserSkipOverflowException` and `Diagnostic` to the Models table, `SharedLogging` to the Parsers table, and a new Services table for `UserTypeRegistry` / `ParseProfile`. Refreshed the "Strictness pass" negative-fixture list to include the third fixture (`test_negative_garbage_in_body.vhdl`), which `Program.cs` already wires. Documentation only — no parser source or public API changes.

### Version 1.14.1 (2026-05-01)

#### Documentation Verification

*   **README.md**: Cross-checked the entire document against the current `Repositories/`, `Models/`, and `Interfaces/` source. Confirmed that the orchestrator entry points are still `Parser(Tokenizer)` / `Parser(string FilePath)`, with `ParseV2()` as the recommended path and `Parse()` retained as the legacy monolithic implementation. The full `IParser` set documented here matches the `*.cs` files on disk one-to-one (14 parser classes, 6 interfaces, 5 model files including `Diagnostic`).
*   **§2 Architecture / `Parser.cs`**: Clarified that `ParseV2()` also handles inline `library`/`use` declarations between design units and walks `configuration` blocks (with the `for CompLabel : CompType use entity work.X(Arch);` binding form) via `ParseConfigurationBlock` / `ParseConfigurationBody`. Earlier versions of this document only mentioned `entity` / `architecture` / `package` switching.
*   **§6.2 Class diagram**: Added the `Diagnostic` model (with `FromException(ParserException)` static adapter) and the `DiagnosticSeverity` enumeration to the Models cluster.
*   **§6.4 Module diagram**: Added the `Diagnostic` / `DiagnosticSeverity` node to the Models subgraph and an explicit edge into the editor surface (`Kmila.Shared.Pages.Editor`) showing how `ParserException` is adapted via `Diagnostic.FromException(...)` to feed Monaco markers.
*   No public API or runtime behavior changes in this release; the parser surface remains backwards-compatible with the v1.14.0 release that introduced the diagnostics pipeline.

---

### Version 0.13.1 (2026-02-21)

#### Documentation Enhancements

*   **Variable.cs**: Added comprehensive XML documentation for all constructors, operators, backing fields (`_size`, `_value`), `IsPort`, `PortType`, `ToString()`, implicit conversion, and equality/inequality operators
*   **DataStructuresParser.cs**: Added XML docs for `ParseDeclaration()` (throws NotImplementedException), and documented private field groups
*   **VectorParser.cs**: Added XML docs for `CheckVariable()` protected method and `IsVector()` private method
*   **TokenizerTests.cs**: Added XML documentation for test class and test method with parameter descriptions
*   **Program.cs**: Added XML documentation for the test runner class and its entry point

---

### Version 0.13.0 (2025-12-13)

This version includes comprehensive documentation improvements, bug fixes, and minor code enhancements.

#### Bug Fixes

*   **Memory Leak Fix in TokenStream**: Fixed significant memory leak caused by each `TokenStream` instance creating its own `LoggerFactory`. Now uses a static shared `LoggerFactory` to prevent memory accumulation during repeated parsing operations.

#### Documentation Enhancements

*   **README Updates**: Added comprehensive documentation for previously undocumented features:
    *   Standard Library Functions: Complete table of 50+ recognized IEEE standard library functions
    *   Block Statement Support: Detailed explanation with VHDL code examples
    *   Component Instantiation: Full documentation with generic/port map examples
    *   Scope Naming Convention: Explanation of `{entityName}_{archName}` pattern
    *   TokenStream Rollback Feature: Usage documentation with code examples

*   **Constants.cs Documentation**: Added XML documentation to all enum values and regex patterns:
    *   `TYPE` enum: Each token type now has detailed description
    *   `ParseExceptions` enum: Each error code has explanation with examples
    *   `DATATYPES` enum: Each data type has description of its VHDL semantics
    *   `OperatorType` enum: Each operator category is documented
    *   `PortType` enum: Input/output port descriptions
    *   All regex patterns: Added examples and explanations for each pattern

#### Updates (from 2025-12-09)

*   **Shift/Rotate Operators in LOGIC_OPERATORS**: Added `sll`, `srl`, `sla`, `sra`, `rol`, `ror` to the `LOGIC_OPERATORS` regex pattern. These operators were already in KEYWORDS but were not classified as logical operators, causing issues in the Interpreter's expression evaluation.

---

### Version 0.12.5 (2025-12-09)

This version adds shift and rotate operators to LOGIC_OPERATORS for proper classification by downstream interpreter.

#### Updates

*   **Shift/Rotate Operators in LOGIC_OPERATORS**: Added `sll`, `srl`, `sla`, `sra`, `rol`, `ror` to the `LOGIC_OPERATORS` regex pattern. These operators were already in KEYWORDS but were not classified as logical operators, causing issues in the Interpreter's expression evaluation.

---

### Version 0.12.5 (2025-12-09)

This version fixes the remaining test failures (tests 18, 19, 20) and adds support for advanced VHDL constructs including block statements, guarded signals, signal attributes, and complex conditional generates.

#### New Features

*   **Block Statement Support**: Full parsing of VHDL block statements with:
    *   Guard expressions (e.g., `block (guard_signal = '1')`)
    *   Local signal/constant declarations within blocks
    *   Nested blocks (blocks inside blocks)
    *   Labeled blocks with optional end labels
*   **Guarded Signal Assignments**: Support for guarded concurrent signal assignments with the `guarded` keyword
*   **Signal Attributes with Arguments**: Parsing of signal attributes with time arguments:
    *   `'stable(time)` - signal stability check
    *   `'delayed(time)` - delayed signal value
    *   `'last_value`, `'last_event`, `'event` in conditional expressions
*   **Type Conversion Functions in Conditionals**: Support for IEEE numeric_std type conversion functions in if/when conditions:
    *   `unsigned()`, `signed()`, `to_integer()`, `to_unsigned()`, `to_signed()`
    *   `resize()`, `shift_left()`, `shift_right()`
    *   `std_logic_vector()`, `integer()`, `natural()`
*   **Configuration Architecture Binding**: Support for architecture specification in configuration blocks:
    *   `use entity work.Component(Architecture);` syntax
*   **Enhanced Operator Support**: Added `/=` (not-equal) and `>=` (greater-equal) as properly tokenized operators
*   **Package Constants in Generics**: Generic default values can now reference package constants (identifiers), not just literals
*   **Custom Types in Ports**: Port declarations now accept user-defined types (identifiers) in addition to standard VHDL types

#### Bug Fixes

*   **Fixed infinite loop in `ParseOperation()`**: Corrected while loop condition that prevented proper termination on semicolon
*   **Fixed `QUALIFIED_IDENTIFIERS` regex**: Now allows digits in the first identifier segment (e.g., `s_axi_s2m.bvalid` where `s2m` contains a digit)
*   **Fixed nested block parsing**: `ParseBlockBody()` now properly recognizes and handles nested block statements
*   **Fixed configuration block parsing**: Added support for architecture binding syntax with parentheses
*   **Added fallback for unknown tokens**: `ParseSingleStatement()` now gracefully skips unknown tokens to the next semicolon instead of causing infinite loops

#### Test File Fixes

*   **test20.vhdl**: Fixed VHDL identifier case conflicts by renaming state machine constants:
    *   `TX_DATA` → `TX_ST_DATA` (to avoid conflict with `tx_data` signal)
    *   `RX_DATA` → `RX_ST_DATA` (to avoid conflict with `rx_data` signal)
    *   Similar renames for `TX_IDLE`, `TX_START`, `TX_STOP`, `RX_IDLE`, `RX_START`, `RX_STOP`

#### Test Coverage

*   All 43 test cases now pass:
    *   test1-10: Basic to intermediate VHDL constructs
    *   test11-17: Generate statements, multi-entity designs, labeled processes
    *   test18: Advanced signal attributes, guarded signals, block statements, configuration
    *   test19: Complex conditional generates with type conversions
    *   test20: Full SoC-style design with packages, records, multiple entities, and configuration

---

### Version 0.12.4 (2025-12-08)

This version includes significant improvements to the parser's handling of complex VHDL constructs, particularly around concurrent vs sequential statement parsing, scope management, and enhanced error reporting.

#### New Features

*   **Generate Statement Support**: Full parsing of `for-generate` and `if-generate` statements, including:
    *   Nested generate statements
    *   Local signal/constant declarations within generate blocks
    *   Iterator variable scoping (automatically removed after generate completes)
*   **Labeled Process Support**: Parsing of processes with labels (e.g., `my_proc : process(clk) begin ... end process my_proc;`)
*   **While Loop Support**: Added `while condition loop ... end loop;` parsing
*   **Additional Sequential Statements**: Support for `exit`, `wait`, and `null` statements
*   **Extended Keywords**: Added support for many additional VHDL keywords including `nand`, `nor`, `xnor`, `sll`, `srl`, `sla`, `sra`, `rol`, `ror`, `abs`, `rem`, `transport`, `reject`, `inertial`, `unaffected`, and more
*   **Vector Attributes**: Extended support for signal attributes like `'last_value`, `'last_event`, `'event`, `'stable`, `'quiet`, `'active`, `'transaction`, `'delayed`, `'now`, `'path_name`, `'instance_name`, `'base`, `'high`, `'low`, `'left`, `'right`, `'length`, `'range`, `'reverse_range`, `'ascending`, etc.
*   **Configuration Block Parsing**: Added support for parsing VHDL `configuration` blocks with nested `for` statements
*   **Component Declarations**: Full parsing of `component` declarations within architecture headers, including optional `generic` and `port` blocks
*   **Component Instantiation**: Support for component instantiations with `port map` and `generic map`
*   **Process Variable Declarations**: Parsing of `variable` and `constant` declarations within process bodies before `begin`
*   **Aggregate Expressions**: Support for aggregate expressions like `(others => '0')` in signal/constant initializations
*   **Function Call Parsing**: Proper parsing of function calls in expressions (e.g., `conv_integer(x)`, `clog2(N)`)
*   **Indexed Signal Access**: Parsing of indexed signal/variable access like `signal(7 downto 0)` or `signal(i)`
*   **Attribute Declarations**: Support for `attribute` declarations and specifications
*   **Disconnect Specifications**: Parsing of `disconnect` specifications for guarded signals

#### Enhanced Error Reporting

*   **`ParserException`**: Completely rewritten with detailed error messages including:
    *   Line and column numbers (1-based)
    *   Context information (near which token the error occurred)
    *   Predefined error types: `INITIALIZATION`, `DUPLICATE_DECLARATION`, `MISSING_STRUCTURE`, `DUPLICATE_STRUCTURE`, `MISSING_DECLARATION`, `SYNTAX_ERROR`
    *   Factory methods: `UnexpectedToken()`, `UndeclaredVariable()`, `TypeMismatch()`
*   **`StreamerException`**: Enhanced with detailed context:
    *   Human-readable type descriptions (e.g., "keyword (e.g., 'process', 'signal', 'begin', 'end')")
    *   Tracks last valid token for better error location when hitting end-of-input

#### Key Architectural Changes

1.  **Concurrent vs Sequential Statement Parsing** (`DataStructuresParser.cs`)
    *   Added `ParseSingleStatement()` method for parsing exactly ONE concurrent statement (used in architecture body context)
    *   Added `ParseSequentialBody()` method for parsing ALL sequential statements until `end` keyword (used in process/function bodies)
    *   The `Parse()` method now uses `ParseSingleStatement()` to prevent incorrect looping through architecture body constructs

2.  **Architecture Scope Naming** (`ArchitectureParser.cs`)
    *   Architecture scopes now use `{entityName}_{archName}` as the key
    *   This allows multiple architectures with the same name for different entities (e.g., both `Adder` and `PipelineReg` can have `Behavioral` architectures)

3.  **Iterator Variable Scoping**
    *   `for-loop` and `for-generate` iterator variables are now properly scoped
    *   Variables are removed from scope after the loop/generate completes
    *   Prevents "duplicate declaration" errors when the same iterator name is reused

4.  **Function Body Parsing** (`FunctionParser.cs`)
    *   Now uses `ParseSequentialBody()` instead of `Parse()` to correctly handle multiple statements in function bodies
    *   Support for return type bounds with complex expressions (e.g., `std_logic_vector((A'length + B'length) - 1 downto 0)`)
    *   Parsing of local variable/constant declarations before `begin`

5.  **Variable Model Enhancements** (`Variable.cs`)
    *   Extended `GetDatatype()` to support additional types: `std_ulogic`, `signed`, `unsigned`, `bit_vector`, `boolean_vector`, `integer_vector`, `positive`, `string`, `time`, `real`
    *   Improved handling of `(others => 'X')` aggregate patterns
    *   Support for concatenation expressions `(concat:...)`
    *   Case-insensitive variable comparison (VHDL identifiers are case-insensitive)

#### Bug Fixes

*   Fixed issue where generate labels were incorrectly parsed as signal assignments
*   Fixed issue where component instantiation labels caused "instance not in scope" errors
*   Fixed duplicate structure errors when multiple architectures shared the same name
*   Fixed function body parsing stopping after first statement
*   Fixed `while` loop body parsing returning prematurely
*   Fixed vector type bounds not being parsed in function parameters
*   Fixed return expression parsing in functions

#### Test Coverage

*   All 20 test cases now pass, covering:
    *   Basic entity/architecture parsing (test1-5)
    *   Complex expressions and operators (test6-10)
    *   Generate statements with local declarations (test11-12)
    *   Multi-entity designs with shared architecture names (test11, test17)
    *   Labeled processes and nested control structures (test13-16)
    *   Component hierarchies and instantiations (test17-20)

---

## Changelog

### Version 1.14.0 (2026-04-19)

*   **Structured diagnostics** — new `Parser.Models.Diagnostic` type with
    `DiagnosticSeverity` (Info / Warning / Error) and line / column / code /
    message. `Diagnostic.FromException(ParserException)` adapts the existing
    exception-based flow into the new collection without breaking callers.
*   **Linter hook** — downstream callers (see `Debugger.Repositories.Parser`)
    can now run a non-fatal pass that reports missing `;` at statement ends
    and `begin` / `end` block imbalance *before* the full parser runs.
    Addresses the long-standing complaint that the parser silently accepted
    sources missing terminators at simulation time.
*   **Editor integration** — these diagnostics are mapped to Monaco's
    `setModelMarkers` API by `Kmila.Shared/Pages/Editor.razor`, so syntax
    errors now light up in the gutter instead of appearing only in a toast.
*   **Build hygiene** — added `<NoWarn>` for the XML-doc ripple
    (CS1587 / CS1591 / CS1570) caused by the comma-separated const lists in
    `Models/Constants.cs`. The project builds warning-free without the churn
    of splitting each regex constant into its own declaration.

---

## What's new in v1.15 (2 May 2026)

Parser surface now accepts:
- `procedure name(...) is ... end name;` in the architecture header (#12).
- Direct entity instantiation `label : entity work.Sub[(arch)] port map(...)` (#13).
- Generic defaults with arithmetic expressions, e.g. `TOTAL : integer := BASE * TIMES` (#16).
- Line numbers in error diagnostics now reference the user's source file (not the tokenizer's call counter) via the new `Tokenizer.TokenizeLineAt(text, sourceLineZeroBased)` overload (#21).

Memory: `Tokenizer.Evaluate()` now releases the raw `_tokens` buffer once `_typeDictionary` is built. Workstation GC enabled at the project level for low-RAM device targeting (#7).

Full release notes: [`Documentacion/CHANGELOG_v1.15.md`](../Documentacion/CHANGELOG_v1.15.md).

---

## Strictness pass (2026-05-17)

`PortsParser` now rejects two classes of port-declaration sloppiness that previously slipped through silently. Both were filed by Daniel as part of the ripple-counter "Visible Wave" repro that built clean despite obvious typos — see [`Documentacion/CHANGELOG_concepts_module_2026-05-17.md`](../Documentacion/CHANGELOG_concepts_module_2026-05-17.md) addendum and the memory entry `project_vhdl_parser_too_lenient.md`.

### Unknown port type → "did you mean …" error

When the type identifier in a port declaration is captured as an IDENTIFIER (not a recognised KEYWORD) AND its case-folded value is within Levenshtein-distance 2 of any canonical VHDL type name in `Constants.KNOWN_TYPE_NAMES`, the parser now throws a `ParserException` with a "Did you mean '<canonical>'?" hint. Catches the common slips:

| Source token | Canonical | Distance | Behaviour |
|---|---|---|---|
| `STDLOGIC` | `std_logic` | 1 (missing `_`) | **rejected** |
| `STD_LGIC` | `std_logic` | 1 (missing `O`) | **rejected** |
| `coefficient_array_t` | none close | > 2 | accepted as user-defined type |

The Levenshtein helper lives at `Constants.cs:LevenshteinDistance` (private, rolling two-row buffer). `Constants.FindTypoSuggestion(string)` is the public entry — pass the source identifier, get back the closest canonical name or `null` when the identifier looks legitimate.

### Missing `;` between ports

The `Parse()` loop now detects the case where a port declaration finishes and the next token is another IDENTIFIER (rather than `;` or `)`). It surfaces a `ParserException` reading "Missing ';' before port '<name>' in port declaration list." with the offending token's line + column — instead of letting the parse cascade into a confusing "expected )" message far below the actual problem.

### Negative test suite

`Program.cs` gained a second test pass for "must-fail" fixtures. Each entry is a `(filename, mustContainSubstring)` pair; a PASS means the parser surfaced the expected exception and the message contained the required substring. The runner's exit code is non-zero unless **both** the 40 positive AND every negative fixture pass — so regressions can't silently re-loosen the strictness.

Current negative coverage (Parser root, alongside the existing `test1..test40.vhdl`):

- `test_negative_unknown_type.vhdl` — typo `STDLOGIC` in a port type (must contain `STDLOGIC`).
- `test_negative_missing_port_semi.vhdl` — port lacks `;` before the next port (must contain `Missing ';'`).
- `test_negative_garbage_in_body.vhdl` — stray `$` token in the architecture body (must contain `Unexpected token '$'`).

Adding more is one entry in the `negativeTests` array + a `test_negative_*.vhdl` file at the Parser root.

---
*Last updated: 2026-08-03*