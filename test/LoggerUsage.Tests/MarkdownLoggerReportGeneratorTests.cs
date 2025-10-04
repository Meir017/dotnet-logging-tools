using FluentAssertions;
using LoggerUsage.Models;
using LoggerUsage.ReportGenerator;
using Microsoft.Extensions.DependencyInjection;

namespace LoggerUsage.Tests;

public class MarkdownLoggerReportGeneratorTests
{
    private readonly ILoggerReportGeneratorFactory _factory;

    public MarkdownLoggerReportGeneratorTests()
    {
        var services = new ServiceCollection();
        services.AddLoggerUsageExtractor();
        _factory = services.BuildServiceProvider().GetRequiredService<ILoggerReportGeneratorFactory>();
    }

    [Fact]
    public void GenerateReport_WithEmptyResults_ReturnsValidMarkdown()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".md");
        var result = new LoggerUsageExtractionResult
        {
            Results = [],
            Summary = new LoggerUsageExtractionSummary()
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().Contain("# Logger Usage Report");
        markdown.Should().Contain("## 📊 Summary");
        markdown.Should().Contain("*No logger usages found.*");
    }

    [Fact]
    public void GenerateReport_WithSampleResults_ReturnsValidMarkdown()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".markdown");
        var result = new LoggerUsageExtractionResult
        {
            Results = [
                new LoggerUsageInfo
                {
                    MethodName = "LogInformation",
                    MethodType = LoggerUsageMethodType.LoggerMethod,
                    LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                    MessageTemplate = "User {UserId} performed action {Action}",
                    Location = new MethodCallLocation
                    {
                        FilePath = "/Source/MyApp/Controllers/UserController.cs",
                        StartLineNumber = 42,
                        EndLineNumber = 42
                    },
                    MessageParameters = [
                        new MessageParameter("UserId", "int", "Argument"),
                        new MessageParameter("Action", "string", "Argument")
                    ],
                    EventId = new EventIdDetails(ConstantOrReference.Constant(1001), ConstantOrReference.Constant("UserAction"))
                }
            ],
            Summary = new LoggerUsageExtractionSummary
            {
                UniqueParameterNameCount = 2,
                TotalParameterUsageCount = 2,
                CommonParameterNames = [
                    new LoggerUsageExtractionSummary.CommonParameterNameInfo("UserId", 1, "int"),
                    new LoggerUsageExtractionSummary.CommonParameterNameInfo("Action", 1, "string")
                ]
            }
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().Contain("# Logger Usage Report");
        markdown.Should().Contain("## 📊 Summary");
        markdown.Should().Contain("| Total Log Usages | 1 |");
        markdown.Should().Contain("| Unique Parameter Names | 2 |");
        markdown.Should().Contain("ℹ️ Line 43: Information - LoggerMethod");
        markdown.Should().Contain("User {UserId} performed action {Action}");
        markdown.Should().Contain("| `UserId` | `int` | Argument |");
        markdown.Should().Contain("| `Action` | `string` | Argument |");
        markdown.Should().Contain("1001 (UserAction)");
        markdown.Should().Contain("UserController.cs");
    }

    [Fact]
    public void GenerateReport_WithParameterInconsistencies_IncludesInconsistencySection()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".md");
        var result = new LoggerUsageExtractionResult
        {
            Results = [],
            Summary = new LoggerUsageExtractionSummary
            {
                InconsistentParameterNames = [
                    new LoggerUsageExtractionSummary.InconsistentParameterNameInfo(
                        Names: [
                            new LoggerUsageExtractionSummary.NameTypePair("userId", "int"),
                            new LoggerUsageExtractionSummary.NameTypePair("UserId", "string")
                        ],
                        IssueTypes: ["Case inconsistency", "Type mismatch"]
                    )
                ]
            }
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().Contain("## ⚠️ Parameter Inconsistencies");
        markdown.Should().Contain("| `userId` | `int` |");
        markdown.Should().Contain("| `UserId` | `string` |");
        markdown.Should().Contain("- Case inconsistency");
        markdown.Should().Contain("- Type mismatch");
    }

    [Fact]
    public void GenerateReport_WithLoggerMessageUsage_IncludesLoggerMessageDetails()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".md");
        var result = new LoggerUsageExtractionResult
        {
            Results = [
                new LoggerMessageUsageInfo
                {
                    MethodName = "LogUserAction",
                    MethodType = LoggerUsageMethodType.LoggerMessageAttribute,
                    LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                    MessageTemplate = "User {UserId} performed action {Action}",
                    DeclaringTypeName = "MyApp.Services.UserService",
                    Location = new MethodCallLocation
                    {
                        FilePath = "/Source/MyApp/Services/UserService.cs",
                        StartLineNumber = 25,
                        EndLineNumber = 25
                    },
                    MessageParameters = [
                        new MessageParameter("UserId", "int", "Argument"),
                        new MessageParameter("Action", "string", "Argument")
                    ],
                    EventId = new EventIdDetails(ConstantOrReference.Constant(2001), ConstantOrReference.Constant("UserActionEvent")),
                    Invocations = [
                        new LoggerMessageInvocation
                        {
                            ContainingType = "MyApp.Controllers.UserController",
                            InvocationLocation = new MethodCallLocation
                            {
                                FilePath = "/Source/MyApp/Controllers/UserController.cs",
                                StartLineNumber = 45,
                                EndLineNumber = 45
                            },
                            Arguments = [
                                new MessageParameter("userId", "int", "Argument"),
                                new MessageParameter("userAction", "string", "Argument")
                            ]
                        }
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary
            {
                UniqueParameterNameCount = 2,
                TotalParameterUsageCount = 2,
                CommonParameterNames = [
                    new LoggerUsageExtractionSummary.CommonParameterNameInfo("UserId", 1, "int"),
                    new LoggerUsageExtractionSummary.CommonParameterNameInfo("Action", 1, "string")
                ]
            }
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().Contain("Line 26: Information - LoggerMessageAttribute");
        markdown.Should().Contain("**LoggerMessage Method Details:**");
        markdown.Should().Contain("- **Declaring Type:** `MyApp.Services.UserService`");
        markdown.Should().Contain("- **Method Name:** `LogUserAction`");
        markdown.Should().Contain("- **Invocation Count:** 1");
        markdown.Should().Contain("**Invocations:**");
        markdown.Should().Contain("- **UserController.cs** (Line 46)");
        markdown.Should().Contain("- **Containing Type:** `MyApp.Controllers.UserController`");
        markdown.Should().Contain("- `userId`: `int` (Argument)");
        markdown.Should().Contain("- `userAction`: `string` (Argument)");
    }

    [Fact]
    public void GenerateReport_WithLoggerMessageUsage_HtmlIncludesLoggerMessageDetails()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".html");
        var result = new LoggerUsageExtractionResult
        {
            Results = [
                new LoggerMessageUsageInfo
                {
                    MethodName = "LogUserAction",
                    MethodType = LoggerUsageMethodType.LoggerMessageAttribute,
                    LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                    MessageTemplate = "User {UserId} performed action {Action}",
                    DeclaringTypeName = "MyApp.Services.UserService",
                    Location = new MethodCallLocation
                    {
                        FilePath = "/Source/MyApp/Services/UserService.cs",
                        StartLineNumber = 25,
                        EndLineNumber = 25
                    },
                    MessageParameters = [
                        new MessageParameter("UserId", "int", "Argument"),
                        new MessageParameter("Action", "string", "Argument")
                    ],
                    EventId = new EventIdDetails(ConstantOrReference.Constant(2001), ConstantOrReference.Constant("UserActionEvent")),
                    Invocations = [
                        new LoggerMessageInvocation
                        {
                            ContainingType = "MyApp.Controllers.UserController",
                            InvocationLocation = new MethodCallLocation
                            {
                                FilePath = "/Source/MyApp/Controllers/UserController.cs",
                                StartLineNumber = 45,
                                EndLineNumber = 45
                            },
                            Arguments = [
                                new MessageParameter("userId", "int", "Argument"),
                                new MessageParameter("userAction", "string", "Argument")
                            ]
                        }
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary
            {
                UniqueParameterNameCount = 2,
                TotalParameterUsageCount = 2,
                CommonParameterNames = [
                    new LoggerUsageExtractionSummary.CommonParameterNameInfo("UserId", 1, "int"),
                    new LoggerUsageExtractionSummary.CommonParameterNameInfo("Action", 1, "string")
                ]
            }
        };

        // Act
        var html = generator.GenerateReport(result);

        // Assert
        html.Should().NotBeNull();
        html.Should().Contain("<title>Logger Usage Report</title>");
        html.Should().Contain("LoggerMessageAttribute");
        html.Should().Contain("MyApp.Services.UserService");
        html.Should().Contain("1 invocation");
        html.Should().Contain("Show Details");
        html.Should().Contain("UserController.cs:46");
        html.Should().Contain("MyApp.Controllers.UserController");
        html.Should().Contain("Invocations");
        html.Should().Contain("colspan='6'"); // Verify we updated the colspan for the new column
    }

    [Fact]
    public void GenerateReport_WithLogPropertiesTransitive_IncludesNestedProperties()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".md");
        var result = new LoggerUsageExtractionResult
        {
            Results = [
                new LoggerMessageUsageInfo
                {
                    MethodName = "LogUserDetails",
                    MethodType = LoggerUsageMethodType.LoggerMessageAttribute,
                    DeclaringTypeName = "MyApp.Logging.UserLogger",
                    LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                    MessageTemplate = "User details logged",
                    Location = new MethodCallLocation
                    {
                        FilePath = "/Source/MyApp/Logging/UserLogger.cs",
                        StartLineNumber = 10,
                        EndLineNumber = 10
                    },
                    EventId = new EventIdDetails(ConstantOrReference.Constant(100), ConstantOrReference.Constant("UserDetails")),
                    LogPropertiesParameters = [
                        new LogPropertiesParameterInfo(
                            "user",
                            "UserDetails",
                            new LogPropertiesConfiguration(OmitReferenceName: false, SkipNullProperties: false, Transitive: true),
                            [
                                new LogPropertyInfo("Name", "Name", "string", false, NestedProperties: null),
                                new LogPropertyInfo("Age", "Age", "int", false, NestedProperties: null),
                                new LogPropertyInfo("Address", "Address", "Address", false, NestedProperties: [
                                    new LogPropertyInfo("Street", "Street", "string", false, NestedProperties: null),
                                    new LogPropertyInfo("City", "City", "string", false, NestedProperties: null),
                                    new LogPropertyInfo("ZipCode", "ZipCode", "string", false, NestedProperties: null)
                                ])
                            ]
                        )
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().Contain("# Logger Usage Report");
        markdown.Should().Contain("**LogProperties Parameters:**");
        markdown.Should().Contain("**Parameter:** `user` (`UserDetails`)");
        markdown.Should().Contain("**Configuration:** Transitive");
        markdown.Should().Contain("**Properties:** 3 properties extracted");
        
        // Verify hierarchical structure
        markdown.Should().Contain("- `Name`: `string`");
        markdown.Should().Contain("- `Age`: `int`");
        markdown.Should().Contain("- `Address`: `Address` ⮑");
        
        // Verify nested properties with indentation
        markdown.Should().Contain("    - `Street`: `string`");
        markdown.Should().Contain("    - `City`: `string`");
        markdown.Should().Contain("    - `ZipCode`: `string`");
    }

    [Fact]
    public void GenerateReport_WithLogPropertiesCollections_ShowsCollectionTypes()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".md");
        var result = new LoggerUsageExtractionResult
        {
            Results = [
                new LoggerMessageUsageInfo
                {
                    MethodName = "LogTeam",
                    MethodType = LoggerUsageMethodType.LoggerMessageAttribute,
                    DeclaringTypeName = "MyApp.Logging.TeamLogger",
                    LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                    MessageTemplate = "Team logged",
                    Location = new MethodCallLocation
                    {
                        FilePath = "/Source/MyApp/Logging/TeamLogger.cs",
                        StartLineNumber = 20,
                        EndLineNumber = 20
                    },
                    EventId = new EventIdDetails(ConstantOrReference.Constant(200), ConstantOrReference.Constant("TeamDetails")),
                    LogPropertiesParameters = [
                        new LogPropertiesParameterInfo(
                            "team",
                            "Team",
                            new LogPropertiesConfiguration(OmitReferenceName: false, SkipNullProperties: false, Transitive: true),
                            [
                                new LogPropertyInfo("TeamName", "TeamName", "string", false, NestedProperties: null),
                                new LogPropertyInfo("Members", "Members", "List", false, NestedProperties: [
                                    new LogPropertyInfo("Name", "Name", "string", false, NestedProperties: null),
                                    new LogPropertyInfo("Role", "Role", "string", false, NestedProperties: null)
                                ])
                            ]
                        )
                    ]
                }
            ],
            Summary = new LoggerUsageExtractionSummary()
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().Contain("**LogProperties Parameters:**");
        markdown.Should().Contain("- `TeamName`: `string`");
        markdown.Should().Contain("- `Members`: `List` ⮑");
        markdown.Should().Contain("    - `Name`: `string`");
        markdown.Should().Contain("    - `Role`: `string`");
    }

    [Fact]
    public void GenerateReport_WithTelemetryFeatures_IncludesTelemetrySection()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".md");
        var result = new LoggerUsageExtractionResult
        {
            Results = [],
            Summary = new LoggerUsageExtractionSummary
            {
                TelemetryStats = new LoggerUsageExtractionSummary.TelemetryStatistics
                {
                    ParametersWithCustomTagNames = 3,
                    PropertiesWithCustomTagNames = 5,
                    ParametersWithTagProviders = 2,
                    TotalTransitiveProperties = 10,
                    CustomTagNameMappings =
                    [
                        new LoggerUsageExtractionSummary.CustomTagNameMapping("userId", "user.id", "Parameter"),
                        new LoggerUsageExtractionSummary.CustomTagNameMapping("userName", "user.name", "Parameter"),
                        new LoggerUsageExtractionSummary.CustomTagNameMapping("Id", "user.identifier", "Property")
                    ],
                    TagProviders =
                    [
                        new TagProviderInfo(
                            "request",
                            "MyApp.Providers.RequestProvider",
                            "ProvideTags",
                            OmitReferenceName: false,
                            IsValid: true
                        )
                    ]
                }
            }
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().Contain("### 🏷️ Telemetry Features Summary");
        markdown.Should().Contain("| Parameters with Custom Tag Names | 3 |");
        markdown.Should().Contain("| Properties with Custom Tag Names | 5 |");
        markdown.Should().Contain("| Parameters with Tag Providers | 2 |");
        markdown.Should().Contain("| Transitive Properties | 10 |");
        
        // Custom tag mappings
        markdown.Should().Contain("**Custom Tag Name Mappings:**");
        markdown.Should().Contain("| `userId` | `user.id` | Parameter |");
        markdown.Should().Contain("| `userName` | `user.name` | Parameter |");
        
        // Tag providers
        markdown.Should().Contain("**Tag Providers:**");
        markdown.Should().Contain("| `request` | `MyApp.Providers.RequestProvider` | `ProvideTags` | False | ✓ |");
    }

    [Fact]
    public void GenerateReport_WithInvalidTagProvider_ShowsValidationWarning()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".md");
        var result = new LoggerUsageExtractionResult
        {
            Results = [],
            Summary = new LoggerUsageExtractionSummary
            {
                TelemetryStats = new LoggerUsageExtractionSummary.TelemetryStatistics
                {
                    ParametersWithTagProviders = 1,
                    TagProviders =
                    [
                        new TagProviderInfo(
                            "data",
                            "MyApp.InvalidProvider",
                            "GetTags",
                            OmitReferenceName: false,
                            IsValid: false,
                            ValidationMessage: "Provider method not found"
                        )
                    ]
                }
            }
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().Contain("**Tag Providers:**");
        markdown.Should().Contain("| `data` | `MyApp.InvalidProvider` | `GetTags` | False | ⚠️ |");
        markdown.Should().Contain("**Validation:** Provider method not found");
    }
}
