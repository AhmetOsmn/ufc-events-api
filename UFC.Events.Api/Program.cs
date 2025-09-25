using StackExchange.Redis;
using UFC.Events.Api.Models;
using UFC.Events.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis Configuration
var redisConfig = builder.Configuration.GetSection("Redis").Get<RedisConfiguration>() 
    ?? new RedisConfiguration();

// Redis Connection
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = ConfigurationOptions.Parse(redisConfig.ConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;
    return ConnectionMultiplexer.Connect(configuration);
});

// HttpClient for web scraping
builder.Services.AddHttpClient();

// Redis Cache Manager
builder.Services.AddScoped<IRedisCacheManager, RedisCacheManager>();

// UFC Scraper Service
builder.Services.AddScoped<IUfcScraperService, UfcScraperService>();

// Event Service
builder.Services.AddScoped<IEventService, EventService>();

// Configure Redis Configuration as singleton
builder.Services.AddSingleton(redisConfig);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

// UFC Events endpoint
app.MapGet("/events", async (IEventService eventService) =>
{
    var events = await eventService.GetAllEventsAsync();
    return Results.Ok(events);
})
.WithName("GetEvents")
.Produces<List<Event>>();

// Manual refresh endpoint
app.MapPost("/events/refresh", async (IEventService eventService) =>
{
    try
    {
        await eventService.LoadLatestEventsAsync();
        var events = await eventService.GetAllEventsAsync();
        return Results.Ok(new { message = "Events refreshed successfully", count = events.Count });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error refreshing events: {ex.Message}");
    }
})
.WithName("RefreshEvents")
.Produces<object>();

// Load fresh UFC events data on startup
using (var scope = app.Services.CreateScope())
{
    var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Uygulama başlatılırken UFC event data yükleniyor...");
        await eventService.LoadLatestEventsAsync();
        logger.LogInformation("UFC event data başarıyla yüklendi");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "UFC event data yüklenirken hata oluştu, uygulama yine de başlatılacak");
    }
}

app.Run();

