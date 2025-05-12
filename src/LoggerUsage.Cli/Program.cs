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

        builder.Services.AddSingleton<LoggerUsageExtractor>();
        builder.Services.Configure<LoggerUsageOptions>(options =>
        {
            var path = args.Length > 0 ? args[0] : null;
            options.Path = path;
        });
        builder.Services.AddSingleton<LoggerUsageWorker>();

        configure?.Invoke(builder);

        return builder.Build().Services.GetRequiredService<LoggerUsageWorker>();
    }
}