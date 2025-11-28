using System.Collections.Generic;
using AwesomeAssertions;
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
        extractionResult.Summary.ParameterTypesByName.Should().ContainSingle();
        extractionResult.Summary.ParameterTypesByName.Should().ContainKey("userId");
        extractionResult.Summary.ParameterTypesByName["userId"].Should().Contain("string");
        extractionResult.Summary.TotalParameterUsageCount.Should().Be(1);
        extractionResult.Summary.UniqueParameterNameCount.Should().Be(1);
        extractionResult.Summary.InconsistentParameterNames.Should().BeEmpty();
        extractionResult.Summary.CommonParameterNames.Should().ContainSingle();
        extractionResult.Summary.CommonParameterNames[0].Should().Be(new LoggerUsageExtractionSummary.CommonParameterNameInfo("userId", 1, "string"));
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
        extractionResult.Summary.ParameterTypesByName.Should().HaveCount(2);
        extractionResult.Summary.ParameterTypesByName.Should().ContainKey("userId");
        extractionResult.Summary.ParameterTypesByName["userId"].Should().Contain("string");
        extractionResult.Summary.ParameterTypesByName["userId"].Should().Contain("int");
        extractionResult.Summary.ParameterTypesByName.Should().ContainKey("orderId");
        extractionResult.Summary.ParameterTypesByName["orderId"].Should().Contain("int");
        extractionResult.Summary.TotalParameterUsageCount.Should().Be(3);
        extractionResult.Summary.UniqueParameterNameCount.Should().Be(2);
        extractionResult.Summary.InconsistentParameterNames.Should().Contain(x => x.Names.Any(pair => pair.Name == "userId") && x.IssueTypes.Contains("TypeMismatch"));
        extractionResult.Summary.CommonParameterNames.Should().HaveCount(2);
        var userIdCommon = extractionResult.Summary.CommonParameterNames.Find(x => x.Name == "userId");
        userIdCommon.Count.Should().Be(2);
        userIdCommon.MostCommonType.Should().BeOneOf("string", "int");
        extractionResult.Summary.ParameterTypesByName["userId"].Should().Contain("string");
        extractionResult.Summary.ParameterTypesByName["userId"].Should().Contain("int");
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
        extractionResult.Summary.ParameterTypesByName.Should().BeEmpty();
        extractionResult.Summary.TotalParameterUsageCount.Should().Be(0);
        extractionResult.Summary.UniqueParameterNameCount.Should().Be(0);
        extractionResult.Summary.InconsistentParameterNames.Should().BeEmpty();
        extractionResult.Summary.CommonParameterNames.Should().BeEmpty();
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
        extractionResult.Summary.InconsistentParameterNames.Should().Contain(x =>
            x.IssueTypes.Contains("TypeMismatch")
            && x.Names.All(pair => pair.Name == "userId")
            && x.Names.Any(pair => pair.Type == "string")
            && x.Names.Any(pair => pair.Type == "int")
        );
        // There should be a casing difference group for userId/UserId/userid
        extractionResult.Summary.InconsistentParameterNames.Should().Contain(x =>
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
        extractionResult.Summary.TelemetryStats.HasTelemetryFeatures.Should().BeTrue();
        extractionResult.Summary.TelemetryStats.ParametersWithCustomTagNames.Should().Be(2);
        extractionResult.Summary.TelemetryStats.PropertiesWithCustomTagNames.Should().Be(2);
        extractionResult.Summary.TelemetryStats.CustomTagNameMappings.Should().HaveCount(4);
        
        // Verify mappings
        extractionResult.Summary.TelemetryStats.CustomTagNameMappings.Should().Contain(
            m => m.OriginalName == "userId" && m.CustomTagName == "user.id" && m.Context == "Parameter");
        extractionResult.Summary.TelemetryStats.CustomTagNameMappings.Should().Contain(
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
        extractionResult.Summary.TelemetryStats.HasTelemetryFeatures.Should().BeTrue();
        extractionResult.Summary.TelemetryStats.ParametersWithTagProviders.Should().Be(1);
        extractionResult.Summary.TelemetryStats.TagProviders.Should().ContainSingle();
        
        var provider = extractionResult.Summary.TelemetryStats.TagProviders[0];
        provider.ParameterName.Should().Be("request");
        provider.ProviderTypeName.Should().Be("MyApp.TagProviders.HttpRequestTagProvider");
        provider.ProviderMethodName.Should().Be("ProvideTags");
        provider.IsValid.Should().BeTrue();
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
        extractionResult.Summary.TelemetryStats.HasTelemetryFeatures.Should().BeTrue();
        extractionResult.Summary.TelemetryStats.TotalTransitiveProperties.Should().Be(3);
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
        extractionResult.Summary.TelemetryStats.HasTelemetryFeatures.Should().BeFalse();
        extractionResult.Summary.TelemetryStats.ParametersWithCustomTagNames.Should().Be(0);
        extractionResult.Summary.TelemetryStats.PropertiesWithCustomTagNames.Should().Be(0);
        extractionResult.Summary.TelemetryStats.ParametersWithTagProviders.Should().Be(0);
        extractionResult.Summary.TelemetryStats.TotalTransitiveProperties.Should().Be(0);
        extractionResult.Summary.TelemetryStats.CustomTagNameMappings.Should().BeEmpty();
        extractionResult.Summary.TelemetryStats.TagProviders.Should().BeEmpty();
    }
}
