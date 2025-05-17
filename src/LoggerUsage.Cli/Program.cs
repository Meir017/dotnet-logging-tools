using LoggerUsage.Cli.ReportGenerator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var worker = CreateWorker(args);
        return await worker.RunAsync();
    }

    public static LoggerUsageWorker CreateWorker(string[] args, Action<HostApplicationBuilder>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders().AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "[HH:mm:ss] ";
        });
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services.AddSingleton<LoggerUsageExtractor>();
        builder.Services.Configure<LoggerUsageOptions>(options =>
        {
            options.Path = args.Length > 0 ? args[0] : null;
            options.OutputPath = args.Length > 1 ? args[1] : null;
        });
        builder.Services.AddSingleton<LoggerUsageWorker>();
        builder.Services.AddSingleton<ILoggerReportGeneratorFactory, LoggerReportGeneratorFactory>();

        configure?.Invoke(builder);

        return builder.Build().Services.GetRequiredService<LoggerUsageWorker>();
    }
}