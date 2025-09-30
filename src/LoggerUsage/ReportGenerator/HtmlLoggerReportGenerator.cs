using LoggerUsage.Models;
using System.Net;
using System.Text.RegularExpressions;

namespace LoggerUsage.ReportGenerator;

internal class HtmlLoggerReportGenerator : ILoggerReportGenerator
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
        .invocations { max-width: 300px; }
        .invocations details { margin: 0; }
        .invocations summary { font-size: 0.9em; }
        .invocations ul { font-size: 0.85em; }
        .invocations code { background: #f0f0f0; padding: 1px 3px; border-radius: 2px; }
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
        html.dark-theme .invocations code { background: #263238; color: #90caf9; }
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
    ";

        // Build improved summary section using StringBuilder
        var summaryBuilder = new System.Text.StringBuilder();
        summaryBuilder.AppendLine("<style>\n" +
            "html.dark-theme .summary-section, html.dark-theme .summary-section * {" +
            "  background: #23272e !important; color: #e8eaed !important;" +
            "}\n" +
            "html.dark-theme .summary-card {" +
            "  background: #181a1b !important; color: #e8eaed !important; box-shadow: 0 2px 8px #0008;" +
            "}\n" +
            "html.dark-theme .summary-card span, html.dark-theme .summary-card div { color: #e8eaed !important; }\n" +
            "html.dark-theme .summary-inconsistencies { background: #2d2323 !important; color: #ffd54f !important; }\n" +
            "html.dark-theme .summary-inconsistencies span, html.dark-theme .summary-inconsistencies div { color: #ffd54f !important; }\n" +
            "html.dark-theme .summary-mostcommon { color: #b0bec5 !important; }\n" +
            "html.dark-theme .summary-mostcommon span, html.dark-theme .summary-mostcommon div { color: #b0bec5 !important; }\n" +
            "html.dark-theme .summary-param-chip { background: #263238 !important; color: #90caf9 !important; border: 1px solid #90caf9 !important; }\n" +
            "html.dark-theme .summary-inconsistency-chip { background: #3a2d1a !important; color: #ffd54f !important; border: 1px solid #ffd54f !important; }\n" +
            "</style>");
        summaryBuilder.AppendLine("<div class='summary-section' style='margin-bottom:2em;padding:1.5em 2em;background:#f1f3f6;border-radius:12px;box-shadow:0 2px 8px #0002;width:100%;max-width:none;'>");
        summaryBuilder.AppendLine("  <h2 style='margin-top:0;margin-bottom:1em;font-size:1.5em;'>Summary</h2>");
        summaryBuilder.AppendLine("  <div style='display:grid;grid-template-columns:repeat(4,minmax(180px,1fr));gap:2em 2em;margin-bottom:1.5em;'>");
        summaryBuilder.AppendLine("    <div class='summary-card' style='background:#fff;border-radius:8px;box-shadow:0 1px 4px #0001;padding:1em 1.2em;display:flex;align-items:center;gap:12px;'>");
        summaryBuilder.AppendLine("      <span style='font-size:2em;color:#0078d4;'>📝</span>");
        summaryBuilder.AppendLine($"      <div><div style='font-size:1.2em;font-weight:bold;'>{loggerUsage.Results.Count}</div><div style='font-size:0.95em;color:#555;'>Total Log Usages</div></div>");
        summaryBuilder.AppendLine("    </div>");
        summaryBuilder.AppendLine("    <div class='summary-card' style='background:#fff;border-radius:8px;box-shadow:0 1px 4px #0001;padding:1em 1.2em;display:flex;align-items:center;gap:12px;'>");
        summaryBuilder.AppendLine("      <span style='font-size:2em;color:#43a047;'>🔑</span>");
        summaryBuilder.AppendLine($"      <div><div style='font-size:1.2em;font-weight:bold;'>{loggerUsage.Summary.UniqueParameterNameCount}</div><div style='font-size:0.95em;color:#555;'>Unique Parameter Names</div></div>");
        summaryBuilder.AppendLine("    </div>");
        summaryBuilder.AppendLine("    <div class='summary-card' style='background:#fff;border-radius:8px;box-shadow:0 1px 4px #0001;padding:1em 1.2em;display:flex;align-items:center;gap:12px;'>");
        summaryBuilder.AppendLine("      <span style='font-size:2em;color:#f9a825;'>#️⃣</span>");
        summaryBuilder.AppendLine($"      <div><div style='font-size:1.2em;font-weight:bold;'>{loggerUsage.Summary.TotalParameterUsageCount}</div><div style='font-size:0.95em;color:#555;'>Total Parameter Usages</div></div>");
        summaryBuilder.AppendLine("    </div>");
        summaryBuilder.AppendLine("    <div class='summary-card' style='background:#fff;border-radius:8px;box-shadow:0 1px 4px #0001;padding:1em 1.2em;display:flex;align-items:center;gap:12px;'>");
        summaryBuilder.AppendLine("      <span style='font-size:2em;color:#d32f2f;'>⚠️</span>");
        summaryBuilder.AppendLine($"      <div><div style='font-size:1.2em;font-weight:bold;'>{loggerUsage.Summary.InconsistentParameterNames.Count}</div><div style='font-size:0.95em;color:#555;'>Parameter Name Inconsistencies</div></div>");
        summaryBuilder.AppendLine("    </div>");
        summaryBuilder.AppendLine("  </div>");
        // Most Common Parameter Names
        summaryBuilder.AppendLine("  <div class='summary-mostcommon' style='font-size:1.1em;color:#555;'><div style='margin-bottom:0.5em;'><b>Most Common Parameter Names:</b></div><div style='display:flex;flex-wrap:wrap;gap:0.5em 1em;margin-top:0.5em;'>");
        var topParams = loggerUsage.Summary.CommonParameterNames.Take(8).ToList();
        foreach (var p in topParams)
        {
            summaryBuilder.Append($"<span class='summary-param-chip' style='background:#e3f2fd;color:#1565c0;border-radius:4px;padding:2px 10px 2px 8px;font-size:1em;display:inline-flex;align-items:center;gap:6px;margin-bottom:2px;'>");
            summaryBuilder.Append($"<span style='font-weight:600;'>{WebUtility.HtmlEncode(p.Name)}</span>");
            summaryBuilder.Append($"<span style='background:#fff;border-radius:3px;padding:1px 6px 1px 6px;margin-left:6px;font-size:0.97em;color:#0078d4;border:1px solid #b3d6f7;'>{WebUtility.HtmlEncode(p.MostCommonType)}</span>");
            summaryBuilder.Append($"<span style='color:#888;font-size:0.97em;margin-left:4px;'>({p.Count})</span></span>");
        }
        if (loggerUsage.Summary.CommonParameterNames.Count > 8)
        {
            summaryBuilder.Append($"<span style='color:#888;font-size:0.95em;'>+{loggerUsage.Summary.CommonParameterNames.Count - 8} more</span>");
        }
        summaryBuilder.AppendLine("</div></div>");
        // Parameter Name Inconsistencies (prettified, improved)
        summaryBuilder.AppendLine("  <div class='summary-inconsistencies' style='margin-top:1.5em;background:#fff;border-radius:12px;box-shadow:0 2px 8px #0002;padding:1.5em 2em;max-width:1100px;transition:box-shadow 0.2s;'>");
        summaryBuilder.AppendLine("    <div style='display:flex;align-items:center;gap:12px;margin-bottom:0.9em;'>");
        summaryBuilder.AppendLine("      <span style='font-size:1.7em;color:#f9a825;'>⚠️</span>");
        summaryBuilder.AppendLine("      <span style='font-size:1.18em;font-weight:600;color:#d18b00;'>Parameter Name Inconsistencies</span>");
        summaryBuilder.AppendLine("      <span style='margin-left:auto;font-size:1em;color:#b26a00;background:#fff3cd;border-radius:7px;padding:3px 14px 3px 12px;font-weight:500;box-shadow:0 1px 2px #0001;'>" + loggerUsage.Summary.InconsistentParameterNames.Count + " found</span>");
        summaryBuilder.AppendLine("    </div>");
        summaryBuilder.AppendLine("    <ul style='margin:0 0 0 1.2em;padding:0;list-style:disc inside;display:flex;flex-direction:column;gap:0.7em;'>");
        foreach (var inc in loggerUsage.Summary.InconsistentParameterNames)
        {
            summaryBuilder.Append("<li style='background:#f8fafc;border-radius:7px;padding:0.7em 1em;box-shadow:0 1px 2px #0001;transition:box-shadow 0.2s;display:flex;flex-wrap:wrap;align-items:center;gap:0.5em 1.2em;'>");
            summaryBuilder.Append("<span style='font-weight:600;color:#0078d4;min-width:60px;'>Names:</span> ");
            summaryBuilder.Append("<span style='display:flex;flex-wrap:wrap;gap:0.3em 0.5em;align-items:center;max-width:70vw;'>");
            summaryBuilder.Append(string.Join("", inc.Names.Select(n =>
                $"<span class='summary-param-chip' style='background:#e3f2fd;color:#1565c0;border-radius:4px;padding:2px 9px 2px 9px;margin:1px 2px 1px 0;font-size:0.97em;display:inline-block;white-space:nowrap;'>"
                + WebUtility.HtmlEncode(n.Name) + "<span style='color:#b3d6f7;margin:0 0 0 4px;'>:</span>" +
                $"<span style='background:#fff;border-radius:3px;padding:1px 6px 1px 6px;margin-left:4px;font-size:0.97em;color:#0078d4;border:1px solid #b3d6f7;'>" + WebUtility.HtmlEncode(n.Type) + "</span></span>"
            )));
            summaryBuilder.Append("</span>");
            summaryBuilder.Append("<span style='font-weight:600;color:#d18b00;margin-left:10px;min-width:60px;'>Issues:</span> ");
            summaryBuilder.Append("<span style='display:flex;flex-wrap:wrap;gap:0.3em 0.5em;align-items:center;'>");
            summaryBuilder.Append(string.Join("", inc.IssueTypes.Select(issue =>
                $"<span class='summary-inconsistency-chip' style='background:#fff3cd;color:#b26a00;border-radius:4px;padding:2px 10px 2px 10px;margin:1px 2px 1px 0;font-size:0.97em;display:inline-block;'>"
                + WebUtility.HtmlEncode(issue) + "</span>"
            )));
            summaryBuilder.Append("</span>");
            summaryBuilder.AppendLine("</li>");
        }
        summaryBuilder.AppendLine("    </ul>");
        summaryBuilder.AppendLine("  </div>");
        summaryBuilder.AppendLine("</div>");
        // Insert summary into html
        html += summaryBuilder.ToString();

        html += @"
    <div style='margin-bottom:1.2em;display:flex;gap:2em;align-items:center;flex-wrap:wrap;'>
        <div style='display:flex;align-items:center;gap:0.5em;'>
            <label for='filterLogLevel' style='font-weight:500;color:#1565c0;'>Log Level:</label>
            <select id='filterLogLevel' style='padding:6px 18px 6px 8px;border-radius:6px;border:1px solid #b3d6f7;background:#f8fafc;color:#1565c0;font-size:1em;box-shadow:0 1px 4px #0001;outline:none;'>
                <option value=''>All</option>
                <option value='Trace'>Trace</option>
                <option value='Debug'>Debug</option>
                <option value='Information'>Information</option>
                <option value='Warning'>Warning</option>
                <option value='Error'>Error</option>
                <option value='Critical'>Critical</option>
                <option value='None'>None</option>
            </select>
        </div>
        <div style='display:flex;align-items:center;gap:0.5em;'>
            <label for='filterMethodType' style='font-weight:500;color:#1565c0;'>Method Type:</label>
            <select id='filterMethodType' style='padding:6px 18px 6px 8px;border-radius:6px;border:1px solid #b3d6f7;background:#f8fafc;color:#1565c0;font-size:1em;box-shadow:0 1px 4px #0001;outline:none;'>
                <option value=''>All</option>
            </select>
        </div>
        <div style='display:flex;align-items:center;gap:0.5em;'>
            <label for='filterText' style='font-weight:500;color:#1565c0;'>Search:</label>
            <input id='filterText' type='text' placeholder='Search message, file, etc.' style='padding:6px 12px;border-radius:6px;border:1px solid #b3d6f7;background:#f8fafc;color:#1565c0;font-size:1em;box-shadow:0 1px 4px #0001;width:220px;outline:none;' />
        </div>
    </div>
    <style>
        /* Filtering controls hover/focus */
        #filterLogLevel:focus, #filterMethodType:focus, #filterText:focus {
            border-color: #0078d4;
            box-shadow: 0 0 0 2px #b3d6f7;
        }
        #filterLogLevel:hover, #filterMethodType:hover, #filterText:hover {
            border-color: #0078d4;
        }
        html.dark-theme #filterLogLevel, html.dark-theme #filterMethodType, html.dark-theme #filterText {
            background: #23272e !important;
            color: #90caf9 !important;
            border: 1px solid #90caf9 !important;
        }
        html.dark-theme #filterLogLevel:focus, html.dark-theme #filterMethodType:focus, html.dark-theme #filterText:focus {
            border-color: #42a5f5 !important;
            box-shadow: 0 0 0 2px #42a5f5 !important;
        }
    </style>
    <table id='loggerTable'>
        <tr>
            <th data-sort='loglevel' style='cursor:pointer;'>Level <span class='sort-arrow'></span></th>
            <th data-sort='methodtype' style='cursor:pointer;'>Method Type <span class='sort-arrow'></span></th>
            <th data-sort='message' style='cursor:pointer;'>Message <span class='sort-arrow'></span></th>
            <th data-sort='parameters' style='cursor:pointer;'>Parameters <span class='sort-arrow'></span></th>
            <th data-sort='eventid' style='cursor:pointer;'>EventId <span class='sort-arrow'></span></th>
            <th data-sort='invocations' style='cursor:pointer;'>Invocations <span class='sort-arrow'></span></th>
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

            // Handle LoggerMessage-specific information
            string invocationsHtml = "";
            string declaringTypeInfo = "";
            if (usage is LoggerMessageUsageInfo loggerMessageUsage)
            {
                declaringTypeInfo = $"<div style='font-size:0.9em;color:#666;margin-top:4px;'><strong>Type:</strong> <code>{WebUtility.HtmlEncode(loggerMessageUsage.DeclaringTypeName)}</code></div>";

                if (loggerMessageUsage.HasInvocations)
                {
                    invocationsHtml = $@"<div style='font-size:0.95em;'>
                        <strong>{loggerMessageUsage.InvocationCount} invocation{(loggerMessageUsage.InvocationCount > 1 ? "s" : "")}</strong>
                        <details style='margin-top:4px;'>
                            <summary style='cursor:pointer;color:#0078d4;font-size:0.9em;'>Show Details</summary>
                            <ul style='margin:8px 0 0 0;padding-left:1.2em;font-size:0.9em;'>
                                {string.Join("", loggerMessageUsage.Invocations.Select(inv =>
                                    $@"<li style='margin:4px 0;'>
                                        <div><strong>{WebUtility.HtmlEncode(Path.GetFileName(inv.InvocationLocation.FilePath))}:{inv.InvocationLocation.StartLineNumber + 1}</strong></div>
                                        <div style='color:#666;font-size:0.95em;'>in <code>{WebUtility.HtmlEncode(inv.ContainingType)}</code></div>
                                        {(inv.Arguments.Count > 0 ?
                                            $"<div style='margin-top:2px;'><em>Args:</em> {string.Join(", ", inv.Arguments.Select(arg => $"<code>{WebUtility.HtmlEncode(arg.Name)}</code>"))}</div>"
                                            : "")}
                                    </li>"))}
                            </ul>
                        </details>
                    </div>";
                }
                else
                {
                    invocationsHtml = "<span style='color:#888;font-size:0.9em;'>No invocations found</span>";
                }
            }

            html += $@"
        <tr class='log-row' data-loglevel='{logLevel}' data-methodtype='{WebUtility.HtmlEncode(methodType.ToString())}' data-message='{WebUtility.HtmlEncode(rawMessage)}' data-filepath='{WebUtility.HtmlEncode(filePath)}'>
            <td class='loglevel-{logLevel}'>{logLevel}</td>
            <td>{WebUtility.HtmlEncode(methodType.ToString())}{declaringTypeInfo}</td>
            <td>{message}</td>
            <td class='params'>{parameters}</td>
            <td>{WebUtility.HtmlEncode(eventId)}</td>
            <td class='invocations'>{invocationsHtml}</td>
        </tr>
        <tr class='code-row' data-logrow>
            <td colspan='6' style='padding:0;'>
                <details style='margin:0;'>
                    <summary class='code-summary' style='display:flex;align-items:center;gap:10px;'>
                        <span class='code-icon'>&lt;/&gt;</span>
                        <span class='code-filename' style='font-family:monospace;font-size:0.98em;color:#888;'>{WebUtility.HtmlEncode(filePath)}:{line}</span>
                        <span style='margin-left:8px;font-size:0.93em;color:#0078d4;'>Show/Hide code</span>
                    </summary>
                    <div class='code-block-container' style='border-radius:0 0 8px 8px;border-top:1px solid #e0e0e0;'>
                        <pre style='margin:0;'><code class='language-csharp'>{WebUtility.HtmlEncode(codeBlock)}</code></pre>
                    </div>
                </details>
            </td>
        </tr>";
        }
        html += @"</table>
<style>
    .sort-arrow { font-size:0.9em; color:#888; margin-left:2px; transition:color 0.2s; }
    th.sorted-asc .sort-arrow, th.sorted-desc .sort-arrow { color:#d32f2f !important; }
    th .sort-arrow::after { content: ''; }
    th.sorted-asc .sort-arrow::after { content: '▲'; }
    th.sorted-desc .sort-arrow::after { content: '▼'; }
</style>
<script>
(function() {
    // Populate Method Type dropdown
    var methodTypes = Array.from(new Set(Array.from(document.querySelectorAll('.log-row')).map(r => r.getAttribute('data-methodtype')))).filter(Boolean).sort();
    var mtSelect = document.getElementById('filterMethodType');
    methodTypes.forEach(function(mt) {
        var opt = document.createElement('option');
        opt.value = mt;
        opt.textContent = mt;
        mtSelect.appendChild(opt);
    });
    function filterTable() {
        var logLevel = document.getElementById('filterLogLevel').value;
        var methodType = document.getElementById('filterMethodType').value;
        var text = document.getElementById('filterText').value.toLowerCase();
        var rows = document.querySelectorAll('.log-row');
        rows.forEach(function(row) {
            var show = true;
            if (logLevel && row.getAttribute('data-loglevel') !== logLevel) show = false;
            if (methodType && row.getAttribute('data-methodtype') !== methodType) show = false;
            if (text) {
                var msg = row.getAttribute('data-message') || '';
                var fp = row.getAttribute('data-filepath') || '';
                show = show && (msg.toLowerCase().includes(text) || fp.toLowerCase().includes(text));
            }
            row.style.display = show ? '' : 'none';
            // Hide/show code row as well
            var codeRow = row.nextElementSibling;
            if (codeRow && codeRow.hasAttribute('data-logrow')) {
                codeRow.style.display = show ? '' : 'none';
            }
        });
    }
    document.getElementById('filterLogLevel').addEventListener('change', filterTable);
    document.getElementById('filterMethodType').addEventListener('change', filterTable);
    document.getElementById('filterText').addEventListener('input', filterTable);
    // Sorting logic
    var table = document.getElementById('loggerTable');
    var headers = table.querySelectorAll('th[data-sort]');
    var sortState = { col: null, dir: 1 };
    headers.forEach(function(th, idx) {
        th.addEventListener('click', function() {
            var sortKey = th.getAttribute('data-sort');
            var rows = Array.from(table.querySelectorAll('tr.log-row'));
            // Remove sort classes and reset all arrows
            headers.forEach(h => h.classList.remove('sorted-asc', 'sorted-desc'));
            // Determine sort direction
            if (sortState.col === sortKey) sortState.dir *= -1;
            else sortState.dir = 1;
            sortState.col = sortKey;
            th.classList.add(sortState.dir === 1 ? 'sorted-asc' : 'sorted-desc');
            // Sort rows
            rows.sort(function(a, b) {
                var getVal = function(row) {
                    switch(sortKey) {
                        case 'loglevel': return row.getAttribute('data-loglevel') || '';
                        case 'methodtype': return row.getAttribute('data-methodtype') || '';
                        case 'message': return row.getAttribute('data-message') || '';
                        case 'parameters': return row.querySelector('.params')?.innerText || '';
                        case 'eventid': return row.children[4]?.innerText || '';
                        default: return '';
                    }
                };
                var va = getVal(a).toLowerCase();
                var vb = getVal(b).toLowerCase();
                if (va < vb) return -1 * sortState.dir;
                if (va > vb) return 1 * sortState.dir;
                return 0;
            });
            // Re-attach sorted rows and their code rows
            var tbody = table.tBodies[0] || table;
            rows.forEach(function(row) {
                var codeRow = row.nextElementSibling;
                tbody.appendChild(row);
                if (codeRow && codeRow.classList.contains('code-row')) tbody.appendChild(codeRow);
            });
        });
    });
})();
</script>
</body>
</html>";
        return html;
    }
}
