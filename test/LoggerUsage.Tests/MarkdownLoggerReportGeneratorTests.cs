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
        Assert.NotNull(markdown);
        Assert.Contains("# Logger Usage Report", markdown);
        Assert.Contains("## 📊 Summary", markdown);
        Assert.Contains("*No logger usages found.*", markdown);
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
        Assert.NotNull(markdown);
        Assert.Contains("# Logger Usage Report", markdown);
        Assert.Contains("## 📊 Summary", markdown);
        Assert.Contains("| Total Log Usages | 1 |", markdown);
        Assert.Contains("| Unique Parameter Names | 2 |", markdown);
        Assert.Contains("ℹ️ Line 43: Information - LoggerMethod", markdown);
        Assert.Contains("User {UserId} performed action {Action}", markdown);
        Assert.Contains("| `UserId` | `int` | Argument |", markdown);
        Assert.Contains("| `Action` | `string` | Argument |", markdown);
        Assert.Contains("1001 (UserAction)", markdown);
        Assert.Contains("UserController.cs", markdown);
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
        Assert.NotNull(markdown);
        Assert.Contains("## ⚠️ Parameter Inconsistencies", markdown);
        Assert.Contains("| `userId` | `int` |", markdown);
        Assert.Contains("| `UserId` | `string` |", markdown);
        Assert.Contains("- Case inconsistency", markdown);
        Assert.Contains("- Type mismatch", markdown);
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
        Assert.NotNull(markdown);
        Assert.Contains("Line 26: Information - LoggerMessageAttribute", markdown);
        Assert.Contains("**LoggerMessage Method Details:**", markdown);
        Assert.Contains("- **Declaring Type:** `MyApp.Services.UserService`", markdown);
        Assert.Contains("- **Method Name:** `LogUserAction`", markdown);
        Assert.Contains("- **Invocation Count:** 1", markdown);
        Assert.Contains("**Invocations:**", markdown);
        Assert.Contains("- **UserController.cs** (Line 46)", markdown);
        Assert.Contains("- **Containing Type:** `MyApp.Controllers.UserController`", markdown);
        Assert.Contains("- `userId`: `int` (Argument)", markdown);
        Assert.Contains("- `userAction`: `string` (Argument)", markdown);
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
        Assert.NotNull(html);
        Assert.Contains("<title>Logger Usage Report</title>", html);
        Assert.Contains("LoggerMessageAttribute", html);
        Assert.Contains("MyApp.Services.UserService", html);
        Assert.Contains("1 invocation", html);
        Assert.Contains("Show Details", html);
        Assert.Contains("UserController.cs:46", html);
        Assert.Contains("MyApp.Controllers.UserController", html);
        Assert.Contains("Invocations", html);
        Assert.Contains("colspan='6'", html); // Verify we updated the colspan for the new column
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
        Assert.NotNull(markdown);
        Assert.Contains("# Logger Usage Report", markdown);
        Assert.Contains("**LogProperties Parameters:**", markdown);
        Assert.Contains("**Parameter:** `user` (`UserDetails`)", markdown);
        Assert.Contains("**Configuration:** Transitive", markdown);
        Assert.Contains("**Properties:** 3 properties extracted", markdown);
        
        // Verify hierarchical structure
        Assert.Contains("- `Name`: `string`", markdown);
        Assert.Contains("- `Age`: `int`", markdown);
        Assert.Contains("- `Address`: `Address` ⮑", markdown);
        
        // Verify nested properties with indentation
        Assert.Contains("    - `Street`: `string`", markdown);
        Assert.Contains("    - `City`: `string`", markdown);
        Assert.Contains("    - `ZipCode`: `string`", markdown);
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
        Assert.NotNull(markdown);
        Assert.Contains("**LogProperties Parameters:**", markdown);
        Assert.Contains("- `TeamName`: `string`", markdown);
        Assert.Contains("- `Members`: `List` ⮑", markdown);
        Assert.Contains("    - `Name`: `string`", markdown);
        Assert.Contains("    - `Role`: `string`", markdown);
    }
}
