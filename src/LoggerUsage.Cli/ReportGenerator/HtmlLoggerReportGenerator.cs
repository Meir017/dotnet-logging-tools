using LoggerUsage.Models;
using System.Net;
using System.Text.RegularExpressions;

namespace LoggerUsage.Cli.ReportGenerator;

public class HtmlLoggerReportGenerator : ILoggerReportGenerator
{
    public string GenerateReport(LoggerUsageExtractionResult loggerUsage)
    {
        string html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Logger Usage Report</title>
    <style>
        body { font-family: 'Segoe UI', Arial, sans-serif; margin: 2em; background: #f8f9fa; }
        h1 { color: #333; }
        table { border-collapse: collapse; width: 100%; background: #fff; box-shadow: 0 2px 8px #0001; }
        th, td { border: 1px solid #dee2e6; padding: 8px 12px; text-align: left; }
        th { background: #0078d4; color: #fff; position: sticky; top: 0; z-index: 2; }
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
        .code-row { background: #f8fafc; border-left: 4px solid #0078d4; transition: background 0.2s; }
        .code-summary { font-size: 0.95em; color: #1565c0; cursor: pointer; padding: 4px 0 4px 12px; background: #e3eafc; border-bottom: 1px solid #dee2e6; display: flex; align-items: center; gap: 8px; }
        .code-icon { font-family: monospace; font-size: 1.1em; color: #0078d4; }
        .code-block-container { padding-left: 32px; overflow-x: auto; max-width: 100vw; background: #fff; }
        pre { margin: 0; font-size: 1em; line-height: 1.4; padding: 8px 0 8px 0; background: #fff; }
        /* PrismJS light theme override */
        code[class*=""language-""] { background: #fff !important; }
        /* Dark theme styles */
        html.dark-theme body { background: #181a1b; color: #e8eaed; }
        html.dark-theme table { background: #23272e; color: #e8eaed; }
        html.dark-theme th { background: #222b3a; color: #fff; }
        html.dark-theme tr:nth-child(even) { background: #23272e; }
        html.dark-theme tr, html.dark-theme td { background: #181a1b; color: #e8eaed; }
        html.dark-theme .loglevel-Trace { color: #b0b0b0; }
        html.dark-theme .loglevel-Debug { color: #4dd0e1; }
        html.dark-theme .loglevel-Information { color: #42a5f5; }
        html.dark-theme .loglevel-Warning { color: #ffd54f; }
        html.dark-theme .loglevel-Error { color: #ef5350; }
        html.dark-theme .loglevel-Critical { color: #ff1744; font-weight: bold; }
        html.dark-theme .loglevel-None { color: #888; }
        html.dark-theme .params { color: #b0bec5; }
        html.dark-theme .placeholder { background: #263238; color: #90caf9; }
        html.dark-theme .param-name { color: #90caf9; }
        html.dark-theme .param-type-builtin { color: #80cbc4; }
        html.dark-theme .param-type-other { color: #b0bec5; }
        html.dark-theme .param-kind { color: #b0bec5; }
        html.dark-theme .code-row { background: #23272e; border-left: 4px solid #42a5f5; }
        html.dark-theme .code-summary { background: #263238; color: #90caf9; }
        html.dark-theme .code-icon { color: #42a5f5; }
        html.dark-theme .code-block-container { background: #181a1b !important; }
        html.dark-theme pre { background: #181a1b !important; color: #e8eaed; }
        html.dark-theme code[class*=""language-""] { background: #181a1b !important; color: #e8eaed; }
        /* PrismJS dark theme override for syntax highlighting */
        html.dark-theme .token.comment, html.dark-theme .token.prolog, html.dark-theme .token.doctype, html.dark-theme .token.cdata { color: #6a9955; }
        html.dark-theme .token.punctuation { color: #d4d4d4; }
        html.dark-theme .token.property, html.dark-theme .token.tag, html.dark-theme .token.constant, html.dark-theme .token.symbol, html.dark-theme .token.deleted { color: #569cd6; }
        html.dark-theme .token.boolean, html.dark-theme .token.number { color: #b5cea8; }
        html.dark-theme .token.selector, html.dark-theme .token.attr-name, html.dark-theme .token.string, html.dark-theme .token.char, html.dark-theme .token.builtin, html.dark-theme .token.inserted { color: #ce9178; }
        html.dark-theme .token.operator, html.dark-theme .token.entity, html.dark-theme .token.url, html.dark-theme .language-css .token.string, html.dark-theme .style .token.string { color: #d4d4d4; }
        html.dark-theme .token.atrule, html.dark-theme .token.attr-value, html.dark-theme .token.keyword { color: #c586c0; }
        html.dark-theme .token.function, html.dark-theme .token.class-name { color: #4ec9b0; }
        html.dark-theme .token.regex, html.dark-theme .token.important, html.dark-theme .token.variable { color: #d16969; }
        html.dark-theme .token.important, html.dark-theme .token.bold { font-weight: bold; }
        html.dark-theme .token.italic { font-style: italic; }
        html.dark-theme .token.entity { cursor: help; }
    </style>
    <script>
    function toggleTheme() {
        const html = document.documentElement;
        const isDark = html.classList.toggle('dark-theme');
        localStorage.setItem('loggerUsageTheme', isDark ? 'dark' : 'light');
    }
    window.onload = function() {
        const theme = localStorage.getItem('loggerUsageTheme');
        if (theme === 'dark') document.documentElement.classList.add('dark-theme');
    }
    </script>
    <link href='https://cdn.jsdelivr.net/npm/prismjs@1.29.0/themes/prism.css' rel='stylesheet' />
    <script src='https://cdn.jsdelivr.net/npm/prismjs@1.29.0/prism.js'></script>
    <script src='https://cdn.jsdelivr.net/npm/prismjs@1.29.0/components/prism-csharp.min.js'></script>
</head>
<body>
    <button onclick='toggleTheme()' style='position:fixed;top:18px;right:32px;z-index:10;padding:6px 16px;border-radius:6px;border:none;background:#0078d4;color:#fff;font-weight:bold;cursor:pointer;'>Toggle Theme</button>
    <h1>Logger Usage Report</h1>
    <table>
        <tr>
            <th>Level</th>
            <th>Method Type</th>
            <th>Message</th>
            <th>Parameters</th>
            <th>EventId</th>
        </tr>";

        foreach (var usage in loggerUsage.Results)
        {
            var logLevel = usage.LogLevel?.ToString() ?? "";
            var rawMessage = usage.MessageTemplate ?? "";
            // Highlight placeholders in the message
            var highlightedMessage = Regex.Replace(
                rawMessage,
                "\\{[^}]+\\}",
                match => $"<span class='placeholder'>{WebUtility.HtmlEncode(match.Value)}</span>"
            );
            var message = highlightedMessage;
            var filePath = usage.Location.FilePath;
            var fileName = WebUtility.HtmlEncode(Path.GetFileName(filePath));
            var line = usage.Location.StartLineNumber + 1;
            string codeBlock = "";
            try
            {
                if (File.Exists(filePath))
                {
                    var allLines = File.ReadAllLines(filePath);
                    int start = Math.Max(0, usage.Location.StartLineNumber);
                    int end = Math.Min(allLines.Length - 1, usage.Location.EndLineNumber);
                    if (start <= end)
                    {
                        var codeLines = allLines.Skip(start).Take(end - start + 1);
                        codeBlock = string.Join("\n", codeLines);
                    }
                }
            }
            catch { /* ignore file errors */ }
            var methodType = usage.MethodType;
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
                        string[] builtInTypes = ["bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "long", "ulong", "object", "short", "ushort", "string", "void"];
                        bool isBuiltIn = builtInTypes.Contains(type) || type.StartsWith("System.");
                        var typeClass = isBuiltIn ? "param-type-builtin" : "param-type-other";
                        return $"<li><span class='param-name'>{WebUtility.HtmlEncode(p.Name)}</span>: <span class='{typeClass}'>{WebUtility.HtmlEncode(type)}</span> [<span class='param-kind'>{WebUtility.HtmlEncode(p.Kind)}</span>]</li>";
                    })) +
                    "</ul>";
            }
            html += $@"
        <tr>
            <td class='loglevel-{logLevel}'>{logLevel}</td>
            <td>{WebUtility.HtmlEncode(methodType.ToString())}</td>
            <td>{message}</td>
            <td class='params'>{parameters}</td>
            <td>{WebUtility.HtmlEncode(eventId)}</td>
        </tr>
        <tr class='code-row'>
            <td colspan='5' style='padding:0;'>
                <details style='margin:0;'>
                    <summary class='code-summary'>
                        <span class='code-icon'>&lt;/&gt;</span>
                        <span class='code-filename'>{fileName}:{line}</span>
                    </summary>
                    <div class='code-block-container'>
                        <pre style='margin:0;'><code class='language-csharp'>{WebUtility.HtmlEncode(codeBlock)}</code></pre>
                    </div>
                </details>
            </td>
        </tr>";
        }
        html += "</table>\n</body>\n</html>";
        return html;
    }
}
