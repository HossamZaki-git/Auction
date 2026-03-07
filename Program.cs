using Auction.Hubs;
using Auction.Models;
using Auction.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Redis db connection
var redisUrl = builder.Configuration["Redis:ConnectionString"];
var multiplexer = ConnectionMultiplexer.Connect(redisUrl);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
var endpoints = new List<RedLockEndPoint>
{
    new RedLockEndPoint(multiplexer.GetEndPoints()[0])
};

builder.Services.AddSingleton<IDistributedLockFactory>(sp => RedLockFactory.Create(endpoints));

// Adding SignalR with Redis backplane, registering the application in the auction channel
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisUrl, options =>
    {
        options.Configuration.ChannelPrefix = "auction";
    });

builder.Services.AddSingleton<AuctionsManager>();


builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.SetIsOriginAllowed(origin => {
            // if its a local file, allow it
            if (origin == "null") 
                return true;

            //  otherwise, check if the host is the localhost
            return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                   && uri.Host == "localhost";
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Seeding data
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var manager = scope.ServiceProvider.GetRequiredService<AuctionsManager>();
        logger.LogInformation("Starting seeding auctions...");
        await manager.SeedAsync();
        logger.LogInformation("Seeding completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding data.");
    }
}


app.MapPut("/restart", async(AuctionsManager auctionsManager, IHubContext<AuctionHub, IAuctionNotificationClient> hubContext) =>
{
    // Resetting the data to the default
    await auctionsManager.RestartAuctions();
    List<AuctionDTO> auctions = await auctionsManager.GetAllAuctions();
    // Notifying all the clients to restart their auctions with the new data
    await hubContext.Clients.All.RestartAllAuctions(auctions);
});


app.MapHub<AuctionHub>("/auctionHub");


app.Run();
