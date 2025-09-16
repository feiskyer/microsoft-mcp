// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Mcp.Tools.Aks.Options;

public static class AksOptionDefinitions
{
    public const string ClusterName = "cluster";
    public const string NodepoolName = "nodepool";
    public const string ResourceTypeName = "resource-type";

    public static readonly Option<string> Cluster = new(
        $"--{ClusterName}"
    )
    {
        Description = "AKS Cluster name.",
        Required = true
    };

    public static readonly Option<string> Nodepool = new(
        $"--{NodepoolName}"
    )
    {
        Description = "AKS node pool (agent pool) name.",
        Required = true
    };

    public static readonly Option<string> ResourceType = new(
        $"--{ResourceTypeName}"
    )
    {
        Description = "Network resource type to query: all, vnet, nsg, route_table, subnet, load_balancer, private_endpoint.",
        Required = true
    };
}
