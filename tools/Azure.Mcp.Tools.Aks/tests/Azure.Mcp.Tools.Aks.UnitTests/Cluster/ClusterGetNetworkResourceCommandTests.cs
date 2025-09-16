// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using Azure.Mcp.Core.Models.Command;
using Azure.Mcp.Tools.Aks.Commands.Cluster;
using Azure.Mcp.Tools.Aks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Azure.Mcp.Tools.Aks.UnitTests.Cluster;

public class ClusterGetNetworkResourceCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAksService _aksService;
    private readonly ILogger<ClusterGetNetworkResourceCommand> _logger;
    private readonly ClusterGetNetworkResourceCommand _command;
    private readonly CommandContext _context;
    private readonly Command _commandDefinition;

    public ClusterGetNetworkResourceCommandTests()
    {
        _aksService = Substitute.For<IAksService>();
        _logger = Substitute.For<ILogger<ClusterGetNetworkResourceCommand>>();

        var collection = new ServiceCollection().AddSingleton(_aksService);
        _serviceProvider = collection.BuildServiceProvider();
        _command = new(_logger);
        _context = new(_serviceProvider);
        _commandDefinition = _command.GetCommand();
    }

    [Fact]
    public void Constructor_InitializesCommandCorrectly()
    {
        var command = _command.GetCommand();
        Assert.Equal("get-network-resource", command.Name);
        Assert.NotNull(command.Description);
        Assert.NotEmpty(command.Description);
    }

    [Theory]
    [InlineData("--subscription sub1 --resource-group rg1 --cluster c1 --resource-type vnet", true)]
    [InlineData("--subscription sub1 --resource-group rg1 --cluster c1 --resource-type all", true)]
    [InlineData("--subscription sub1 --resource-group rg1 --cluster c1 --resource-type invalid", false)]
    [InlineData("--subscription sub1 --resource-group rg1 --resource-type vnet", false)] // Missing cluster
    [InlineData("--subscription sub1 --cluster c1 --resource-type vnet", false)] // Missing RG
    [InlineData("--resource-type vnet --resource-group rg1 --cluster c1", false)] // Missing subscription
    [InlineData("--subscription sub1 --resource-group rg1 --cluster c1", false)] // Missing resource-type
    public async Task ExecuteAsync_ValidatesInputCorrectly(string args, bool shouldSucceed)
    {
        // Arrange
        if (shouldSucceed)
        {
            _aksService.GetClusterNetworkResource(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Core.Options.RetryPolicyOptions>())
                .Returns("{\"sample\":true}");
        }

        var parseResult = _commandDefinition.Parse(args);

        // Act
        var response = await _command.ExecuteAsync(_context, parseResult);

        // Assert
        Assert.Equal(shouldSucceed ? 200 : 400, response.Status);
        if (shouldSucceed)
        {
            Assert.NotNull(response.Results);
            Assert.Equal("Success", response.Message);
        }
        else
        {
            Assert.Contains("Invalid", response.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPayloadWhenAvailable()
    {
        // Arrange
        _aksService.GetClusterNetworkResource(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Core.Options.RetryPolicyOptions>())
            .Returns("{\"k\":\"v\"}");

        var parseResult = _commandDefinition.Parse(["--subscription", "sub1", "--resource-group", "rg1", "--cluster", "c1", "--resource-type", "vnet"]);

        // Act
        var response = await _command.ExecuteAsync(_context, parseResult);

        // Assert
        Assert.Equal(200, response.Status);
        Assert.NotNull(response.Results);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesServiceErrors()
    {
        // Arrange
        _aksService.GetClusterNetworkResource(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Core.Options.RetryPolicyOptions>())
            .Returns(Task.FromException<string>(new Exception("Test error")));

        var parseResult = _commandDefinition.Parse(["--subscription", "sub1", "--resource-group", "rg1", "--cluster", "c1", "--resource-type", "vnet"]);

        // Act
        var response = await _command.ExecuteAsync(_context, parseResult);

        // Assert
        Assert.Equal(500, response.Status);
        Assert.Contains("Test error", response.Message);
        Assert.Contains("troubleshooting", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Handles403And404()
    {
        var ex403 = new RequestFailedException(403, "Forbidden");
        _aksService.GetClusterNetworkResource(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Core.Options.RetryPolicyOptions>())
            .Returns(Task.FromException<string>(ex403));

        var parseResult403 = _commandDefinition.Parse(["--subscription", "sub1", "--resource-group", "rg1", "--cluster", "c1", "--resource-type", "vnet"]);
        var response403 = await _command.ExecuteAsync(_context, parseResult403);
        Assert.Equal(403, response403.Status);
        Assert.Contains("Authorization failed", response403.Message);

        var ex404 = new RequestFailedException(404, "Not Found");
        _aksService.GetClusterNetworkResource(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Core.Options.RetryPolicyOptions>())
            .Returns(Task.FromException<string>(ex404));

        var parseResult404 = _commandDefinition.Parse(["--subscription", "sub1", "--resource-group", "rg1", "--cluster", "c1", "--resource-type", "vnet"]);
        var response404 = await _command.ExecuteAsync(_context, parseResult404);
        Assert.Equal(404, response404.Status);
        Assert.Contains("not found", response404.Message, StringComparison.OrdinalIgnoreCase);
    }
}

