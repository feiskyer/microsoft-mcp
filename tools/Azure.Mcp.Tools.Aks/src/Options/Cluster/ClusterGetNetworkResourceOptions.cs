// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Mcp.Tools.Aks.Options.Cluster;

public class ClusterGetNetworkResourceOptions : BaseAksOptions
{
    [JsonPropertyName(AksOptionDefinitions.ClusterName)]
    public string? ClusterName { get; set; }

    [JsonPropertyName(AksOptionDefinitions.ResourceTypeName)]
    public string? ResourceType { get; set; }

    // Optional future extension for parity with Go tool
    [JsonPropertyName("filters")]
    public string? Filters { get; set; }
}

