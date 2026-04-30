namespace CloudTermDemo;

/// <summary>
/// Provides mock YAML content for Kubernetes resources.
/// </summary>
internal static class MockYaml
{
    public static string ForPod(string name, string ns) => $"""
        apiVersion: v1
        kind: Pod
        metadata:
          name: {name}
          namespace: {ns}
          labels:
            app: {name.Split('-')[0]}
            version: v1
        spec:
          containers:
          - name: app
            image: mcr.microsoft.com/dotnet/aspnet:10.0
            ports:
            - containerPort: 8080
              protocol: TCP
            resources:
              requests:
                cpu: 100m
                memory: 128Mi
              limits:
                cpu: 500m
                memory: 512Mi
            env:
            - name: ASPNETCORE_URLS
              value: "http://+:8080"
            - name: DOTNET_ENVIRONMENT
              value: Production
            livenessProbe:
              httpGet:
                path: /health
                port: 8080
              initialDelaySeconds: 10
              periodSeconds: 30
            readinessProbe:
              httpGet:
                path: /ready
                port: 8080
              initialDelaySeconds: 5
              periodSeconds: 10
          restartPolicy: Always
        """;

    public static string ForNamespace(string name) => $$"""
        apiVersion: v1
        kind: Namespace
        metadata:
          name: {{name}}
          labels:
            environment: production
            managed-by: cloud-term
          annotations:
            description: "Managed namespace"
        spec:
          finalizers:
          - kubernetes
        ---
        apiVersion: v1
        kind: ResourceQuota
        metadata:
          name: default-quota
          namespace: {{name}}
        spec:
          hard:
            pods: "50"
            requests.cpu: "4"
            requests.memory: 8Gi
            limits.cpu: "8"
            limits.memory: 16Gi
        ---
        apiVersion: networking.k8s.io/v1
        kind: NetworkPolicy
        metadata:
          name: default-deny
          namespace: {{name}}
        spec:
          podSelector: {}
          policyTypes:
          - Ingress
          - Egress
        """;

    public static string ForAksCluster(string name) => $"""
        apiVersion: containerservice.azure.com/v1
        kind: ManagedCluster
        metadata:
          name: {name}
          location: eastus
        spec:
          kubernetesVersion: "1.30"
          dnsPrefix: {name}
          agentPoolProfiles:
          - name: nodepool1
            count: 3
            vmSize: Standard_D4s_v3
            osType: Linux
            mode: System
            maxPods: 110
            enableAutoScaling: true
            minCount: 1
            maxCount: 5
          networkProfile:
            networkPlugin: azure
            networkPolicy: calico
            serviceCidr: 10.0.0.0/16
            dnsServiceIP: 10.0.0.10
          identity:
            type: SystemAssigned
          addonProfiles:
            omsagent:
              enabled: true
            azurePolicy:
              enabled: true
        """;

    public static string ForAppService(string name) => $"""
        # App Service Configuration
        # {name}

        runtime: dotnet|10.0
        os: linux
        sku: P1v3

        settings:
          ASPNETCORE_ENVIRONMENT: Production
          WEBSITE_RUN_FROM_PACKAGE: "1"
          DOTNET_CLI_TELEMETRY_OPTOUT: "1"

        connectionStrings:
          DefaultConnection:
            type: SQLAzure
            value: "Server=tcp:sql-prod-eastus.database.windows.net;Database=app;"

        slots:
        - name: staging
          settings:
            ASPNETCORE_ENVIRONMENT: Staging
        """;
}
