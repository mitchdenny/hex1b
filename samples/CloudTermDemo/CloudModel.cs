namespace CloudTermDemo;

/// <summary>
/// Identifies the kind of cloud resource for command dispatch.
/// </summary>
public enum CloudNodeKind
{
    User,
    Tenant,
    Subscription,
    ResourceGroup,
    AppService,
    AksCluster,
    Namespace,
    Pod,
    SqlServer,
    StorageAccount,
    CosmosDb,
    EventHub,
}

/// <summary>
/// A node in the fake cloud resource hierarchy.
/// </summary>
public sealed class CloudNode
{
    public string Name { get; }
    public CloudNodeKind Kind { get; }
    public string? Description { get; init; }
    public List<CloudNode> Children { get; } = [];
    public CloudNode? Parent { get; private set; }

    public CloudNode(string name, CloudNodeKind kind)
    {
        Name = name;
        Kind = kind;
    }

    /// <summary>Display-friendly type name.</summary>
    public string TypeLabel => Kind switch
    {
        CloudNodeKind.User => "User",
        CloudNodeKind.Tenant => "Tenant",
        CloudNodeKind.Subscription => "Subscription",
        CloudNodeKind.ResourceGroup => "Resource Group",
        CloudNodeKind.AppService => "App Service",
        CloudNodeKind.AksCluster => "AKS Cluster",
        CloudNodeKind.Namespace => "Namespace",
        CloudNodeKind.Pod => "Pod",
        CloudNodeKind.SqlServer => "SQL Server",
        CloudNodeKind.StorageAccount => "Storage Account",
        CloudNodeKind.CosmosDb => "Cosmos DB",
        CloudNodeKind.EventHub => "Event Hub",
        _ => Kind.ToString(),
    };

    public CloudNode AddChild(CloudNode child)
    {
        child.Parent = this;
        Children.Add(child);
        return this;
    }
}

/// <summary>
/// Fake cloud resource hierarchy with realistic Azure-like names.
/// </summary>
public static class CloudModel
{
    public static CloudNode BuildHierarchy()
    {
        var root = new CloudNode("me@contoso.com", CloudNodeKind.User);

        // Tenant: Contoso Corp
        var contoso = new CloudNode("Contoso Corp", CloudNodeKind.Tenant);
        root.AddChild(contoso);

        var prodSub = new CloudNode("Production", CloudNodeKind.Subscription) { Description = "ID: a1b2c3d4-..." };
        contoso.AddChild(prodSub);

        var rgWebProd = new CloudNode("rg-web-prod", CloudNodeKind.ResourceGroup);
        prodSub.AddChild(rgWebProd);
        rgWebProd.AddChild(new CloudNode("app-frontend", CloudNodeKind.AppService) { Description = "Linux, .NET 10" });
        rgWebProd.AddChild(new CloudNode("app-api", CloudNodeKind.AppService) { Description = "Linux, .NET 10" });

        var aks = new CloudNode("aks-prod-eastus", CloudNodeKind.AksCluster) { Description = "1.30, 3 nodes" };
        rgWebProd.AddChild(aks);

        var nsDefault = new CloudNode("default", CloudNodeKind.Namespace);
        aks.AddChild(nsDefault);
        nsDefault.AddChild(new CloudNode("api-gateway-7f8d9c", CloudNodeKind.Pod) { Description = "Running, 1/1" });
        nsDefault.AddChild(new CloudNode("web-frontend-4a2b1e", CloudNodeKind.Pod) { Description = "Running, 1/1" });
        nsDefault.AddChild(new CloudNode("worker-processor-9c3d5f", CloudNodeKind.Pod) { Description = "Running, 2/2" });

        var nsIngress = new CloudNode("ingress-nginx", CloudNodeKind.Namespace);
        aks.AddChild(nsIngress);
        nsIngress.AddChild(new CloudNode("ingress-controller-6b7a3c", CloudNodeKind.Pod) { Description = "Running, 1/1" });

        var nsMonitoring = new CloudNode("monitoring", CloudNodeKind.Namespace);
        aks.AddChild(nsMonitoring);
        nsMonitoring.AddChild(new CloudNode("prometheus-server-2d4e6f", CloudNodeKind.Pod) { Description = "Running, 1/1" });
        nsMonitoring.AddChild(new CloudNode("grafana-8a1b3c", CloudNodeKind.Pod) { Description = "Running, 1/1" });

        rgWebProd.AddChild(new CloudNode("sql-prod-eastus", CloudNodeKind.SqlServer) { Description = "Gen5, 4 vCores" });
        rgWebProd.AddChild(new CloudNode("st-webprod", CloudNodeKind.StorageAccount) { Description = "StorageV2, LRS" });

        var rgDataProd = new CloudNode("rg-data-prod", CloudNodeKind.ResourceGroup);
        prodSub.AddChild(rgDataProd);
        rgDataProd.AddChild(new CloudNode("cosmos-analytics", CloudNodeKind.CosmosDb) { Description = "Serverless" });
        rgDataProd.AddChild(new CloudNode("ehub-events", CloudNodeKind.EventHub) { Description = "Standard, 4 TU" });
        rgDataProd.AddChild(new CloudNode("st-datalake", CloudNodeKind.StorageAccount) { Description = "ADLS Gen2, LRS" });

        var devSub = new CloudNode("Dev/Test", CloudNodeKind.Subscription) { Description = "ID: e5f6g7h8-..." };
        contoso.AddChild(devSub);

        var rgDevTest = new CloudNode("rg-dev", CloudNodeKind.ResourceGroup);
        devSub.AddChild(rgDevTest);
        rgDevTest.AddChild(new CloudNode("app-dev-api", CloudNodeKind.AppService) { Description = "Linux, .NET 10" });
        rgDevTest.AddChild(new CloudNode("aks-dev", CloudNodeKind.AksCluster) { Description = "1.30, 1 node" });
        rgDevTest.AddChild(new CloudNode("sql-dev", CloudNodeKind.SqlServer) { Description = "Basic" });

        // Tenant: Personal
        var personal = new CloudNode("Personal", CloudNodeKind.Tenant);
        root.AddChild(personal);

        var personalSub = new CloudNode("Pay-As-You-Go", CloudNodeKind.Subscription) { Description = "ID: z9y8x7w6-..." };
        personal.AddChild(personalSub);

        var rgSideProject = new CloudNode("rg-side-project", CloudNodeKind.ResourceGroup);
        personalSub.AddChild(rgSideProject);
        rgSideProject.AddChild(new CloudNode("app-blog", CloudNodeKind.AppService) { Description = "Linux, Node 22" });
        rgSideProject.AddChild(new CloudNode("st-blog", CloudNodeKind.StorageAccount) { Description = "StorageV2, LRS" });

        return root;
    }
}
