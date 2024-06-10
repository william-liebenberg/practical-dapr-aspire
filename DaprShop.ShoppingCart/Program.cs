using Dapr;
using Dapr.Client;

using DaprShop.Contracts;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDaprClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddKeyedScoped("products-api", (sp, _) => DaprClient.CreateInvokeHttpClient("products"));

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseCloudEvents();
app.MapSubscribeHandler();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();



app.MapGet("/getCart", async Task<Ok<CartDto>> (DaprClient dapr) =>
{
    try
    {
        var cart = await dapr.GetStateAsync<CartDto>("statestore", "cart");
        return TypedResults.Ok(cart);
    }
    catch (DaprException dx)
    {
        app.Logger.LogError(dx, "Attempting to retrieve Cart");
        throw;
    }
})
.WithName("GetCart")
.WithOpenApi();


app.MapPost("/addToCart", 
    async Task<Results<Ok, NotFound<string>>> 
    ([FromBody] string item, DaprClient dapr, [FromKeyedServices("products-api")] HttpClient productsHttpClient) =>
{
    try
    {
        // make sure product exists
        var checkProduct = await productsHttpClient.GetFromJsonAsync<ProductDto>($"product?name={item}");
        if(checkProduct is null)
        {
            return TypedResults.NotFound($"The product [{item}] does not exist!");
        }

        var cart = await dapr.GetStateAsync<CartDto>("statestore", "cart");
        if (cart is null || cart.Items is null)
        {
            app.Logger.LogInformation("Adding item to new cart: {item}", item);

            cart = new CartDto([item]);
        }
        else
        {
            app.Logger.LogInformation("Adding item to cart: {item}", item);

            cart = cart with
            {
                Items = [.. cart.Items, item]
            };
        }

        // save the item to the cart
        await dapr.SaveStateAsync("statestore", "cart", cart);

        // let everyone know we added an item to the cart
        await dapr.PublishEventAsync("pubsub", "new.cart.item", item);

        return TypedResults.Ok();
    }
    catch (DaprException dx)
    {
        app.Logger.LogError(dx, "Couldn't add item [{item}] to the cart", item);
        throw;
    }
})
.WithName("AddToCart")
.WithOpenApi();

app.MapPost("/checkout", async Task<Results<Ok, BadRequest<string>>> ([FromServices] DaprClient dapr) =>
{
    var cart = await dapr.GetStateAsync<CartDto>("statestore", "cart");
    if(cart is null || cart.Items is null || cart.Items.Length < 1)
    {
        return TypedResults.BadRequest("Cart is empty! -- add some items before checking out");
    }

    app.Logger.LogInformation("Checking out {count} items", cart.Items.Length);

    OrderDto order = new([..cart.Items]);

    // let everyone know we have a submitted an order
    await dapr.PublishEventAsync("pubsub", "new.orders", order);

    // clear the cart
    await dapr.DeleteStateAsync("statestore", "cart");

    return TypedResults.Ok();
})
.WithName("Checkout")
.WithOpenApi();

app.MapPost("/clear", async ([FromServices] DaprClient dapr) =>
{
    // clear the cart
    await dapr.DeleteStateAsync("statestore", "cart");

    return TypedResults.Ok();
})
.WithName("Clear")
.WithOpenApi();

app.Run();
