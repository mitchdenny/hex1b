namespace CloudTermDemo;

/// <summary>
/// A node in the fake cloud resource hierarchy.
/// </summary>
public sealed class CloudNode
{
    public string Name { get; }
    public string Type { get; }
    public string? Description { get; init; }
    public List<CloudNode> Children { get; } = [];
    public CloudNode? Parent { get; private set; }

    public CloudNode(string name, string type)
    {
        Name = name;
        Type = type;
    }

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
        var root = new CloudNode("me@contoso.com", "User");

        // Tenant: Contoso Corp
        var contoso = new CloudNode("Contoso Corp", "Tenant");
        root.AddChild(contoso);

        var prodSub = new CloudNode("Production", "Subscription") { Description = "ID: a1b2c3d4-..." };
        contoso.AddChild(prodSub);

        var rgWebProd = new CloudNode("rg-web-prod", "Resource Group");
        prodSub.AddChild(rgWebProd);
        rgWebProd.AddChild(new CloudNode("app-frontend", "App Service") { Description = "Linux, .NET 10" });
        rgWebProd.AddChild(new CloudNode("app-api", "App Service") { Description = "Linux, .NET 10" });

        var aks = new CloudNode("aks-prod-eastus", "AKS Cluster") { Description = "1.30, 3 nodes" };
        rgWebProd.AddChild(aks);
        aks.AddChild(new CloudNode("default", "Namespace"));
        aks.AddChild(new CloudNode("ingress-nginx", "Namespace"));
        aks.AddChild(new CloudNode("monitoring", "Namespace"));

        rgWebProd.AddChild(new CloudNode("sql-prod-eastus", "SQL Server") { Description = "Gen5, 4 vCores" });
        rgWebProd.AddChild(new CloudNode("st-webprod", "Storage Account") { Description = "StorageV2, LRS" });

        var rgDataProd = new CloudNode("rg-data-prod", "Resource Group");
        prodSub.AddChild(rgDataProd);
        rgDataProd.AddChild(new CloudNode("cosmos-analytics", "Cosmos DB") { Description = "Serverless" });
        rgDataProd.AddChild(new CloudNode("ehub-events", "Event Hub") { Description = "Standard, 4 TU" });
        rgDataProd.AddChild(new CloudNode("st-datalake", "Storage Account") { Description = "ADLS Gen2, LRS" });

        var devSub = new CloudNode("Dev/Test", "Subscription") { Description = "ID: e5f6g7h8-..." };
        contoso.AddChild(devSub);

        var rgDevTest = new CloudNode("rg-dev", "Resource Group");
        devSub.AddChild(rgDevTest);
        rgDevTest.AddChild(new CloudNode("app-dev-api", "App Service") { Description = "Linux, .NET 10" });
        rgDevTest.AddChild(new CloudNode("aks-dev", "AKS Cluster") { Description = "1.30, 1 node" });
        rgDevTest.AddChild(new CloudNode("sql-dev", "SQL Server") { Description = "Basic" });

        // Tenant: Personal
        var personal = new CloudNode("Personal", "Tenant");
        root.AddChild(personal);

        var personalSub = new CloudNode("Pay-As-You-Go", "Subscription") { Description = "ID: z9y8x7w6-..." };
        personal.AddChild(personalSub);

        var rgSideProject = new CloudNode("rg-side-project", "Resource Group");
        personalSub.AddChild(rgSideProject);
        rgSideProject.AddChild(new CloudNode("app-blog", "App Service") { Description = "Linux, Node 22" });
        rgSideProject.AddChild(new CloudNode("st-blog", "Storage Account") { Description = "StorageV2, LRS" });

        return root;
    }
}
