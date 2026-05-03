using Microsoft.Extensions.Logging;

namespace Parser.Repositories;

internal static class SharedLogging
{
    public static readonly ILoggerFactory Factory =
        LoggerFactory.Create(builder => builder.AddConsole());
}
