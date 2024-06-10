using Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// Add DAPR
var dapr = builder.AddDapr();

// ADD Custom Dapr Component that reads from a DAPR Component YAML file
var stateStore = builder.AddDaprStateStore("statestore", new DaprComponentOptions()
{
    LocalPath = "../dapr/cosmos-statestore.yaml"
});

var pubSub = builder.AddDaprPubSub("pubsub");



// when persisting data with volume mounts, remember to add a persistent user-secret in the AppHost project folder for the sql-password:
// ```
// dotnet user-secrets set Parameters:sql-password "<password>"
// ```
// see: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/persist-data-volumes#create-a-persistent-password

var sql = builder.AddSqlServer("sql", port: 50664)
    // add volume mount to persist data across multiple restarts
    // see: https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/persist-data-volumes
    //.WithBindMount("products-sql", "/var/opt/mssql");
    .WithDataVolume()
    .PublishAsAzureSqlDatabase();

var sqldb = sql.AddDatabase("sqldb");


// Add Dapr Sidecars to the services and reference the required Dapr Components (scoping in DAPR-speak)
var productsService = builder.AddProject<Projects.DaprShop_Products>("daprshop-products")
    .WithExternalHttpEndpoints()
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "products",
        AppProtocol = builder.ExecutionContext.IsPublishMode ? null : "https"
    })
    .WithReference(stateStore)
    .WithReference(pubSub)
    .WithReference(sqldb);



var cartService = builder.AddProject<Projects.DaprShop_ShoppingCart>("daprshop-cart")
    .WithExternalHttpEndpoints()
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "cart",
        AppProtocol = builder.ExecutionContext.IsPublishMode ? null : "https"
    })
    ///.WithReference(productsService) // so that it can make service-to-service calls to the products service
    .WithReference(stateStore)
    .WithReference(pubSub);

var ordersService = builder.AddProject<Projects.DaprShop_Orders>("daprshop-orders")
    .WithExternalHttpEndpoints()
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "orders",
        AppProtocol = builder.ExecutionContext.IsPublishMode ? null : "https"
    })
    .WithReference(stateStore)
    .WithReference(pubSub);

// Executable to Host Dapr Dashboard - because we can
var daprDashboard = builder.AddExecutable("dapr-dashboard", "dapr", ".", "dashboard")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "dapr-dashboard-http", isProxied: false)
    .ExcludeFromManifest();

builder.Build().Run();