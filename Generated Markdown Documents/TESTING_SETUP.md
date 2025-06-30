# Testing Setup Guide

## Overview

This guide explains how to set up testing packages for your .NET project and create comprehensive unit tests.

## Testing Packages Installed

### Core Testing Framework

- **Microsoft.NET.Test.Sdk** (17.8.0) - Test SDK for running tests
- **xUnit** (2.6.1) - Testing framework for .NET
- **xunit.runner.visualstudio** (2.5.3) - Visual Studio test runner for xUnit

### Assertion and Mocking Libraries

- **FluentAssertions** (6.12.0) - Fluent API for assertions
- **Moq** (4.20.69) - Mocking framework for creating test doubles

### Integration Testing

- **Microsoft.AspNetCore.Mvc.Testing** (9.0.0) - Testing support for ASP.NET Core
- **Microsoft.EntityFrameworkCore.InMemory** (9.0.6) - In-memory database for testing

### Code Coverage

- **coverlet.collector** (6.0.0) - Code coverage collector

## Project File Configuration

The testing packages are added to your `.csproj` file:

```xml
<!-- Testing Packages -->
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="xunit" Version="2.6.1" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.3">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="Moq" Version="4.20.69" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
<PackageReference Include="coverlet.collector" Version="6.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

## Test Structure

Tests are organized in the `Tests` folder with the following structure:

```
Tests/
├── Configuration/
│   └── CorsConfigurationTests.cs
├── Controllers/
├── Services/
└── Integration/
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=normal"

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName=CorsConfigurationTests"

# Run specific test method
dotnet test --filter "MethodName=GetAllowedOrigins_Development_ReturnsValidOrigins"
```

### Visual Studio Code

- Use the "Test Explorer" panel to run and debug tests
- Install the "C# Dev Kit" extension for better test integration
- Use Ctrl+; Ctrl+A to run all tests
- Use Ctrl+; Ctrl+F to run failed tests

## Test Patterns and Examples

### 1. Unit Test Structure (AAA Pattern)

```csharp
[Fact]
public void GetAllowedOrigins_Development_ReturnsValidOrigins()
{
    // Arrange
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"AppSettings:AllowedOrigins:0", "http://localhost:5173"},
            {"AppSettings:AllowedOrigins:1", "http://localhost:3000"}
        })
        .Build();

    // Act
    var origins = ConfigurationHelper.GetAllowedOrigins(configuration, "Development");

    // Assert
    origins.Should().Contain("http://localhost:5173");
    origins.Should().Contain("https://localhost:5173");
}
```

### 2. Theory Tests (Data-Driven Tests)

```csharp
[Theory]
[InlineData("http://localhost:5173", "Development", true)]
[InlineData("https://myapp.com", "Production", true)]
[InlineData("ftp://invalid.com", "Development", false)]
[InlineData("not-a-url", "Development", false)]
public void GetAllowedOrigins_HandlesVariousOriginFormats(string frontendUrl, string environment, bool shouldContainOrigin)
{
    // Test implementation
}
```

### 3. Exception Testing

```csharp
[Fact]
public void ValidateConfiguration_InvalidOrigins_ThrowsException()
{
    // Arrange
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"AppSettings:FrontendUrl", "not-a-valid-url"}
        })
        .Build();

    // Act & Assert
    var action = () => ConfigurationHelper.ValidateConfiguration(configuration, "Development");
    action.Should().Throw<InvalidOperationException>()
        .WithMessage("*Invalid CORS origins*");
}
```

### 4. FluentAssertions Examples

```csharp
// Collection assertions
origins.Should().Contain("http://localhost:5173");
origins.Should().NotContain("http://insecure.com");
origins.Should().HaveCount(2);
origins.Should().NotBeEmpty();

// String assertions
message.Should().StartWith("Configuration");
message.Should().Contain("validation failed");

// Exception assertions
action.Should().Throw<InvalidOperationException>()
    .WithMessage("*HTTP origins not allowed*");

// Boolean assertions
result.Should().BeTrue();
result.Should().BeFalse();
```

## Configuration Testing

### In-Memory Configuration

```csharp
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        {"Jwt:Key", "test-key"},
        {"AppSettings:FrontendUrl", "https://myapp.com"}
    })
    .Build();
```

### JSON Configuration Files

```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.test.json")
    .Build();
```

## Mocking with Moq

### Interface Mocking

```csharp
[Fact]
public void ServiceMethod_CallsDependency_ReturnsExpectedResult()
{
    // Arrange
    var mockDependency = new Mock<IDependency>();
    mockDependency.Setup(x => x.GetData()).Returns("test data");
    
    var service = new MyService(mockDependency.Object);

    // Act
    var result = service.ProcessData();

    // Assert
    result.Should().Be("processed: test data");
    mockDependency.Verify(x => x.GetData(), Times.Once);
}
```

## Integration Testing

### Web Application Factory

```csharp
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetEndpoint_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString()
            .Should().Be("application/json; charset=utf-8");
    }
}
```

## Code Coverage

### Generate Coverage Report

```bash
# Install coverage tools
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator "-reports:TestResults/**/coverage.cobertura.xml" "-targetdir:coverage" "-reporttypes:Html"
```

### Coverage Configuration

Create a `coverlet.runsettings` file:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Exclude>[*]*.Migrations.*</Exclude>
          <ExcludeByFile>**/Program.cs</ExcludeByFile>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

## Best Practices

### 1. Test Naming

- Use descriptive names that explain what is being tested
- Format: `MethodName_Scenario_ExpectedBehavior`
- Example: `GetAllowedOrigins_Development_ReturnsValidOrigins`

### 2. Test Organization

- Group related tests in the same class
- Use nested classes for complex scenarios
- Follow the same namespace structure as your source code

### 3. Test Data

- Use builders or object mothers for complex test data
- Keep test data minimal and focused
- Use constants for commonly used test values

### 4. Assertions

- Use FluentAssertions for more readable assertions
- Be specific about what you're testing
- Include meaningful failure messages

### 5. Test Independence

- Each test should be independent and able to run in isolation
- Clean up resources after tests
- Don't rely on test execution order

## Common Testing Commands

```bash
# Install packages
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package xunit
dotnet add package FluentAssertions
dotnet add package Moq

# Create test class
dotnet new xunit -n MyProject.Tests

# Run specific tests
dotnet test --filter "Category=Unit"
dotnet test --filter "FullyQualifiedName~CorsConfiguration"

# Watch mode for continuous testing
dotnet watch test
```

## Troubleshooting

### Common Issues

1. **Missing test packages** - Run `dotnet restore` to install packages
2. **Tests not discovered** - Ensure test methods are marked with `[Fact]` or `[Theory]`
3. **Assembly loading issues** - Check that all dependencies are properly referenced
4. **Mocking private members** - Use internal/protected members or refactor for testability

### Debug Tests

- Set breakpoints in test methods
- Use `Debug Test` option in VS Code Test Explorer
- Add `Console.WriteLine()` statements for debugging
- Use `--logger trx` for detailed test output files
