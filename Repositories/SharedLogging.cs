using Microsoft.Extensions.Logging;

namespace Parser.Repositories;

/// <summary>
/// Process-wide <see cref="ILoggerFactory"/> for the Parser module. Used by
/// the structural and declaration parsers to obtain typed loggers without
/// requiring DI in console / test contexts.
/// </summary>
internal static class SharedLogging
{
    /// <summary>Singleton <see cref="ILoggerFactory"/> wired to the console provider.</summary>
    public static readonly ILoggerFactory Factory =
        LoggerFactory.Create(builder => builder.AddConsole());
}
