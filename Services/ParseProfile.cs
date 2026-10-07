using System.Diagnostics;

namespace Parser.Services;

/// <summary>
/// F-G-08 pass 5 (2026-07-22): lightweight per-run counters for the three
/// parse-side hot paths surfaced by the pass-4 probe. Populated in-place
/// by <c>Operation</c> constructor (Interpreter), <c>Tokenizer.EvaluateToken</c>
/// (Parser), and <c>Architecture.SyntetizeAssingations</c> (Interpreter).
/// Emitted by <c>HeadlessSimulator</c> when the <c>KMILA_PARSE_PROFILE</c>
/// env var is set. Off by default — the <c>Record*</c> methods early-return
/// in the hot path when disabled, so cost is one bool read per call site.
/// </summary>
/// <remarks>
/// Lives in Parser because Parser.Repositories.Tokenizer is the deepest
/// consumer, and Parser sits below Interpreter in the dependency graph
/// (Interpreter references Parser, not vice versa). Non-parse callers in
/// Interpreter reach it through the same <c>Parser.Services</c> namespace.
/// </remarks>
public static class ParseProfile
{
    /// <summary>True if <c>KMILA_PARSE_PROFILE=1</c> was set at process start.</summary>
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("KMILA_PARSE_PROFILE") == "1";

    // Counters — plain long fields, updated with Interlocked to stay
    // race-free across parallel test runs. Reset() zeros them for A/B
    // measurement inside a single process.
    private static long _operationCtorCount, _operationCtorTicks;
    private static long _tokenizerEvaluateTokenCount, _tokenizerEvaluateTokenTicks;
    private static long _syntetizeAssignationsCount, _syntetizeAssignationsTicks;

    /// <summary>Number of Operation constructor invocations recorded.</summary>
    public static long OperationCtorCount => Interlocked.Read(ref _operationCtorCount);
    /// <summary>Total time spent in Operation constructors, in milliseconds.</summary>
    public static long OperationCtorMs => TicksToMs(Interlocked.Read(ref _operationCtorTicks));
    /// <summary>Number of tokenizer EvaluateToken calls recorded.</summary>
    public static long TokenizerEvaluateTokenCount => Interlocked.Read(ref _tokenizerEvaluateTokenCount);
    /// <summary>Total time spent in tokenizer EvaluateToken, in milliseconds.</summary>
    public static long TokenizerEvaluateTokenMs => TicksToMs(Interlocked.Read(ref _tokenizerEvaluateTokenTicks));
    /// <summary>Number of SyntetizeAssignations calls recorded.</summary>
    public static long SyntetizeAssignationsCount => Interlocked.Read(ref _syntetizeAssignationsCount);
    /// <summary>Total time spent in SyntetizeAssignations, in milliseconds.</summary>
    public static long SyntetizeAssignationsMs => TicksToMs(Interlocked.Read(ref _syntetizeAssignationsTicks));

    /// <summary>Record a completed Operation constructor invocation.</summary>
    public static void RecordOperationCtor(long stopwatchTicks)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _operationCtorCount);
        Interlocked.Add(ref _operationCtorTicks, stopwatchTicks);
    }

    /// <summary>Record a Tokenizer.EvaluateToken invocation.</summary>
    public static void RecordTokenizerEvaluateToken(long stopwatchTicks)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _tokenizerEvaluateTokenCount);
        Interlocked.Add(ref _tokenizerEvaluateTokenTicks, stopwatchTicks);
    }

    /// <summary>Record an Architecture.SyntetizeAssingations invocation.</summary>
    public static void RecordSyntetizeAssignations(long stopwatchTicks)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _syntetizeAssignationsCount);
        Interlocked.Add(ref _syntetizeAssignationsTicks, stopwatchTicks);
    }

    /// <summary>Zero the counters. Used for A/B testing within one process.</summary>
    public static void Reset()
    {
        Interlocked.Exchange(ref _operationCtorCount, 0);
        Interlocked.Exchange(ref _operationCtorTicks, 0);
        Interlocked.Exchange(ref _tokenizerEvaluateTokenCount, 0);
        Interlocked.Exchange(ref _tokenizerEvaluateTokenTicks, 0);
        Interlocked.Exchange(ref _syntetizeAssignationsCount, 0);
        Interlocked.Exchange(ref _syntetizeAssignationsTicks, 0);
    }

    /// <summary>
    /// Produce a single-line summary matching the KMILA_SIM_PHASE_TRACE
    /// convention. Callers gate on <see cref="Enabled"/> before emit.
    /// </summary>
    public static string Summarize() =>
        $"[kmila-parse-profile] " +
        $"OperationCtor={OperationCtorCount} in {OperationCtorMs}ms | " +
        $"TokenizerEvaluateToken={TokenizerEvaluateTokenCount} in {TokenizerEvaluateTokenMs}ms | " +
        $"SyntetizeAssignations={SyntetizeAssignationsCount} in {SyntetizeAssignationsMs}ms";

    private static long TicksToMs(long ticks) =>
        (long)(ticks * 1000.0 / Stopwatch.Frequency);
}
