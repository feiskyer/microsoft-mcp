// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Mcp.Core.Models.Command;
using Azure.Mcp.Tools.Aks.Commands;
using Azure.Mcp.Tools.Aks.Commands.Cluster;
using Azure.Mcp.Tools.Aks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Azure.Mcp.Tools.Aks.UnitTests.Cluster;

public sealed class ClusterGetNetworkResourceCommandShapeTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAksService _aksService;
    private readonly ILogger<ClusterGetNetworkResourceCommand> _logger;
    private readonly ClusterGetNetworkResourceCommand _command;

    public ClusterGetNetworkResourceCommandShapeTests()
    {
        _aksService = Substitute.For<IAksService>();
        _logger = Substitute.For<ILogger<ClusterGetNetworkResourceCommand>>();

        var collection = new ServiceCollection();
        collection.AddSingleton(_aksService);
        _serviceProvider = collection.BuildServiceProvider();

        _command = new(_logger);
    }

    [Fact]
    public async Task ExecuteAsync_ProducesExpectedResultEnvelope()
    {
        _aksService.GetClusterNetworkResource(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Core.Options.RetryPolicyOptions>())
            .Returns("{\"key\":\"value\"}");

        var context = new CommandContext(_serviceProvider);
        var parseResult = _command.GetCommand().Parse("--subscription sub --resource-group rg --cluster c --resource-type vnet");
        var response = await _command.ExecuteAsync(context, parseResult);

        Assert.Equal(200, response.Status);
        Assert.NotNull(response.Results);

        var json = JsonSerializer.Serialize(response.Results);
        var result = JsonSerializer.Deserialize(json, AksJsonContext.Default.ClusterGetNetworkResourceResult);

        Assert.NotNull(result);
        Assert.Equal("vnet", result.ResourceType);
        Assert.Equal("{\"key\":\"value\"}", result.JsonPayload);
    }
}

