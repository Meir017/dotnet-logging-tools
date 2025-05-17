using LoggerUsage.Models;
using System.Net;

namespace LoggerUsage.Cli.ReportGenerator;

public class HtmlLoggerReportGenerator : ILoggerReportGenerator
{
    public string GenerateReport(List<LoggerUsageInfo> results)
    {
        string css = @"
        <style>
            body { font-family: 'Segoe UI', Arial, sans-serif; margin: 2em; background: #f8f9fa; }
            h1 { color: #333; }
            table { border-collapse: collapse; width: 100%; background: #fff; box-shadow: 0 2px 8px #0001; }
            th, td { border: 1px solid #dee2e6; padding: 8px 12px; text-align: left; }
            th { background: #0078d4; color: #fff; }
            tr:nth-child(even) { background: #f2f2f2; }
            .loglevel-Trace { color: #6c757d; }
            .loglevel-Debug { color: #17a2b8; }
            .loglevel-Information { color: #0078d4; }
            .loglevel-Warning { color: #ffc107; }
            .loglevel-Error { color: #dc3545; }
            .loglevel-Critical { color: #b21f1f; font-weight: bold; }
            .loglevel-None { color: #888; }
            .params { font-size: 0.95em; color: #555; }
            .placeholder { background: #e3f2fd; color: #1565c0; font-weight: bold; border-radius: 3px; padding: 0 2px; }
            .params-list { margin: 0; padding-left: 1em; }
            .params-list li { margin: 0; }
            .param-name { font-weight: bold; color: #1565c0; }
            .param-type { color: #555; }
            .param-type-builtin { color: #00796b; font-weight: bold; }
            .param-type-other { color: #555; }
            .param-kind { color: #888; }
        </style>";
        string html = $@"
        <html>
        <head>
            <meta charset='utf-8'>
            <title>Logger Usage Report</title>
            {css}
        </head>
        <body>
            <h1>Logger Usage Report</h1>
            <table>
                <tr>
                    <th>Level</th>
                    <th>Message</th>
                    <th>Parameters</th>
                    <th>Location</th>
                    <th>Method Name</th>
                    <th>EventId</th>
                </tr>";
        foreach (var usage in results)
        {
            var logLevel = usage.LogLevel?.ToString() ?? "";
            var rawMessage = usage.MessageTemplate ?? "";
            // Highlight placeholders in the message
            var highlightedMessage = System.Text.RegularExpressions.Regex.Replace(
                rawMessage,
                "\\{[^}]+\\}",
                match => $"<span class='placeholder'>{WebUtility.HtmlEncode(match.Value)}</span>"
            );
            var message = highlightedMessage;
            var filePath = usage.Location.FilePath;
            var fileName = WebUtility.HtmlEncode(Path.GetFileName(filePath));
            var line = usage.Location.LineNumber + 1;
            string codeLine = "";
            try
            {
                if (File.Exists(filePath))
                {
                    var allLines = File.ReadAllLines(filePath);
                    if (usage.Location.LineNumber >= 0 && usage.Location.LineNumber < allLines.Length)
                    {
                        codeLine = allLines[usage.Location.LineNumber];
                    }
                }
            }
            catch { /* ignore file errors */ }
            var locationDisplay = $@"<details><summary>{fileName}:{line}</summary><pre><code class='language-csharp'>" + WebUtility.HtmlEncode(codeLine) + "</code></pre></details>";
            var method = usage.MethodName + " (" + usage.MethodType + ")";
            string eventId = usage.EventId switch
            {
                EventIdDetails details => $"{details.Id.Value} / {details.Name.Value}",
                EventIdRef reference => reference.Name,
                _ => ""
            };
            var parameters = "";
            if (usage.MessageParameters != null && usage.MessageParameters.Count > 0)
            {
                parameters = "<ul class='params-list'>" +
                    string.Join("", usage.MessageParameters.Select(p => {
                        var type = p.Type ?? "";
                        // C# built-in types and System.* types
                        string[] builtInTypes = new[] { "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "long", "ulong", "object", "short", "ushort", "string", "void" };
                        bool isBuiltIn = builtInTypes.Contains(type) || type.StartsWith("System.");
                        var typeClass = isBuiltIn ? "param-type-builtin" : "param-type-other";
                        return $"<li><span class='param-name'>{WebUtility.HtmlEncode(p.Name)}</span>: <span class='{typeClass}'>{WebUtility.HtmlEncode(type)}</span> [<span class='param-kind'>{WebUtility.HtmlEncode(p.Kind)}</span>]</li>";
                    })) +
                    "</ul>";
            }
            html += $@"
                <tr>
                    <td class='loglevel-{logLevel}'>{logLevel}</td>
                    <td>{message}</td>
                    <td class='params'>{parameters}</td>
                    <td>{locationDisplay}</td>
                    <td>{method}</td>
                    <td>{WebUtility.HtmlEncode(eventId)}</td>
                </tr>";
        }
        html += @"</table></body></html>";
        return html;
    }
}
