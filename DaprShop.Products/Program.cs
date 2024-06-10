using Dapr;
using Dapr.Client;

using DaprShop.Contracts;
using DaprShop.Products;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<ProductsDbContext>("sqldb");

//builder.Services.AddDbContext<ProductsDbContext>(opt =>
//{
//    opt.UseSqlServer(builder.Configuration.GetConnectionString("ProductsDb"));
//});

builder.Services.AddDaprClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();

//if (app.Environment.IsDevelopment())
{
    // NOT BEST PRACTICE - Retrieve an instance of the DbContext class and manually run migrations during startup
    using IServiceScope scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
    context.Database.Migrate();
}

app.UseCloudEvents();
app.MapSubscribeHandler();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapGet("/product", async Task<Results<Ok<ProductDto>, NotFound>>([FromQuery] string name, [FromServices] ProductsDbContext ctx) =>
{
    var product = await ctx.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Name == name);

    if (product is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Ok(new ProductDto(product.Name, product.Price));
})
.WithName("GetProducts")
.WithOpenApi();

app.MapGet("/products", async ([FromServices] ProductsDbContext ctx) =>
{
    var productList = await ctx.Products
        .AsNoTracking()
        .Where(p => p.Price >= 0)
        .OrderByDescending(p => p.Price)
        .Select(p => new ProductDto(p.Name, p.Price))
        .ToListAsync();

    return TypedResults.Ok(productList);
})
.WithName("GetAllProducts")
.WithOpenApi();

app.MapPost("/products", async Task<Results<Ok, BadRequest<string>>> (ProductDto p, [FromServices] ProductsDbContext ctx) =>
{
    try
    {
        var newProduct = new Product()
        {
            Name = p.Name,
            Price = p.UnitPrice
        };

        var result = await ctx.Products.AddAsync(newProduct);

        var stateCount = await ctx.SaveChangesAsync();

        app.Logger.LogInformation("Added new [{stateCount}] product [{productName}] with id: [{resultId}/{productId}]", stateCount, result.Entity.Name, result.Entity.Id, newProduct.Id);

        return TypedResults.Ok();
    }
    catch (SqlException sx)
    {
        app.Logger.LogError(sx.Message, "Couldn't add new product to catalogue");
        throw;
    }
})
.WithName("AddProductSql")
.WithOpenApi();


app.MapPost("/clear", async ([FromServices] ProductsDbContext ctx) =>
{
    // clear the catalogue
    await ctx.Products.ExecuteDeleteAsync();

    return TypedResults.Ok();
})
.WithName("Clear")
.WithOpenApi();

app.Run();
