

using System.Text;

namespace LoggerUsage;

// Take from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Abstractions/src/LogValuesFormatter.cs
internal class LogValuesFormatter
{
    private readonly List<string> _valueNames = [];

    public LogValuesFormatter(string format)
    {
        var vsb = new StringBuilder(256);
        int scanIndex = 0;
        int endIndex = format.Length;

        while (scanIndex < endIndex)
        {
            int openBraceIndex = FindBraceIndex(format, '{', scanIndex, endIndex);
            if (scanIndex == 0 && openBraceIndex == endIndex)
            {
                return;
            }

            int closeBraceIndex = FindBraceIndex(format, '}', openBraceIndex, endIndex);

            if (closeBraceIndex == endIndex)
            {
                vsb.Append(format.AsSpan(scanIndex, endIndex - scanIndex));
                scanIndex = endIndex;
            }
            else
            {
                // Format item syntax : { index[,alignment][ :formatString] }.
                int formatDelimiterIndex = format.AsSpan(openBraceIndex, closeBraceIndex - openBraceIndex).IndexOfAny(',', ':');
                formatDelimiterIndex = formatDelimiterIndex < 0 ? closeBraceIndex : formatDelimiterIndex + openBraceIndex;

                vsb.Append(format.AsSpan(scanIndex, openBraceIndex - scanIndex + 1));
                vsb.Append(_valueNames.Count.ToString());
                _valueNames.Add(format.Substring(openBraceIndex + 1, formatDelimiterIndex - openBraceIndex - 1));
                vsb.Append(format.AsSpan(formatDelimiterIndex, closeBraceIndex - formatDelimiterIndex + 1));

                scanIndex = closeBraceIndex + 1;
            }
        }
    }

    public List<string> ValueNames => _valueNames;

    private static int FindBraceIndex(string format, char brace, int startIndex, int endIndex)
    {
        // Example: {{prefix{{{Argument}}}suffix}}.
        int braceIndex = endIndex;
        int scanIndex = startIndex;
        int braceOccurrenceCount = 0;

        while (scanIndex < endIndex)
        {
            if (braceOccurrenceCount > 0 && format[scanIndex] != brace)
            {
                if (braceOccurrenceCount % 2 == 0)
                {
                    // Even number of '{' or '}' found. Proceed search with next occurrence of '{' or '}'.
                    braceOccurrenceCount = 0;
                    braceIndex = endIndex;
                }
                else
                {
                    // An unescaped '{' or '}' found.
                    break;
                }
            }
            else if (format[scanIndex] == brace)
            {
                if (brace == '}')
                {
                    if (braceOccurrenceCount == 0)
                    {
                        // For '}' pick the first occurrence.
                        braceIndex = scanIndex;
                    }
                }
                else
                {
                    // For '{' pick the last occurrence.
                    braceIndex = scanIndex;
                }

                braceOccurrenceCount++;
            }

            scanIndex++;
        }

        return braceIndex;
    }

}
