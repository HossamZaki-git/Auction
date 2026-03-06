using Auction.Models;
using RedLockNet;
using StackExchange.Redis;
using System.Text.Json;

namespace Auction.Utilities
{
    public enum BidResult
    {
        Success,
        AuctionFinished,
        BidTooLow,
        BadRequest,
        Retry
    }
    public class AuctionsManager
    {
        private readonly IDatabase db;
        private readonly IConnectionMultiplexer connectionMultiplexer;
        private readonly IDistributedLockFactory lockFactory;
        private readonly ILogger<AuctionsManager> _logger;

        public AuctionsManager(IConnectionMultiplexer connectionMultiplexer, IDistributedLockFactory lockFactory, ILogger<AuctionsManager> logger)
        {
            db = connectionMultiplexer.GetDatabase();
            this.connectionMultiplexer = connectionMultiplexer;
            this.lockFactory = lockFactory;
            _logger = logger;
        }

        private bool isEmpty() => !db.KeyExists("0");

        public async Task SeedAsync()
        {
            // The lock key name
            var resource = "seedingLock";
            var expiry = TimeSpan.FromSeconds(5);

            using (var redLock = await lockFactory.CreateLockAsync(resource, expiry))
            {
                if (redLock.IsAcquired)
                {
                    // Only ONE server can be inside this block at a time
                    if (isEmpty())
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            string auctionKey = i.ToString();

                            // Initialize WinnersIDs with one null value as requested
                            var initialWinners = new string?[] { null };
                            string winnersJson = JsonSerializer.Serialize(initialWinners);

                            var auctionData = new HashEntry[]
                            {
                                new("EndingTime", DateTime.UtcNow.AddMinutes(5).ToString("o")), // ISO 8601 format
                                new("highestValue", 0),
                                new("WinnersIDs", winnersJson)
                            };

                            await db.HashSetAsync(auctionKey, auctionData);
                        }
                        _logger.LogInformation("Successfully seeded 3 auctions.");
                    }
                }
            }

        }

        public async Task RestartAuctions()
        {
            // Some Redis providers (managed services) disable FLUSHDB (admin-only).
            // Instead of calling FlushDatabase, enumerate keys and delete them so this works
            // without admin privileges.
            try
            {
                var endpoint = connectionMultiplexer.GetEndPoints()[0];
                var server = connectionMultiplexer.GetServer(endpoint);

                // Enumerate keys in DB 0 and delete them via IDatabase so we don't need admin
                foreach (var key in server.Keys(database: 0))
                    await db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to clear database during RestartAuctions");
            }

            await SeedAsync();
        }

        public async Task<BidResult> Bid(string auctionID, string userID, double value)
        {
            // The lock key name
            var resource = $"lock:auction:{auctionID}";
            var expiry = TimeSpan.FromSeconds(5);

            using (var redLock = await lockFactory.CreateLockAsync(resource, expiry))
            {
                // The other server is trying to bid in this auction
                if (!redLock.IsAcquired)
                    return BidResult.Retry;


                var data = await db.HashGetAllAsync(auctionID);
                if (data.Length == 0) // no such an auction
                    return BidResult.BadRequest;

                var dict = data.ToDictionary(x => x.Name.ToString(), x => x.Value);


                DateTime.TryParse(dict["EndingTime"], null, System.Globalization.DateTimeStyles.RoundtripKind, out var endingTime);
                if (DateTime.UtcNow > endingTime) // auction is finished
                    return BidResult.AuctionFinished;


                double.TryParse((string)dict["highestValue"], out double highestValue);
                if (highestValue >= value) // Low bid
                    return BidResult.BidTooLow;

                // Updating the list and the value
                var winners = JsonSerializer.Deserialize<List<string?>>((string?)dict["WinnersIDs"]);
                // The user with the highest bid is the last one in the list
                winners.Add(userID);

                var updates = new HashEntry[]
                {
                     new("highestValue", value),
                    new("WinnersIDs", JsonSerializer.Serialize(winners))
                };

                await db.HashSetAsync(auctionID, updates);
                return BidResult.Success;
            }

        }

        public async Task<List<AuctionDTO>> GetAllAuctions()
        {
            var auctions = new List<AuctionDTO>();

            var endpoint = connectionMultiplexer.GetEndPoints().First();
            var server = connectionMultiplexer.GetServer(endpoint);


            var keys = server.Keys(database: 0).ToList();

            foreach (var key in keys)
            {
                
                var data = await db.HashGetAllAsync(key);
                if (data.Length == 0) 
                    continue;

              
                var dict = data.ToDictionary(x => x.Name.ToString(), x => x.Value);

                var auction = new AuctionDTO
                {
                    ID = int.Parse(key!),
                    HighestBidValue = double.TryParse(dict.GetValueOrDefault("highestValue").ToString(), out var val) ? val : 0,
                    EndingTime = DateTime.TryParse(dict.GetValueOrDefault("EndingTime"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var time) ? time : DateTime.UtcNow,

                    // Deserialize the JSON winners list
                    WinnersIDs = dict.ContainsKey("WinnersIDs")
                        ? JsonSerializer.Deserialize<List<string>>((string)dict["WinnersIDs"]!) ?? new List<string>()
                        : new List<string>()
                };

                auctions.Add(auction);
            }

            return auctions.OrderBy(a => a.ID).ToList();
        }

        public async Task<AuctionDTO?> GetAuctionByID(int id)
        {
            string key = id.ToString();

            var data = await db.HashGetAllAsync(key);

            // If the key doesn't exist or is empty, return null
            if (data.Length == 0)
            {
                return null;
            }

            
            var dict = data.ToDictionary(x => x.Name.ToString(), x => x.Value);

            
            return new AuctionDTO
            {
                ID = id,

                HighestBidValue = (double)dict.GetValueOrDefault("highestValue", 0),

                EndingTime = DateTime.TryParse(dict.GetValueOrDefault("EndingTime"), null,
                             System.Globalization.DateTimeStyles.RoundtripKind, out var time)
                             ? time : DateTime.UtcNow,

                WinnersIDs = dict.TryGetValue("WinnersIDs", out var winnersJson) && !winnersJson.IsNull
                    ? JsonSerializer.Deserialize<List<string>>((string)winnersJson!) ?? new List<string>()
                    : new List<string>()
            };
        }
    }
}
