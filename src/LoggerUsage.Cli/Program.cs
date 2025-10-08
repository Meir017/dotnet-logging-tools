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

        builder.Services.AddLoggerUsageExtractor()
            .AddMSBuild();

        builder.Services.AddSingleton<LoggerUsageWorker>();
        builder.Services.Configure<LoggerUsageOptions>(options =>
        {
            // Parse command line arguments
            var nonFlagArgs = args.Where(a => !a.StartsWith("--")).ToArray();
            options.Path = nonFlagArgs.Length > 0 ? nonFlagArgs[0] : null;
            options.OutputPath = nonFlagArgs.Length > 1 ? nonFlagArgs[1] : null;
            options.Verbose = args.Contains("--verbose") || args.Contains("-v");
        });

        configure?.Invoke(builder);

        return builder.Build().Services.GetRequiredService<LoggerUsageWorker>();
    }
}
