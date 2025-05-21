

using LoggerUsage;
using LoggerUsage.MSBuild;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class LoggerUsageMSBuildBuilderExtensions
{
    public static ILoggerUsageBuilder AddMSBuild(this ILoggerUsageBuilder builder)
    {
        builder.Services.AddSingleton<IWorkspaceFactory, MSBuildWorkspaceFactory>();

        return builder;
    }
}