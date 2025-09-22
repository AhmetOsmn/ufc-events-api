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

// Redis Cache Manager
builder.Services.AddScoped<IRedisCacheManager, RedisCacheManager>();

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

// Mock data seed
using (var scope = app.Services.CreateScope())
{
    var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
    await eventService.SeedMockDataAsync();
}

app.Run();

