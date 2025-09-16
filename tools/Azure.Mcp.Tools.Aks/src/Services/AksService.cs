// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Options;
using Azure.Mcp.Core.Services.Azure;
using Azure.Mcp.Core.Services.Azure.Subscription;
using Azure.Mcp.Core.Services.Azure.Tenant;
using Azure.Mcp.Core.Services.Caching;
using Azure.Mcp.Tools.Aks.Models;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.Network;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace Azure.Mcp.Tools.Aks.Services;

public sealed class AksService(
    ISubscriptionService subscriptionService,
    ITenantService tenantService,
    ICacheService cacheService) : BaseAzureService(tenantService), IAksService
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

    private const string CacheGroup = "aks";
    private const string AksClustersCacheKey = "clusters";
    private const string AksNodePoolsCacheKey = "nodepools";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromHours(1);

    public async Task<List<Cluster>> ListClusters(
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription);

        // Create cache key
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"{AksClustersCacheKey}_{subscription}"
            : $"{AksClustersCacheKey}_{subscription}_{tenant}";

        // Try to get from cache first
        var cachedClusters = await _cacheService.GetAsync<List<Cluster>>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedClusters != null)
        {
            return cachedClusters;
        }

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);
        var clusters = new List<Cluster>();

        try
        {
            await foreach (var cluster in subscriptionResource.GetContainerServiceManagedClustersAsync())
            {
                if (cluster?.Data != null)
                {
                    clusters.Add(ConvertToClusterModel(cluster));
                }
            }

            // Cache the results
            await _cacheService.SetAsync(CacheGroup, cacheKey, clusters, s_cacheDuration);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving AKS clusters: {ex.Message}", ex);
        }

        return clusters;
    }

    public async Task<Cluster?> GetCluster(
        string subscription,
        string clusterName,
        string resourceGroup,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, clusterName, resourceGroup);

        // Create cache key
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"cluster_{subscription}_{resourceGroup}_{clusterName}"
            : $"cluster_{subscription}_{resourceGroup}_{clusterName}_{tenant}";

        // Try to get from cache first
        var cachedCluster = await _cacheService.GetAsync<Cluster>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedCluster != null)
        {
            return cachedCluster;
        }

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);

        try
        {
            var resourceGroupResource = await subscriptionResource
                .GetResourceGroupAsync(resourceGroup);

            if (resourceGroupResource?.Value == null)
            {
                return null;
            }

            var clusterResource = await resourceGroupResource.Value
                .GetContainerServiceManagedClusters()
                .GetAsync(clusterName);

            if (clusterResource?.Value?.Data == null)
            {
                return null;
            }

            var cluster = ConvertToClusterModel(clusterResource.Value);

            // Cache the result
            await _cacheService.SetAsync(CacheGroup, cacheKey, cluster, s_cacheDuration);

            return cluster;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving AKS cluster '{clusterName}': {ex.Message}", ex);
        }
    }

    public async Task<List<NodePool>> ListNodePools(
        string subscription,
        string resourceGroup,
        string clusterName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, resourceGroup, clusterName);

        // Create cache key
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"{AksNodePoolsCacheKey}_{subscription}_{resourceGroup}_{clusterName}"
            : $"{AksNodePoolsCacheKey}_{subscription}_{resourceGroup}_{clusterName}_{tenant}";

        // Try to get from cache first
        var cachedNodePools = await _cacheService.GetAsync<List<NodePool>>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedNodePools != null)
        {
            return cachedNodePools;
        }

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);
        var nodePools = new List<NodePool>();

        try
        {
            var resourceGroupResource = await subscriptionResource.GetResourceGroupAsync(resourceGroup);
            if (resourceGroupResource?.Value == null)
            {
                return nodePools;
            }

            var clusterResource = await resourceGroupResource.Value
                .GetContainerServiceManagedClusters()
                .GetAsync(clusterName);

            if (clusterResource?.Value == null)
            {
                return nodePools;
            }

            await foreach (var agentPool in clusterResource.Value
                               .GetContainerServiceAgentPools()
                               .GetAllAsync())
            {
                if (agentPool?.Data != null)
                {
                    nodePools.Add(ConvertToNodePoolModel(agentPool));
                }
            }

            // Cache the results
            await _cacheService.SetAsync(CacheGroup, cacheKey, nodePools, s_cacheDuration);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving AKS node pools for cluster '{clusterName}': {ex.Message}", ex);
        }

        return nodePools;
    }

    public async Task<NodePool?> GetNodePool(
        string subscription,
        string resourceGroup,
        string clusterName,
        string nodePoolName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, resourceGroup, clusterName, nodePoolName);

        // Create cache key
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"nodepool_{subscription}_{resourceGroup}_{clusterName}_{nodePoolName}"
            : $"nodepool_{subscription}_{resourceGroup}_{clusterName}_{nodePoolName}_{tenant}";

        // Try to get from cache first
        var cachedNodePool = await _cacheService.GetAsync<NodePool>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedNodePool != null)
        {
            return cachedNodePool;
        }

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);

        try
        {
            var resourceGroupResource = await subscriptionResource.GetResourceGroupAsync(resourceGroup);
            if (resourceGroupResource?.Value == null)
            {
                return null;
            }

            var clusterResource = await resourceGroupResource.Value
                .GetContainerServiceManagedClusters()
                .GetAsync(clusterName);

            if (clusterResource?.Value == null)
            {
                return null;
            }

            var agentPoolResource = await clusterResource.Value
                .GetContainerServiceAgentPools()
                .GetAsync(nodePoolName);

            if (agentPoolResource?.Value?.Data == null)
            {
                return null;
            }

            var nodePool = ConvertToNodePoolModel(agentPoolResource.Value);

            // Cache the result
            await _cacheService.SetAsync(CacheGroup, cacheKey, nodePool, s_cacheDuration);

            return nodePool;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving AKS node pool '{nodePoolName}' for cluster '{clusterName}': {ex.Message}", ex);
        }
    }

    public async Task<string> GetClusterNetworkResource(
        string subscription,
        string resourceGroup,
        string clusterName,
        string resourceType,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, resourceGroup, clusterName, resourceType);

        var type = resourceType.Trim().ToLowerInvariant();
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "all", "vnet", "nsg", "route_table", "subnet", "load_balancer", "private_endpoint"
        };
        if (!supported.Contains(type))
        {
            throw new ArgumentException($"Unsupported resource type '{resourceType}'. Supported types: {string.Join(", ", supported)}");
        }

        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"network_{subscription}_{resourceGroup}_{clusterName}_{type}"
            : $"network_{subscription}_{resourceGroup}_{clusterName}_{type}_{tenant}";
        var cached = await _cacheService.GetAsync<string>(CacheGroup, cacheKey, s_cacheDuration);
        if (!string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);

        // Resolve the AKS cluster
        var rg = await subscriptionResource.GetResourceGroupAsync(resourceGroup);
        if (rg?.Value is null)
        {
            throw new RequestFailedException(404, $"Resource group '{resourceGroup}' not found in subscription '{subscription}'.");
        }

        var clusterResponse = await rg.Value
            .GetContainerServiceManagedClusters()
            .GetAsync(clusterName);
        var clusterResource = clusterResponse.Value;
        if (clusterResource?.Data is null)
        {
            throw new RequestFailedException(404, $"AKS cluster '{clusterName}' not found in resource group '{resourceGroup}'.");
        }

        string resultJson;

        switch (type)
        {
            case "vnet":
                resultJson = await GetVNetJson(subscriptionResource, clusterResource);
                break;
            case "subnet":
                resultJson = await GetSubnetJson(subscriptionResource, clusterResource);
                break;
            case "nsg":
                resultJson = await GetNsgJson(subscriptionResource, clusterResource);
                break;
            case "route_table":
                resultJson = await GetRouteTableJson(subscriptionResource, clusterResource);
                break;
            case "load_balancer":
                resultJson = await GetLoadBalancersJson(subscriptionResource, clusterResource);
                break;
            case "private_endpoint":
                resultJson = await GetPrivateEndpointJson(subscriptionResource, clusterResource);
                break;
            case "all":
                resultJson = await GetAllNetworkResourcesJson(subscriptionResource, clusterResource);
                break;
            default:
                throw new ArgumentException($"Unsupported resource type '{resourceType}'.");
        }

        await _cacheService.SetAsync(CacheGroup, cacheKey, resultJson, s_cacheDuration);
        return resultJson;
    }

    private static (string? SubnetId, string? VnetId) GetSubnetAndVnetId(ContainerServiceManagedClusterResource cluster)
    {
        var data = cluster.Data;
        var agentPool = data.AgentPoolProfiles?.FirstOrDefault();
        var subnetId = agentPool?.VnetSubnetId?.ToString();
        if (string.IsNullOrEmpty(subnetId))
        {
            return (null, null);
        }
        var idx = subnetId.LastIndexOf("/subnets/", StringComparison.OrdinalIgnoreCase);
        var vnetId = idx > 0 ? subnetId.Substring(0, idx) : null;
        return (subnetId, vnetId);
    }

    private static (string ResourceGroup, string VnetName, string SubnetName) ParseSubnetId(string subnetId)
    {
        // Example: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{subnet}
        var parts = subnetId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? rg = null, vnet = null, subnet = null;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length) rg = parts[i + 1];
            if (parts[i].Equals("virtualNetworks", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length) vnet = parts[i + 1];
            if (parts[i].Equals("subnets", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length) subnet = parts[i + 1];
        }
        if (string.IsNullOrEmpty(rg) || string.IsNullOrEmpty(vnet) || string.IsNullOrEmpty(subnet))
            throw new ArgumentException("Invalid subnet resource ID");
        return (rg!, vnet!, subnet!);
    }

    private static (string ResourceGroup, string VnetName) ParseVnetId(string vnetId)
    {
        var parts = vnetId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? rg = null, name = null;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length) rg = parts[i + 1];
            if (parts[i].Equals("virtualNetworks", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length) name = parts[i + 1];
        }
        if (string.IsNullOrEmpty(rg) || string.IsNullOrEmpty(name))
            throw new ArgumentException("Invalid virtual network resource ID");
        return (rg!, name!);
    }

    private static (string ResourceGroup, string Name) ParseGenericId(string resourceId)
    {
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? rg = null;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
            {
                rg = parts[i + 1];
                break;
            }
        }
        var name = parts.Length > 0 ? parts[^1] : null;
        if (string.IsNullOrEmpty(rg) || string.IsNullOrEmpty(name))
            throw new ArgumentException("Invalid resource ID");
        return (rg!, name!);
    }

    [RequiresUnreferencedCode("Serializing arbitrary Azure SDK models may require reflection. Consumers should ensure required types are preserved.")]
    [RequiresDynamicCode("Serializing arbitrary Azure SDK models may require runtime code generation. Consider System.Text.Json source generation if used in AOT contexts.")]
    private static string Serialize(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });

    [RequiresUnreferencedCode("Serializing arbitrary Azure SDK models may require reflection. Consumers should ensure required types are preserved.")]
    [RequiresDynamicCode("Serializing arbitrary Azure SDK models may require runtime code generation. Consider System.Text.Json source generation if used in AOT contexts.")]
    private static string SerializeIndented(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });

    private static async Task<string> GetVNetJson(SubscriptionResource subscriptionResource, ContainerServiceManagedClusterResource cluster)
    {
        var (_, vnetId) = GetSubnetAndVnetId(cluster);
        if (string.IsNullOrEmpty(vnetId))
        {
            throw new RequestFailedException(404, "VNet ID not found from AKS cluster configuration.");
        }

        var (rgName, vnetName) = ParseVnetId(vnetId);
        var vnetRg = await subscriptionResource.GetResourceGroupAsync(rgName);
        if (vnetRg.Value == null)
        {
            throw new RequestFailedException(404, $"Resource group '{rgName}' for VNet not found.");
        }
        var vnet = await vnetRg.Value.GetVirtualNetworks().GetAsync(vnetName);
        return SerializeIndented(vnet.Value.Data);
    }

    private static async Task<string> GetSubnetJson(SubscriptionResource subscriptionResource, ContainerServiceManagedClusterResource cluster)
    {
        var (subnetId, _) = GetSubnetAndVnetId(cluster);
        if (string.IsNullOrEmpty(subnetId))
        {
            throw new RequestFailedException(404, "Subnet ID not found from AKS cluster configuration.");
        }

        var (rgName, vnetName, subnetName) = ParseSubnetId(subnetId);
        var vnetRg = await subscriptionResource.GetResourceGroupAsync(rgName);
        if (vnetRg.Value == null)
        {
            throw new RequestFailedException(404, $"Resource group '{rgName}' for subnet not found.");
        }
        var vnet = await vnetRg.Value.GetVirtualNetworks().GetAsync(vnetName);
        var subnet = await vnet.Value.GetSubnets().GetAsync(subnetName);
        return SerializeIndented(subnet.Value.Data);
    }

    private static async Task<string> GetNsgJson(SubscriptionResource subscriptionResource, ContainerServiceManagedClusterResource cluster)
    {
        var (subnetId, _) = GetSubnetAndVnetId(cluster);
        if (string.IsNullOrEmpty(subnetId))
        {
            throw new RequestFailedException(404, "Subnet ID not found from AKS cluster configuration.");
        }

        var (rgName, vnetName, subnetName) = ParseSubnetId(subnetId);
        var vnetRg = await subscriptionResource.GetResourceGroupAsync(rgName);
        if (vnetRg.Value == null)
        {
            throw new RequestFailedException(404, $"Resource group '{rgName}' for subnet not found.");
        }
        var vnet = await vnetRg.Value.GetVirtualNetworks().GetAsync(vnetName);
        var subnet = await vnet.Value.GetSubnets().GetAsync(subnetName);

        var nsgId = subnet.Value.Data.NetworkSecurityGroup?.Id?.ToString();
        if (string.IsNullOrEmpty(nsgId))
        {
            throw new RequestFailedException(404, "Network Security Group not associated with the AKS subnet.");
        }

        var (nsgRg, nsgName) = ParseGenericId(nsgId);
        var nsgRgRes = await subscriptionResource.GetResourceGroupAsync(nsgRg);
        if (nsgRgRes.Value == null)
        {
            throw new RequestFailedException(404, $"Resource group '{nsgRg}' for NSG not found.");
        }
        var nsg = await nsgRgRes.Value.GetNetworkSecurityGroups().GetAsync(nsgName);
        return SerializeIndented(nsg.Value.Data);
    }

    private static async Task<string> GetRouteTableJson(SubscriptionResource subscriptionResource, ContainerServiceManagedClusterResource cluster)
    {
        var (subnetId, _) = GetSubnetAndVnetId(cluster);
        if (string.IsNullOrEmpty(subnetId))
        {
            throw new RequestFailedException(404, "Subnet ID not found from AKS cluster configuration.");
        }

        var (rgName, vnetName, subnetName) = ParseSubnetId(subnetId);
        var vnetRg = await subscriptionResource.GetResourceGroupAsync(rgName);
        if (vnetRg.Value == null)
        {
            throw new RequestFailedException(404, $"Resource group '{rgName}' for subnet not found.");
        }
        var vnet = await vnetRg.Value.GetVirtualNetworks().GetAsync(vnetName);
        var subnet = await vnet.Value.GetSubnets().GetAsync(subnetName);

        var rtId = subnet.Value.Data.RouteTable?.Id?.ToString();
        if (string.IsNullOrEmpty(rtId))
        {
            var response = new
            {
                message = "No route table attached to the AKS cluster subnet",
                reason = "This is normal for AKS clusters using Azure CNI with Overlay mode or clusters that rely on Azure's default routing"
            };
            return SerializeIndented(response);
        }

        var (rtRg, rtName) = ParseGenericId(rtId);
        var rtRgRes = await subscriptionResource.GetResourceGroupAsync(rtRg);
        if (rtRgRes.Value == null)
        {
            throw new RequestFailedException(404, $"Resource group '{rtRg}' for Route Table not found.");
        }
        var rt = await rtRgRes.Value.GetRouteTables().GetAsync(rtName);
        return SerializeIndented(rt.Value.Data);
    }

    private static async Task<string> GetLoadBalancersJson(SubscriptionResource subscriptionResource, ContainerServiceManagedClusterResource cluster)
    {
        var nodeRgName = cluster.Data.NodeResourceGroup;
        if (string.IsNullOrEmpty(nodeRgName))
        {
            var response = new
            {
                message = "No node resource group found for this AKS cluster",
                reason = "The cluster may not be fully provisioned or is in an unexpected state."
            };
            return SerializeIndented(response);
        }

        var nodeRg = await subscriptionResource.GetResourceGroupAsync(nodeRgName);
        if (nodeRg.Value == null)
        {
            var response = new
            {
                message = $"Node resource group '{nodeRgName}' not found.",
                reason = "The cluster may have been deleted or moved."
            };
            return SerializeIndented(response);
        }

        var wantedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "kubernetes", "kubernetes-internal"
        };

        var selected = new List<LoadBalancerData>();
        await foreach (var lb in nodeRg.Value.GetLoadBalancers().GetAllAsync())
        {
            if (lb?.Data != null && wantedNames.Contains(lb.Data.Name))
            {
                selected.Add(lb.Data);
            }
        }

        if (selected.Count == 0)
        {
            var response = new
            {
                message = "No AKS load balancers (kubernetes/kubernetes-internal) found for this cluster",
                reason = "This cluster may not have standard AKS load balancers configured, or it may be using a different networking setup."
            };
            return SerializeIndented(response);
        }

        if (selected.Count == 1)
        {
            return SerializeIndented(selected[0]);
        }

        var result = new
        {
            count = selected.Count,
            load_balancers = selected
        };
        return SerializeIndented(result);
    }

    private static async Task<string> GetPrivateEndpointJson(SubscriptionResource subscriptionResource, ContainerServiceManagedClusterResource cluster)
    {
        var isPrivate = cluster.Data.ApiServerAccessProfile?.EnablePrivateCluster == true;
        if (!isPrivate)
        {
            var payload = new
            {
                message = "No private endpoint found. This AKS cluster is not configured as a private cluster.",
                private_cluster = false
            };
            return SerializeIndented(payload);
        }

        // Best-effort: list private endpoints in node resource group and return if found
        var nodeRgName = cluster.Data.NodeResourceGroup;
        if (string.IsNullOrEmpty(nodeRgName))
        {
            var response = new
            {
                message = "Cluster is private, but node resource group was not found to locate private endpoints.",
                private_cluster = true
            };
            return SerializeIndented(response);
        }

        var nodeRg = await subscriptionResource.GetResourceGroupAsync(nodeRgName);
        if (nodeRg.Value == null)
        {
            var response = new
            {
                message = $"Cluster is private, but node resource group '{nodeRgName}' was not found.",
                private_cluster = true
            };
            return SerializeIndented(response);
        }

        var endpoints = new List<PrivateEndpointData>();
        await foreach (var pe in nodeRg.Value.GetPrivateEndpoints().GetAllAsync())
        {
            if (pe?.Data != null)
            {
                endpoints.Add(pe.Data);
            }
        }

        if (endpoints.Count == 0)
        {
            var response = new
            {
                message = "No private endpoints found in the node resource group for this private AKS cluster.",
                private_cluster = true
            };
            return SerializeIndented(response);
        }

        if (endpoints.Count == 1)
        {
            return SerializeIndented(endpoints[0]);
        }

        var payload2 = new
        {
            count = endpoints.Count,
            private_endpoints = endpoints
        };
        return SerializeIndented(payload2);
    }

    private static async Task<string> GetAllNetworkResourcesJson(SubscriptionResource subscriptionResource, ContainerServiceManagedClusterResource cluster)
    {
        var result = new Dictionary<string, object?>();

        async Task AddAsync(string key, Func<Task<string>> func)
        {
            try
            {
                var json = await func();
                // Parse back to a JsonElement to avoid double-encoding without reflection
                using var doc = JsonDocument.Parse(json);
                result[key] = doc.RootElement.Clone();
            }
            catch (Exception ex)
            {
                result[$"{key}_error"] = new { message = ex.Message, type = ex.GetType().ToString() };
            }
        }

        await AddAsync("vnet", () => GetVNetJson(subscriptionResource, cluster));
        await AddAsync("nsg", () => GetNsgJson(subscriptionResource, cluster));
        await AddAsync("route_table", () => GetRouteTableJson(subscriptionResource, cluster));
        await AddAsync("subnet", () => GetSubnetJson(subscriptionResource, cluster));
        await AddAsync("load_balancer", () => GetLoadBalancersJson(subscriptionResource, cluster));
        await AddAsync("private_endpoint", () => GetPrivateEndpointJson(subscriptionResource, cluster));

        return SerializeIndented(result);
    }

    private static Cluster ConvertToClusterModel(ContainerServiceManagedClusterResource clusterResource)
    {
        var data = clusterResource.Data;
        var agentPool = data.AgentPoolProfiles?.FirstOrDefault();

        return new Cluster
        {
            Name = data.Name,
            SubscriptionId = clusterResource.Id.SubscriptionId,
            ResourceGroupName = clusterResource.Id.ResourceGroupName,
            Location = data.Location.ToString(),
            KubernetesVersion = data.KubernetesVersion,
            ProvisioningState = data.ProvisioningState?.ToString(),
            PowerState = data.PowerStateCode?.ToString(),
            DnsPrefix = data.DnsPrefix,
            Fqdn = data.Fqdn,
            NodeCount = agentPool?.Count,
            NodeVmSize = agentPool?.VmSize,
            IdentityType = data.Identity?.ManagedServiceIdentityType.ToString(),
            EnableRbac = data.EnableRbac,
            NetworkPlugin = data.NetworkProfile?.NetworkPlugin?.ToString(),
            NetworkPolicy = data.NetworkProfile?.NetworkPolicy?.ToString(),
            ServiceCidr = data.NetworkProfile?.ServiceCidr,
            DnsServiceIP = data.NetworkProfile?.DnsServiceIP?.ToString(),
            SkuTier = data.Sku?.Tier?.ToString(),
            Tags = data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    private static NodePool ConvertToNodePoolModel(ContainerServiceAgentPoolResource agentPoolResource)
    {
        var data = agentPoolResource.Data;

        return new NodePool
        {
            Name = data.Name,
            NodeCount = data.Count,
            NodeVmSize = data.VmSize?.ToString(),
            OsType = data.OSType?.ToString(),
            Mode = data.Mode?.ToString(),
            OrchestratorVersion = data.OrchestratorVersion,
            EnableAutoScaling = data.EnableAutoScaling,
            MinCount = data.MinCount,
            MaxCount = data.MaxCount,
            ProvisioningState = data.ProvisioningState?.ToString()
        };
    }
}
