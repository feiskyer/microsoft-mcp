AKS: Cluster Get Network Resource

Command
- Tool: `azmcp_aks_cluster_get-network-resource`
- Description: Get Azure network resource information used by an AKS cluster. Supports querying all resources or a specific resource type.

Options
- `--subscription` (required): Azure subscription ID.
- `--resource-group` (required): Resource group of the AKS cluster.
- `--cluster` (required): AKS cluster name.
- `--resource-type` (required): One of `all`, `vnet`, `nsg`, `route_table`, `subnet`, `load_balancer`, `private_endpoint`.
- `--tenant` (optional): AAD tenant ID or domain.
- Retry policy options (optional): Standard AZ MCP retry settings.

Output
- On success, returns an object with fields:
  - `resourceType`: Echoes the requested resource-type.
  - `jsonPayload`: A JSON string containing the raw ARM resource(s) serialized as JSON.
    - For `all`, `jsonPayload` contains a JSON object with keys: `vnet`, `nsg`, `route_table`, `subnet`, `load_balancer`, `private_endpoint`. On per-resource failure, a `*_error` object is included with `message` and `type`.

Examples
- Get all resources for a cluster:
  azmcp_aks_cluster_get-network-resource --subscription <sub> --resource-group <rg> --cluster <name> --resource-type all
- Get VNet only:
  azmcp_aks_cluster_get-network-resource --subscription <sub> --resource-group <rg> --cluster <name> --resource-type vnet
- Get Load Balancers:
  azmcp_aks_cluster_get-network-resource --subscription <sub> --resource-group <rg> --cluster <name> --resource-type load_balancer

Notes
- The command returns raw ARM resource JSON (as a string) for maximum fidelity and parity with the Go tool.
- Load balancers are filtered to `kubernetes` and `kubernetes-internal` in the node resource group when present.
- For private clusters, if no private endpoints are discovered, a message is returned.

