using System.Collections.Generic;
using LoggerUsage;
using LoggerUsage.Models;
using Xunit;

namespace LoggerUsage.Tests;

public class LoggerUsageSummarizerTests
{
    [Fact]
    public void PopulateSummary_SingleParameter_SummaryIsCorrect()
    {
        // Arrange
        var extractionResult = new LoggerUsageExtractionResult
        {
            Results =
            [
                new LoggerUsageInfo
                {
                    MethodName = "TestMethod",
                    MethodType = LoggerUsageMethodType.LoggerExtensions,
                    Location = new MethodCallLocation
                    {
                        FilePath = "TestClass.cs",
                        StartLineNumber = 1,
                        EndLineNumber = 1
                    },
                    MessageParameters =
                    [
                        new MessageParameter("userId", "string", "ParameterReference")
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };
        var summarizer = new LoggerUsageSummarizer();

        // Act
        summarizer.PopulateSummary(extractionResult);

        // Assert
        Assert.Single(extractionResult.Summary.ParameterTypesByName);
        Assert.Contains("userId", extractionResult.Summary.ParameterTypesByName.Keys);
        Assert.Contains("string", extractionResult.Summary.ParameterTypesByName["userId"]);
        Assert.Equal(1, extractionResult.Summary.TotalParameterUsageCount);
        Assert.Equal(1, extractionResult.Summary.UniqueParameterNameCount);
        Assert.Empty(extractionResult.Summary.InconsistentParameterNames);
        Assert.Single(extractionResult.Summary.CommonParameterNames);
        Assert.Equal(new LoggerUsageExtractionSummary.CommonParameterNameInfo("userId", 1, "string"), extractionResult.Summary.CommonParameterNames[0]);
    }

    [Fact]
    public void PopulateSummary_MultipleParametersAndTypes_DetectsInconsistencies()
    {
        // Arrange
        var extractionResult = new LoggerUsageExtractionResult
        {
            Results =
            [
                new LoggerUsageInfo
                {
                    MethodName = "TestMethod",
                    MethodType = LoggerUsageMethodType.LoggerExtensions,
                    Location = new MethodCallLocation
                    {
                        FilePath = "TestClass.cs",
                        StartLineNumber = 1,
                        EndLineNumber = 1
                    },
                    MessageParameters =
                    [
                        new MessageParameter("userId", "string", "ParameterReference"),
                        new MessageParameter("orderId", "int", "ParameterReference")
                    ]
                },
                new LoggerUsageInfo
                {
                    MethodName = "TestMethod",
                    MethodType = LoggerUsageMethodType.LoggerExtensions,
                    Location = new MethodCallLocation
                    {
                        FilePath = "TestClass.cs",
                        StartLineNumber = 2,
                        EndLineNumber = 2
                    },
                    MessageParameters =
                    [
                        new MessageParameter("userId", "int", "ParameterReference")
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };
        var summarizer = new LoggerUsageSummarizer();

        // Act
        summarizer.PopulateSummary(extractionResult);

        // Assert
        Assert.Equal(2, extractionResult.Summary.ParameterTypesByName.Count);
        Assert.Contains("userId", extractionResult.Summary.ParameterTypesByName.Keys);
        Assert.Contains("string", extractionResult.Summary.ParameterTypesByName["userId"]);
        Assert.Contains("int", extractionResult.Summary.ParameterTypesByName["userId"]);
        Assert.Contains("orderId", extractionResult.Summary.ParameterTypesByName.Keys);
        Assert.Contains("int", extractionResult.Summary.ParameterTypesByName["orderId"]);
        Assert.Equal(3, extractionResult.Summary.TotalParameterUsageCount);
        Assert.Equal(2, extractionResult.Summary.UniqueParameterNameCount);
        Assert.Contains(extractionResult.Summary.InconsistentParameterNames, x => x.Names.Any(pair => pair.Name == "userId") && x.IssueTypes.Contains("TypeMismatch"));
        Assert.Equal(2, extractionResult.Summary.CommonParameterNames.Count);
        var userIdCommon = extractionResult.Summary.CommonParameterNames.Find(x => x.Name == "userId");
        Assert.Equal(2, userIdCommon.Count);
        Assert.Contains(userIdCommon.MostCommonType, new[] { "string", "int" });
        Assert.Contains("string", extractionResult.Summary.ParameterTypesByName["userId"]);
        Assert.Contains("int", extractionResult.Summary.ParameterTypesByName["userId"]);
    }

    [Fact]
    public void PopulateSummary_EmptyParameters_SummaryIsEmpty()
    {
        // Arrange
        var extractionResult = new LoggerUsageExtractionResult
        {
            Results =
            [
                new LoggerUsageInfo
                {
                    MethodName = "TestMethod",
                    MethodType = LoggerUsageMethodType.LoggerExtensions,
                    Location = new MethodCallLocation
                    {
                        FilePath = "TestClass.cs",
                        StartLineNumber = 1,
                        EndLineNumber = 1
                    },
                    MessageParameters = []
                },
                new LoggerUsageInfo
                {
                    MethodName = "TestMethod",
                    MethodType = LoggerUsageMethodType.LoggerExtensions,
                    Location = new MethodCallLocation
                    {
                        FilePath = "TestClass.cs",
                        StartLineNumber = 2,
                        EndLineNumber = 2
                    },
                    MessageParameters = []
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };
        var summarizer = new LoggerUsageSummarizer();

        // Act
        summarizer.PopulateSummary(extractionResult);

        // Assert
        Assert.Empty(extractionResult.Summary.ParameterTypesByName);
        Assert.Equal(0, extractionResult.Summary.TotalParameterUsageCount);
        Assert.Equal(0, extractionResult.Summary.UniqueParameterNameCount);
        Assert.Empty(extractionResult.Summary.InconsistentParameterNames);
        Assert.Empty(extractionResult.Summary.CommonParameterNames);
    }

    [Fact]
    public void PopulateSummary_TypeMismatchAndCasingDifference_AreDetected()
    {
        // Arrange
        var extractionResult = new LoggerUsageExtractionResult
        {
            Results =
            [
                new LoggerUsageInfo
                {
                    MethodName = "TestMethod",
                    MethodType = LoggerUsageMethodType.LoggerExtensions,
                    Location = new MethodCallLocation
                    {
                        FilePath = "TestClass.cs",
                        StartLineNumber = 1,
                        EndLineNumber = 1
                    },
                    MessageParameters =
                    [
                        new MessageParameter("userId", "string", "ParameterReference"),
                        new MessageParameter("UserId", "int", "ParameterReference"),
                        new MessageParameter("userid", "string", "ParameterReference"),
                        new MessageParameter("orderId", "int", "ParameterReference")
                    ]
                },
                new LoggerUsageInfo
                {
                    MethodName = "TestMethod",
                    MethodType = LoggerUsageMethodType.LoggerExtensions,
                    Location = new MethodCallLocation
                    {
                        FilePath = "TestClass.cs",
                        StartLineNumber = 2,
                        EndLineNumber = 2
                    },
                    MessageParameters =
                    [
                        new MessageParameter("userId", "int", "ParameterReference")
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };
        var summarizer = new LoggerUsageSummarizer();

        // Act
        summarizer.PopulateSummary(extractionResult);

        // Assert
        // There should be a type mismatch for 'userId' (string and int)
        Assert.Contains(extractionResult.Summary.InconsistentParameterNames, x =>
            x.IssueTypes.Contains("TypeMismatch")
            && x.Names.All(pair => pair.Name == "userId")
            && x.Names.Any(pair => pair.Type == "string")
            && x.Names.Any(pair => pair.Type == "int")
        );
        // There should be a casing difference group for userId/UserId/userid
        Assert.Contains(extractionResult.Summary.InconsistentParameterNames, x =>
            x.IssueTypes.Contains("CasingDifference")
            && x.Names.Select(pair => pair.Name).Distinct().Count() == 3
            && x.Names.Any(pair => pair.Name == "userId")
            && x.Names.Any(pair => pair.Name == "UserId")
            && x.Names.Any(pair => pair.Name == "userid")
        );
    }
}
