namespace AzHST.Application.Models;

public static class SampleQueryCatalog
{
    private static readonly SampleQueryCategory ServiceExplanation = new(
        "service-explanation",
        "Service explanation",
        "Explain how an Azure service, cloud concept, request flow, or data flow works.");

    private static readonly SampleQueryCategory Comparison = new(
        "comparison",
        "Comparison",
        "Compare Azure services or architecture options, including trade-offs and suitable scenarios.");

    private static readonly SampleQueryCategory ArchitectureDesign = new(
        "architecture-design",
        "Architecture design",
        "Design a reference architecture, landing zone, integration, or hybrid connectivity solution.");

    private static readonly SampleQueryCategory Lifecycle = new(
        "lifecycle",
        "Lifecycle and operations",
        "Visualize deployment, scaling, failover, recovery, maintenance, or operational state transitions.");

    private static readonly SampleQueryCategory DecisionGuide = new(
        "decision-guide",
        "Decision guide",
        "Guide a choice of Azure service, SKU, topology, or design from concrete requirements.");

    private static readonly SampleQueryCategory ImplementationOverview = new(
        "implementation-overview",
        "Implementation overview",
        "Provide a phased implementation, migration, security, governance, or adoption plan.");

    public static IReadOnlyList<SampleQueryCategory> Categories { get; } =
    [
        ServiceExplanation,
        Comparison,
        ArchitectureDesign,
        Lifecycle,
        DecisionGuide,
        ImplementationOverview,
    ];

    public static IReadOnlyList<SampleQuery> SeedQueries { get; } =
    [
        Seed(
            ServiceExplanation,
            "Explain how Azure Functions processes an event from its trigger through bindings, execution, and automatic scaling."),
        Seed(
            ServiceExplanation,
            "How does Azure Application Gateway inspect and route a request through listeners, WAF policies, rules, and backend pools?"),
        Seed(
            ServiceExplanation,
            "Show how Azure Service Bus queues and topics deliver messages, retry failures, and move poison messages to dead-letter queues."),
        Seed(
            ServiceExplanation,
            "Explain how Azure Front Door routes global traffic using anycast, health probes, caching, WAF, and origin failover."),
        Seed(
            ServiceExplanation,
            "How does Azure Key Vault protect secrets, keys, and certificates when an application uses a managed identity?"),
        Seed(
            ServiceExplanation,
            "Explain how Azure Kubernetes Service schedules pods, exposes applications, performs health checks, and scales workloads."),
        Seed(
            ServiceExplanation,
            "How does Azure Cosmos DB partition data, distribute requests, replicate globally, and apply consistency levels?"),
        Seed(
            ServiceExplanation,
            "Show how Microsoft Entra ID authenticates a user or workload and issues tokens for an Azure application."),
        Seed(
            ServiceExplanation,
            "Explain the Azure API Management request pipeline from gateway policies through authentication, transformation, and backend routing."),
        Seed(
            ServiceExplanation,
            "How does Azure Monitor collect metrics, logs, and traces and turn them into dashboards, alerts, and automated actions?"),

        Seed(
            Comparison,
            "Compare Azure App Service and Azure Container Apps for hosting a production web API with variable traffic."),
        Seed(
            Comparison,
            "Compare Azure Functions and Azure Logic Apps for event-driven integration and business workflow automation."),
        Seed(
            Comparison,
            "Compare Azure Front Door and Azure Application Gateway for global and regional application delivery."),
        Seed(
            Comparison,
            "Compare Azure SQL Database and Azure Cosmos DB for a globally distributed transactional application."),
        Seed(
            Comparison,
            "Compare Azure Service Bus and Azure Event Grid for reliable messaging and reactive event distribution."),
        Seed(
            Comparison,
            "Compare Azure Kubernetes Service and Azure Container Apps for microservices that need autoscaling and private networking."),
        Seed(
            Comparison,
            "Compare Azure Blob Storage and Azure Data Lake Storage Gen2 for analytics, archival, and application data."),
        Seed(
            Comparison,
            "Compare site-to-site VPN and Azure ExpressRoute for connecting an enterprise datacenter to Azure."),
        Seed(
            Comparison,
            "Compare Azure Firewall and network security groups for controlling traffic in a hub-and-spoke network."),
        Seed(
            Comparison,
            "Compare availability zones and availability sets for improving virtual machine resilience."),

        Seed(
            ArchitectureDesign,
            "Design a secure active-active multi-region Azure architecture for a customer-facing web application."),
        Seed(
            ArchitectureDesign,
            "Design an enterprise Azure landing zone for multiple teams with centralized identity, networking, governance, and observability."),
        Seed(
            ArchitectureDesign,
            "Propose a secure architecture for connecting Azure API Management to services running in an on-premises datacenter."),
        Seed(
            ArchitectureDesign,
            "Design an event-driven e-commerce architecture on Azure that handles orders, payments, inventory, and notifications."),
        Seed(
            ArchitectureDesign,
            "Design a governed Azure analytics platform that ingests operational data and serves business intelligence workloads."),
        Seed(
            ArchitectureDesign,
            "Design a retrieval-augmented generation architecture on Azure with private data access, content filtering, and monitoring."),
        Seed(
            ArchitectureDesign,
            "Design a hub-and-spoke Azure network for shared security services and isolated application environments."),
        Seed(
            ArchitectureDesign,
            "Propose a multi-tenant SaaS architecture on Azure with tenant isolation, metering, and regional resilience."),
        Seed(
            ArchitectureDesign,
            "Design an Azure IoT architecture for securely ingesting, processing, and visualizing telemetry from remote devices."),
        Seed(
            ArchitectureDesign,
            "Design a zero-trust hybrid architecture for users accessing private applications across Azure and on-premises networks."),

        Seed(
            Lifecycle,
            "Visualize the failover and failback lifecycle for a business-critical Azure application during a regional outage."),
        Seed(
            Lifecycle,
            "Explain the lifecycle of an Azure Kubernetes Service rolling upgrade from node preparation through workload validation."),
        Seed(
            Lifecycle,
            "Show how a virtual machine scale set responds to increasing demand, failed instances, and a later scale-in event."),
        Seed(
            Lifecycle,
            "Visualize a secure secret and certificate rotation lifecycle using Azure Key Vault and managed identities."),
        Seed(
            Lifecycle,
            "Explain the incident response flow from Azure Monitor detection through alert routing, investigation, mitigation, and review."),
        Seed(
            Lifecycle,
            "Show the backup, restore, and validation lifecycle for an Azure SQL Database after accidental data deletion."),
        Seed(
            Lifecycle,
            "Visualize a blue-green deployment on Azure App Service, including testing, traffic switching, and rollback."),
        Seed(
            Lifecycle,
            "Explain the Azure Service Bus message lifecycle through delivery, lock renewal, retry, and dead-letter handling."),
        Seed(
            Lifecycle,
            "Show how Azure Cosmos DB handles regional failover, client redirection, recovery, and return to normal operation."),
        Seed(
            Lifecycle,
            "Visualize a controlled operating system patching lifecycle for Azure virtual machines across multiple availability zones."),

        Seed(
            DecisionGuide,
            "Which Azure compute service should host a new API: App Service, Container Apps, Functions, or AKS?"),
        Seed(
            DecisionGuide,
            "Choose between Azure SQL Database, Cosmos DB, and PostgreSQL for a globally available application with mixed data needs."),
        Seed(
            DecisionGuide,
            "When should an architecture use Azure Service Bus, Event Grid, or Event Hubs for asynchronous communication?"),
        Seed(
            DecisionGuide,
            "Choose a global traffic entry service for a secure multi-region application using Front Door, Traffic Manager, or Application Gateway."),
        Seed(
            DecisionGuide,
            "Select between VPN Gateway, ExpressRoute, and Virtual WAN for a company connecting many offices to Azure."),
        Seed(
            DecisionGuide,
            "Which Azure identity option should an application use: managed identity, service principal, or workload identity federation?"),
        Seed(
            DecisionGuide,
            "Choose an Azure monitoring design for correlating application traces, platform metrics, logs, and security events."),
        Seed(
            DecisionGuide,
            "Select the right Azure Storage access tier and redundancy option for frequently accessed, backup, and archival data."),
        Seed(
            DecisionGuide,
            "Choose an Azure disaster recovery strategy that balances recovery objectives, operational complexity, and cost."),
        Seed(
            DecisionGuide,
            "Which Azure data integration service best fits batch pipelines, streaming ingestion, and low-code transformations?"),

        Seed(
            ImplementationOverview,
            "Create a phased implementation plan for an enterprise Azure landing zone with identity, networking, policy, and management groups."),
        Seed(
            ImplementationOverview,
            "Outline a migration plan for moving an on-premises web application and SQL database to Azure with minimal downtime."),
        Seed(
            ImplementationOverview,
            "Provide an implementation overview for placing Azure API Management in front of private application services."),
        Seed(
            ImplementationOverview,
            "Plan the implementation of a secure hub-and-spoke Azure network with centralized inspection and DNS."),
        Seed(
            ImplementationOverview,
            "Outline the steps for establishing a production-ready Azure Kubernetes Service platform for multiple application teams."),
        Seed(
            ImplementationOverview,
            "Create an implementation plan for cross-region business continuity and disaster recovery for an Azure application."),
        Seed(
            ImplementationOverview,
            "Plan a secure CI/CD workflow for deploying infrastructure and applications to Azure across development and production environments."),
        Seed(
            ImplementationOverview,
            "Outline a zero-trust implementation plan using Microsoft Entra ID, Conditional Access, private endpoints, and Defender for Cloud."),
        Seed(
            ImplementationOverview,
            "Create a phased observability implementation plan using Azure Monitor, Application Insights, Log Analytics, and alerts."),
        Seed(
            ImplementationOverview,
            "Plan the introduction of Azure cost governance using budgets, tagging, policy, reservations, and recurring optimization reviews."),
    ];

    private static SampleQuery Seed(
        SampleQueryCategory category,
        string query)
    {
        return new SampleQuery(category.Id, category.DisplayName, query);
    }
}
