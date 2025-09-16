// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Mcp.Tests;
using Azure.Mcp.Tests.Client;
using Xunit;

namespace Azure.Mcp.Tools.Aks.LiveTests;

public sealed class NetworkCommandTests(ITestOutputHelper output)
    : CommandTestsBase(output)
{
    [Fact]
    public async Task Should_get_network_resources_all_for_cluster()
    {
        // Discover a real cluster to target
        var listResult = await CallToolAsync(
            "azmcp_aks_cluster_list",
            new()
            {
                { "subscription", Settings.SubscriptionId }
            });

        var clusters = listResult.AssertProperty("clusters");
        Assert.True(clusters.GetArrayLength() > 0, "Expected at least one AKS cluster for testing network resource command");

        var firstCluster = clusters.EnumerateArray().First();
        var clusterName = firstCluster.GetProperty("name").GetString()!;
        var resourceGroupName = firstCluster.GetProperty("resourceGroupName").GetString()!;

        // Query all network resources
        var netResult = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", resourceGroupName },
                { "cluster", clusterName },
                { "resource-type", "all" }
            });

        // Tool returns an envelope with jsonPayload string
        var payload = netResult.AssertProperty("jsonPayload");
        Assert.Equal(JsonValueKind.String, payload.ValueKind);
        var json = payload.GetString();
        Assert.False(string.IsNullOrEmpty(json));

        using var doc = JsonDocument.Parse(json!);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);

        // Verify at least one expected key exists (content varies by cluster)
        var root = doc.RootElement;
        var expectedKeys = new[] { "vnet", "subnet", "nsg", "route_table", "load_balancer", "private_endpoint" };
        Assert.Contains(expectedKeys, k => root.TryGetProperty(k, out _) || root.TryGetProperty(k + "_error", out _));
    }

    [Fact]
    public async Task Should_validate_required_parameters()
    {
        // Missing resource-type
        var r1 = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", "rg" },
                { "cluster", "cluster" }
            });
        Assert.False(r1.HasValue);

        // Missing cluster
        var r2 = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", "rg" },
                { "resource-type", "vnet" }
            });
        Assert.False(r2.HasValue);

        // Missing resource-group
        var r3 = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "cluster", "cluster" },
                { "resource-type", "vnet" }
            });
        Assert.False(r3.HasValue);

        // Missing subscription
        var r4 = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "resource-group", "rg" },
                { "cluster", "cluster" },
                { "resource-type", "vnet" }
            });
        Assert.False(r4.HasValue);
    }

    [Fact]
    public async Task Should_handle_invalid_resource_type_gracefully()
    {
        var result = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", "rg" },
                { "cluster", "cluster" },
                { "resource-type", "invalid-type" }
            });
        Assert.False(result.HasValue);
    }

    [Fact]
    public async Task Should_get_vnet_info_for_cluster()
    {
        var listResult = await CallToolAsync(
            "azmcp_aks_cluster_list",
            new() { { "subscription", Settings.SubscriptionId } });
        var first = listResult.AssertProperty("clusters").EnumerateArray().First();
        var clusterName = first.GetProperty("name").GetString()!;
        var rgName = first.GetProperty("resourceGroupName").GetString()!;

        var result = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", rgName },
                { "cluster", clusterName },
                { "resource-type", "vnet" }
            });

        var payload = result.AssertProperty("jsonPayload").GetString();
        Assert.False(string.IsNullOrEmpty(payload));
        using var doc = JsonDocument.Parse(payload!);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        // Optional heuristic check
        if (doc.RootElement.TryGetProperty("id", out var idProp))
        {
            Assert.Contains("/virtualNetworks/", idProp.GetString());
        }
    }

    [Fact]
    public async Task Should_get_subnet_info_for_cluster()
    {
        var listResult = await CallToolAsync(
            "azmcp_aks_cluster_list",
            new() { { "subscription", Settings.SubscriptionId } });
        var first = listResult.AssertProperty("clusters").EnumerateArray().First();
        var clusterName = first.GetProperty("name").GetString()!;
        var rgName = first.GetProperty("resourceGroupName").GetString()!;

        var result = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", rgName },
                { "cluster", clusterName },
                { "resource-type", "subnet" }
            });

        var payload = result.AssertProperty("jsonPayload").GetString();
        Assert.False(string.IsNullOrEmpty(payload));
        using var doc = JsonDocument.Parse(payload!);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        if (doc.RootElement.TryGetProperty("id", out var idProp))
        {
            Assert.Contains("/subnets/", idProp.GetString());
        }
    }

    [Fact]
    public async Task Should_get_nsg_info_or_message()
    {
        var listResult = await CallToolAsync(
            "azmcp_aks_cluster_list",
            new() { { "subscription", Settings.SubscriptionId } });
        var first = listResult.AssertProperty("clusters").EnumerateArray().First();
        var clusterName = first.GetProperty("name").GetString()!;
        var rgName = first.GetProperty("resourceGroupName").GetString()!;

        var result = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", rgName },
                { "cluster", clusterName },
                { "resource-type", "nsg" }
            });

        var payload = result.AssertProperty("jsonPayload").GetString();
        Assert.False(string.IsNullOrEmpty(payload));
        using var doc = JsonDocument.Parse(payload!);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public async Task Should_get_route_table_or_message()
    {
        var listResult = await CallToolAsync(
            "azmcp_aks_cluster_list",
            new() { { "subscription", Settings.SubscriptionId } });
        var first = listResult.AssertProperty("clusters").EnumerateArray().First();
        var clusterName = first.GetProperty("name").GetString()!;
        var rgName = first.GetProperty("resourceGroupName").GetString()!;

        var result = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", rgName },
                { "cluster", clusterName },
                { "resource-type", "route_table" }
            });

        var payload = result.AssertProperty("jsonPayload").GetString();
        Assert.False(string.IsNullOrEmpty(payload));
        using var doc = JsonDocument.Parse(payload!);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public async Task Should_get_load_balancers_or_message()
    {
        var listResult = await CallToolAsync(
            "azmcp_aks_cluster_list",
            new() { { "subscription", Settings.SubscriptionId } });
        var first = listResult.AssertProperty("clusters").EnumerateArray().First();
        var clusterName = first.GetProperty("name").GetString()!;
        var rgName = first.GetProperty("resourceGroupName").GetString()!;

        var result = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", rgName },
                { "cluster", clusterName },
                { "resource-type", "load_balancer" }
            });

        var payload = result.AssertProperty("jsonPayload").GetString();
        Assert.False(string.IsNullOrEmpty(payload));
        using var doc = JsonDocument.Parse(payload!);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object || doc.RootElement.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task Should_get_private_endpoint_or_message()
    {
        var listResult = await CallToolAsync(
            "azmcp_aks_cluster_list",
            new() { { "subscription", Settings.SubscriptionId } });
        var first = listResult.AssertProperty("clusters").EnumerateArray().First();
        var clusterName = first.GetProperty("name").GetString()!;
        var rgName = first.GetProperty("resourceGroupName").GetString()!;

        var result = await CallToolAsync(
            "azmcp_aks_cluster_get-network-resource",
            new()
            {
                { "subscription", Settings.SubscriptionId },
                { "resource-group", rgName },
                { "cluster", clusterName },
                { "resource-type", "private_endpoint" }
            });

        var payload = result.AssertProperty("jsonPayload").GetString();
        Assert.False(string.IsNullOrEmpty(payload));
        using var doc = JsonDocument.Parse(payload!);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object || doc.RootElement.ValueKind == JsonValueKind.Array);
    }
}
