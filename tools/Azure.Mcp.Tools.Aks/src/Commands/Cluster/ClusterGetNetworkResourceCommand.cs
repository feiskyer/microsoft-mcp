// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Commands;
using Azure.Mcp.Tools.Aks.Options;
using Azure.Mcp.Tools.Aks.Options.Cluster;
using Azure.Mcp.Tools.Aks.Services;
using Microsoft.Extensions.Logging;

namespace Azure.Mcp.Tools.Aks.Commands.Cluster;

public sealed class ClusterGetNetworkResourceCommand(ILogger<ClusterGetNetworkResourceCommand> logger) : BaseAksCommand<ClusterGetNetworkResourceOptions>
{
    private const string CommandTitle = "Get AKS Cluster Network Resource";
    private readonly ILogger<ClusterGetNetworkResourceCommand> _logger = logger;

    private readonly Option<string> _clusterNameOption = AksOptionDefinitions.Cluster;
    private readonly Option<string> _resourceTypeOption = AksOptionDefinitions.ResourceType;

    private static readonly string[] s_supportedTypes =
    [
        "all", "vnet", "nsg", "route_table", "subnet", "load_balancer", "private_endpoint"
    ];

    public override string Name => "get-network-resource";

    public override string Description =>
        """
        Get Azure network resource information used by an AKS cluster.
        Supported types: all, vnet, nsg, route_table, subnet, load_balancer, private_endpoint.
        """;

    public override string Title => CommandTitle;

    public override ToolMetadata Metadata => new()
    {
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        ReadOnly = true,
        LocalRequired = false,
        Secret = false
    };

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        RequireResourceGroup();
        command.Options.Add(_clusterNameOption);
        command.Options.Add(_resourceTypeOption);
    }

    protected override ClusterGetNetworkResourceOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.ClusterName = parseResult.GetValue(_clusterNameOption);
        options.ResourceType = parseResult.GetValue(_resourceTypeOption)?.ToLowerInvariant();
        return options;
    }

    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
        if (!Validate(parseResult.CommandResult, context.Response).IsValid)
        {
            return context.Response;
        }

        var options = BindOptions(parseResult);

        if (!IsSupportedResourceType(options.ResourceType))
        {
            context.Response.Status = 400;
            context.Response.Message = $"Invalid --{AksOptionDefinitions.ResourceTypeName} value. Supported values: {string.Join(", ", s_supportedTypes)}";
            return context.Response;
        }

        try
        {
            var aksService = context.GetService<IAksService>();
            var json = await aksService.GetClusterNetworkResource(
                options.Subscription!,
                options.ResourceGroup!,
                options.ClusterName!,
                options.ResourceType!,
                options.Tenant,
                options.RetryPolicy);

            context.Response.Results = string.IsNullOrEmpty(json) ?
                null : ResponseResult.Create(
                    new ClusterGetNetworkResourceResult(options.ResourceType!, json),
                    AksJsonContext.Default.ClusterGetNetworkResourceResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting AKS network resource. Subscription: {Subscription}, ResourceGroup: {ResourceGroup}, ClusterName: {ClusterName}, ResourceType: {ResourceType}, Options: {@Options}",
                options.Subscription, options.ResourceGroup, options.ClusterName, options.ResourceType, options);
            HandleException(context, ex);
        }

        return context.Response;
    }

    public override ValidationResult Validate(CommandResult commandResult, CommandResponse? commandResponse = null)
    {
        var result = base.Validate(commandResult, commandResponse);
        if (!result.IsValid && commandResponse != null)
        {
            var msg = commandResponse.Message ?? string.Empty;
            if (!msg.Contains("Invalid", StringComparison.OrdinalIgnoreCase))
            {
                commandResponse.Message = $"Invalid {msg}".Trim();
                result.ErrorMessage = commandResponse.Message;
            }
        }
        return result;
    }

    private static bool IsSupportedResourceType(string? value) =>
        !string.IsNullOrEmpty(value) && s_supportedTypes.Contains(value);

    protected override string GetErrorMessage(Exception ex) => ex switch
    {
        RequestFailedException reqEx when reqEx.Status == 404 =>
            "AKS cluster or network resource not found. Verify names, resource group, and subscription, and ensure you have access.",
        RequestFailedException reqEx when reqEx.Status == 403 =>
            $"Authorization failed accessing AKS network resources. Details: {reqEx.Message}",
        RequestFailedException reqEx => reqEx.Message,
        _ => base.GetErrorMessage(ex)
    };

    protected override int GetStatusCode(Exception ex) => ex switch
    {
        RequestFailedException reqEx => reqEx.Status,
        _ => base.GetStatusCode(ex)
    };

    internal record ClusterGetNetworkResourceResult(string ResourceType, string JsonPayload);
}
