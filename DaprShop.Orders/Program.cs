using Dapr;
using Dapr.Client;

using DaprShop.Contracts;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDaprClient();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseCloudEvents();
app.MapSubscribeHandler();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();





app.MapGet("/orders", async ([FromServices]DaprClient dapr) =>
{
    try
    {
        // get all the submitted currentOrders
        var currentOrders = await dapr.GetStateAsync<OrderDto>("statestore", "currentOrders");
        return TypedResults.Ok(currentOrders);
    }
    catch (DaprException dx)
    {
        app.Logger.LogError(dx, "Attempting to retrieve Cart");
        throw;
    }
})
.WithName("GetOrders")
.WithOpenApi();

app.MapPost("/orders/watch",
    [Topic("pubsub", "new.cart.item")]
    ([FromBody] string item, CancellationToken ct) =>
    {
        app.Logger.LogInformation("Someone added an item to their cart: {item}", item);
        return Results.Ok();
    })
    .WithName("Watch");

app.MapPost("/orders/submit",
    [Topic("pubsub", "new.orders")] // Listen for new orders
    async Task<Results<Ok, BadRequest<string>>> ([FromBody] OrderDto newOrder, [FromServices]DaprClient dapr) =>
    {
        if(newOrder.Items is null || newOrder.Items.Length < 1)
        {
            return TypedResults.BadRequest("OrderDto must contain items!");
        }

        app.Logger.LogInformation($"New Order received with items: {string.Join(", ", newOrder.Items)}");

        // Add the new newOrder to the list of currentOrders (that will be processed by another service... 
        OrderDto currentOrders = await dapr.GetStateAsync<OrderDto>("statestore", "currentOrders");

        if (currentOrders is null || currentOrders.Items is null)
        {
            app.Logger.LogInformation("Adding newOrder to new newOrder list...");

            currentOrders = new OrderDto(newOrder.Items);
        }
        else
        {
            app.Logger.LogInformation("Adding newOrder to newOrder list...");

            currentOrders = currentOrders with
            {
                Items = [.. currentOrders.Items, .. newOrder.Items]
            };
        }

        await dapr.SaveStateAsync<OrderDto>("statestore", "currentOrders", currentOrders);

        return TypedResults.Ok();
    })
    .WithName("ReceiveOrder");

app.MapPost("/clear", async ([FromServices] DaprClient dapr) =>
{
    // clear the current orders
    await dapr.DeleteStateAsync("statestore", "currentOrders");

    return TypedResults.Ok();
})
.WithName("Clear")
.WithOpenApi();

app.Run();

