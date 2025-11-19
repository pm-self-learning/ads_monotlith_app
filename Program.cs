using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Services;
using RetailMonolith.Models;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.Extensions.Options;
using Azure.AI.OpenAI;
using System.Text.Json;
using System.Linq;


var builder = WebApplication.CreateBuilder(args);

// Bind Azure OpenAI settings
builder.Services.Configure<AzureOpenAISettings>(builder.Configuration.GetSection("AzureOpenAI"));

// Register ChatClient (Azure OpenAI) using configured settings
builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<IOptions<AzureOpenAISettings>>().Value;
    if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.DeploymentName))
    {
        throw new InvalidOperationException("AzureOpenAI settings are not fully configured. Please ensure Endpoint, ApiKey, and DeploymentName are set.");
    }
    var azureClient = new AzureOpenAIClient(new Uri(settings.Endpoint), new ApiKeyCredential(settings.ApiKey));
    return azureClient.GetChatClient(settings.DeploymentName);
});

// DB � localdb for hack; swap to SQL in appsettings for Azure
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
                   "Server=(localdb)\\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true"));


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // Add this for API controllers
builder.Services.AddScoped<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IAIRecommendationService, AIRecommendationService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// auto-migrate & seed (hack convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await AppDbContext.SeedAsync(db); // seed the database
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers(); // Add this for API controllers


// minimal APIs for the �decomp� path
app.MapPost("/api/checkout", async (ICheckoutService svc) =>
{
    var order = await svc.CheckoutAsync("guest", "tok_test");
    return Results.Ok(new { order.Id, order.Status, order.Total });
});

app.MapGet("/api/orders/{id:int}", async (int id, AppDbContext db) =>
{
    var order = await db.Orders.Include(o => o.Lines)
        .SingleOrDefaultAsync(o => o.Id == id);

    return order is null ? Results.NotFound() : Results.Ok(order);
});

// Endpoint: send user query + full product catalogue to Azure OpenAI chat completion
app.MapPost("/api/chat/products", async (RetailMonolith.Models.ChatProductRequest req, AppDbContext db, ChatClient client, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.message))
        return Results.BadRequest(new { error = "Empty message" });

    // Pull active products (minimal fields to reduce token usage)
    var products = await db.Products
        .Where(p => p.IsActive)
        .Select(p => new { p.Id, p.Sku, p.Name, p.Category, p.Price, p.Currency })
        .ToListAsync(ct);

    // Serialize catalogue - consider truncation/pagination for large sets
    var productsJson = JsonSerializer.Serialize(products);

    var messages = new List<OpenAI.Chat.ChatMessage>
    {
        OpenAI.Chat.ChatMessage.CreateSystemMessage("You are a retail assistant. Use the provided JSON product catalogue if helpful. Catalogue:" + productsJson),
        OpenAI.Chat.ChatMessage.CreateUserMessage(req.message)
    };

    var completion = await client.CompleteChatAsync(messages, new ChatCompletionOptions
    {
        Temperature = 0.3f
    }, ct);

    var reply = completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;

    // Slim raw object for easier client display
    var raw = new
    {
        id = completion.Value.Id,
        model = completion.Value.Model,
        finishReason = completion.Value.FinishReason,
        usage = completion.Value.Usage,
        content = completion.Value.Content.Select(c => c.Text).ToArray()
    };

    return Results.Ok(new { reply, products, raw });
});


app.Run();
