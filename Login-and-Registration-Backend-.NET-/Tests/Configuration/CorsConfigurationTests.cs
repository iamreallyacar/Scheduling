using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;
using Login_and_Registration_Backend_.NET_.Configuration;

namespace Login_and_Registration_Backend_.NET_.Tests.Configuration;

public class CorsConfigurationTests
{
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
        origins.Should().Contain("https://localhost:5173"); // Should add HTTPS version
        origins.Should().Contain("http://localhost:3000");
        origins.Should().Contain("https://localhost:3000"); // Should add HTTPS version
    }

    [Fact]
    public void GetAllowedOrigins_Production_OnlyReturnsHttpsOrigins()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"AppSettings:AllowedOrigins:0", "https://myapp.com"},
                {"AppSettings:AllowedOrigins:1", "http://insecure.com"} // Should be filtered out
            })
            .Build();

        // Act
        var origins = ConfigurationHelper.GetAllowedOrigins(configuration, "Production");

        // Assert
        origins.Should().Contain("https://myapp.com");
        origins.Should().NotContain("http://insecure.com");
    }

    [Fact]
    public void GetAllowedOrigins_FallsBackToFrontendUrl_WhenAllowedOriginsNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"AppSettings:FrontendUrl", "https://myapp.com"}
            })
            .Build();

        // Act
        var origins = ConfigurationHelper.GetAllowedOrigins(configuration, "Production");

        // Assert
        origins.Should().Contain("https://myapp.com");
        origins.Should().HaveCount(1);
    }

    [Fact]
    public void GetAllowedOrigins_FallsBackToDefaults_WhenNoConfigurationProvided()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var origins = ConfigurationHelper.GetAllowedOrigins(configuration, "Development");

        // Assert
        origins.Should().Contain("http://localhost:5173");
        origins.Should().Contain("https://localhost:5173");
    }

    [Fact]
    public void ValidateConfiguration_InvalidOrigins_ThrowsException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Jwt:Key", "test-key"},
                {"Jwt:Issuer", "test-issuer"},
                {"Jwt:Audience", "test-audience"},
                {"Authentication:Google:ClientId", "test-client-id"},
                {"Authentication:Google:ClientSecret", "test-client-secret"},
                {"AppSettings:FrontendUrl", "not-a-valid-url"}
            })
            .Build();

        // Act & Assert
        var action = () => ConfigurationHelper.ValidateConfiguration(configuration, "Development");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid CORS origins*");
    }

    [Fact]
    public void ValidateConfiguration_ProductionWithHttpOrigins_ThrowsException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Jwt:Key", "test-key"},
                {"Jwt:Issuer", "test-issuer"},
                {"Jwt:Audience", "test-audience"},
                {"Authentication:Google:ClientId", "test-client-id"},
                {"Authentication:Google:ClientSecret", "test-client-secret"},
                {"AppSettings:FrontendUrl", "http://insecure.com"}
            })
            .Build();

        // Act & Assert
        var action = () => ConfigurationHelper.ValidateConfiguration(configuration, "Production");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*HTTP origins not allowed in production*");
    }

    [Fact]
    public void ValidateConfiguration_ValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Jwt:Key", "test-key"},
                {"Jwt:Issuer", "test-issuer"},
                {"Jwt:Audience", "test-audience"},
                {"Authentication:Google:ClientId", "test-client-id"},
                {"Authentication:Google:ClientSecret", "test-client-secret"},
                {"AppSettings:FrontendUrl", "https://myapp.com"}
            })
            .Build();

        // Act & Assert
        var action = () => ConfigurationHelper.ValidateConfiguration(configuration, "Production");
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("http://localhost:5173", "Development", true)]
    [InlineData("https://myapp.com", "Production", true)]
    [InlineData("ftp://invalid.com", "Development", false)]
    [InlineData("not-a-url", "Development", false)]
    [InlineData("", "Development", false)]
    [InlineData("http://insecure.com", "Production", false)]
    public void GetAllowedOrigins_HandlesVariousOriginFormats(string frontendUrl, string environment, bool shouldContainOrigin)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"AppSettings:FrontendUrl", frontendUrl}
            })
            .Build();

        // Act
        var origins = ConfigurationHelper.GetAllowedOrigins(configuration, environment);
        
        // Assert
        if (shouldContainOrigin && !string.IsNullOrEmpty(frontendUrl))
        {
            origins.Should().Contain(frontendUrl);
        }
        else
        {
            // Should fall back to defaults if invalid
            origins.Should().NotBeEmpty();
            if (environment == "Development")
            {
                origins.Should().Contain("http://localhost:5173");
            }
            else if (environment == "Production")
            {
                origins.Should().Contain("https://your-production-domain.com");
            }
        }
    }

    [Fact]
    public void GetAllowedOrigins_Development_AddsAlternateProtocolVersions()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"AppSettings:FrontendUrl", "http://localhost:3000"}
            })
            .Build();

        // Act
        var origins = ConfigurationHelper.GetAllowedOrigins(configuration, "Development");

        // Assert
        origins.Should().Contain("http://localhost:3000");
        origins.Should().Contain("https://localhost:3000");
        origins.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetAllowedOrigins_NonDevelopment_DoesNotAddAlternateProtocolVersions()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"AppSettings:FrontendUrl", "https://myapp.com"}
            })
            .Build();

        // Act
        var origins = ConfigurationHelper.GetAllowedOrigins(configuration, "Production");

        // Assert
        origins.Should().Contain("https://myapp.com");
        origins.Should().NotContain("http://myapp.com");
        origins.Should().HaveCount(1);
    }
}
