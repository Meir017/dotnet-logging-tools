using LoggerUsage.Models;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(LoggerUsage.Tests.MessageParameterListXunitSerializer), typeof(List<MessageParameter>))]
