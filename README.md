# dotnet-logging-usage
Create a summary of which log messages a project writes and the paramters to improve consistency


## Usage

### CLI

create HTML/JSON report:

```bash
dotnet run --project src/LoggerUsage.Cli -- <path-to-your-sln-or-csproj> <output-file-name>.<html/json>
```

Example report:

run the command:
```bash
dotnet run --project src/LoggerUsage.Cli -- src/LoggerUsage.Cli/LoggerUsage.Cli.csproj report.html
```

![Logger Usage Report in light mode, showing summary cards, most common parameter names, and parameter name inconsistencies, with a light background and dark text for accessibility.](assets/report-light.png)

and in dark mode:

![Logger Usage Report in dark mode, showing summary cards, most common parameter names, and parameter name inconsistencies, with a dark background and light text for accessibility.](assets/report-dark.png)

## Roadmap

- [ ] Add support for `ILogger.Log` method
- [ ] Add support for `ILogger.BeginScope` method
- [ ] Create a summary of the log messages
- [ ] Integrate AI to suggest improvements and find inconsistencies
- [ ] For LoggerMessageAttribute - find all invocations of method
- [ ] Expose as a MCP
