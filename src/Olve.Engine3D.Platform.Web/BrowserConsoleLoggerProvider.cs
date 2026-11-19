using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Platform.Web;

public sealed class BrowserConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BrowserConsoleLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class BrowserConsoleLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = $"{logLevel}: {categoryName}: {formatter(state, exception)}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            (logLevel >= LogLevel.Error ? Console.Error : Console.Out).WriteLine(line);
        }
    }
}
