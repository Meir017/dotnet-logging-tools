using LoggerUsage.Models;

namespace LoggerUsage.Cli;

/// <summary>
/// Handles progress reporting by displaying a console progress bar.
/// </summary>
internal class ProgressBarHandler
{
    private int _lastLineLength = 0;
    private readonly object _lock = new();

    /// <summary>
    /// Reports progress by updating the console progress bar.
    /// </summary>
    /// <param name="progress">The progress information to display.</param>
    public void Report(LoggerUsageProgress progress)
    {
        lock (_lock)
        {
            // Clear previous line
            if (_lastLineLength > 0)
            {
                Console.Write('\r' + new string(' ', _lastLineLength) + '\r');
            }

            // Calculate bar length based on console width (leave room for text)
            var consoleWidth = GetConsoleWidth();
            var availableWidth = Math.Max(consoleWidth - 40, 20); // Reserve 40 chars for percentage and text
            var barLength = Math.Min(availableWidth, 50);

            // Calculate filled portion
            var filledLength = (int)(barLength * progress.PercentComplete / 100.0);
            var emptyLength = barLength - filledLength;

            // Build progress bar
            var bar = $"[{new string('█', filledLength)}{new string('░', emptyLength)}]";
            var message = $"{bar} {progress.PercentComplete,3}% {progress.OperationDescription}";

            // Truncate if too long
            if (message.Length > consoleWidth - 1)
            {
                message = message.Substring(0, consoleWidth - 4) + "...";
            }

            Console.Write(message);
            _lastLineLength = message.Length;
        }
    }

    /// <summary>
    /// Clears the progress bar from the console.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            if (_lastLineLength > 0)
            {
                Console.Write('\r' + new string(' ', _lastLineLength) + '\r');
                _lastLineLength = 0;
            }
        }
    }

    private static int GetConsoleWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch
        {
            // If console width is not available (e.g., when output is redirected), use default
            return 120;
        }
    }
}
