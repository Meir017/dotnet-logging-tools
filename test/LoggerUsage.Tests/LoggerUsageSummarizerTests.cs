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

    [Fact]
    public void PopulateSummary_WithCustomTagNames_PopulatesTelemetryStats()
    {
        // Arrange
        var extractionResult = new LoggerUsageExtractionResult
        {
            Results =
            [
                new LoggerUsageInfo
                {
                    MethodName = "LogUser",
                    MethodType = LoggerUsageMethodType.LoggerExtensions,
                    Location = new MethodCallLocation
                    {
                        FilePath = "TestClass.cs",
                        StartLineNumber = 1,
                        EndLineNumber = 1
                    },
                    MessageParameters =
                    [
                        new MessageParameter("userId", "string", "Parameter", CustomTagName: "user.id"),
                        new MessageParameter("userName", "string", "Parameter", CustomTagName: "user.name")
                    ]
                },
                new LoggerMessageUsageInfo
                {
                    MethodName = "LogDetails",
                    MethodType = LoggerUsageMethodType.LoggerMessageAttribute,
                    DeclaringTypeName = "Test.Logger",
                    Location = new MethodCallLocation
                    {
                        FilePath = "Logger.cs",
                        StartLineNumber = 10,
                        EndLineNumber = 10
                    },
                    LogPropertiesParameters =
                    [
                        new LogPropertiesParameterInfo(
                            "user",
                            "User",
                            new LogPropertiesConfiguration(),
                            [
                                new LogPropertyInfo("Id", "Id", "int", false, CustomTagName: "user.identifier"),
                                new LogPropertyInfo("Email", "Email", "string", false, CustomTagName: "user.email")
                            ]
                        )
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };
        var summarizer = new LoggerUsageSummarizer();

        // Act
        summarizer.PopulateSummary(extractionResult);

        // Assert
        Assert.True(extractionResult.Summary.TelemetryStats.HasTelemetryFeatures);
        Assert.Equal(2, extractionResult.Summary.TelemetryStats.ParametersWithCustomTagNames);
        Assert.Equal(2, extractionResult.Summary.TelemetryStats.PropertiesWithCustomTagNames);
        Assert.Equal(4, extractionResult.Summary.TelemetryStats.CustomTagNameMappings.Count);
        
        // Verify mappings
        Assert.Contains(extractionResult.Summary.TelemetryStats.CustomTagNameMappings, 
            m => m.OriginalName == "userId" && m.CustomTagName == "user.id" && m.Context == "Parameter");
        Assert.Contains(extractionResult.Summary.TelemetryStats.CustomTagNameMappings, 
            m => m.OriginalName == "Id" && m.CustomTagName == "user.identifier" && m.Context == "Property");
    }

    [Fact]
    public void PopulateSummary_WithTagProviders_PopulatesTelemetryStats()
    {
        // Arrange
        var extractionResult = new LoggerUsageExtractionResult
        {
            Results =
            [
                new LoggerMessageUsageInfo
                {
                    MethodName = "LogWithProvider",
                    MethodType = LoggerUsageMethodType.LoggerMessageAttribute,
                    DeclaringTypeName = "Test.Logger",
                    Location = new MethodCallLocation
                    {
                        FilePath = "Logger.cs",
                        StartLineNumber = 20,
                        EndLineNumber = 20
                    },
                    LogPropertiesParameters =
                    [
                        new LogPropertiesParameterInfo(
                            "request",
                            "HttpRequest",
                            new LogPropertiesConfiguration(),
                            [
                                new LogPropertyInfo("Method", "Method", "string", false),
                                new LogPropertyInfo("Path", "Path", "string", false)
                            ],
                            TagProvider: new TagProviderInfo(
                                "request",
                                "MyApp.TagProviders.HttpRequestTagProvider",
                                "ProvideTags",
                                OmitReferenceName: false,
                                IsValid: true
                            )
                        )
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };
        var summarizer = new LoggerUsageSummarizer();

        // Act
        summarizer.PopulateSummary(extractionResult);

        // Assert
        Assert.True(extractionResult.Summary.TelemetryStats.HasTelemetryFeatures);
        Assert.Equal(1, extractionResult.Summary.TelemetryStats.ParametersWithTagProviders);
        Assert.Single(extractionResult.Summary.TelemetryStats.TagProviders);
        
        var provider = extractionResult.Summary.TelemetryStats.TagProviders[0];
        Assert.Equal("request", provider.ParameterName);
        Assert.Equal("MyApp.TagProviders.HttpRequestTagProvider", provider.ProviderTypeName);
        Assert.Equal("ProvideTags", provider.ProviderMethodName);
        Assert.True(provider.IsValid);
    }

    [Fact]
    public void PopulateSummary_WithTransitiveProperties_CountsThem()
    {
        // Arrange
        var extractionResult = new LoggerUsageExtractionResult
        {
            Results =
            [
                new LoggerMessageUsageInfo
                {
                    MethodName = "LogUserWithAddress",
                    MethodType = LoggerUsageMethodType.LoggerMessageAttribute,
                    DeclaringTypeName = "Test.Logger",
                    Location = new MethodCallLocation
                    {
                        FilePath = "Logger.cs",
                        StartLineNumber = 30,
                        EndLineNumber = 30
                    },
                    LogPropertiesParameters =
                    [
                        new LogPropertiesParameterInfo(
                            "user",
                            "User",
                            new LogPropertiesConfiguration(Transitive: true),
                            [
                                new LogPropertyInfo("Name", "Name", "string", false),
                                new LogPropertyInfo("Age", "Age", "int", false),
                                new LogPropertyInfo("Address", "Address", "Address", false, NestedProperties:
                                [
                                    new LogPropertyInfo("Street", "Street", "string", false),
                                    new LogPropertyInfo("City", "City", "string", false),
                                    new LogPropertyInfo("ZipCode", "ZipCode", "string", false)
                                ])
                            ]
                        )
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };
        var summarizer = new LoggerUsageSummarizer();

        // Act
        summarizer.PopulateSummary(extractionResult);

        // Assert
        Assert.True(extractionResult.Summary.TelemetryStats.HasTelemetryFeatures);
        Assert.Equal(3, extractionResult.Summary.TelemetryStats.TotalTransitiveProperties);
    }

    [Fact]
    public void PopulateSummary_WithNoTelemetryFeatures_HasTelemetryFeaturesIsFalse()
    {
        // Arrange
        var extractionResult = new LoggerUsageExtractionResult
        {
            Results =
            [
                new LoggerUsageInfo
                {
                    MethodName = "LogSimple",
                    MethodType = LoggerUsageMethodType.LoggerExtensions,
                    Location = new MethodCallLocation
                    {
                        FilePath = "TestClass.cs",
                        StartLineNumber = 1,
                        EndLineNumber = 1
                    },
                    MessageParameters =
                    [
                        new MessageParameter("message", "string", "Parameter")
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };
        var summarizer = new LoggerUsageSummarizer();

        // Act
        summarizer.PopulateSummary(extractionResult);

        // Assert
        Assert.False(extractionResult.Summary.TelemetryStats.HasTelemetryFeatures);
        Assert.Equal(0, extractionResult.Summary.TelemetryStats.ParametersWithCustomTagNames);
        Assert.Equal(0, extractionResult.Summary.TelemetryStats.PropertiesWithCustomTagNames);
        Assert.Equal(0, extractionResult.Summary.TelemetryStats.ParametersWithTagProviders);
        Assert.Equal(0, extractionResult.Summary.TelemetryStats.TotalTransitiveProperties);
        Assert.Empty(extractionResult.Summary.TelemetryStats.CustomTagNameMappings);
        Assert.Empty(extractionResult.Summary.TelemetryStats.TagProviders);
    }
}
