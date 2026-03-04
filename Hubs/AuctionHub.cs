using Auction.Models;
using Auction.Utilities;
using Microsoft.AspNetCore.SignalR;
using System.Runtime.InteropServices;


namespace Auction.Hubs
{
    // contains the signatures of the methods that can be applied on the client by the server call
    public interface IAuctionNotificationClient
    {
        // Letting the client know the auctions data and their user id
        Task Initialize(List<AuctionDTO> auctions, string UserID);
        // Notifying all the clients to restart their auctions with this new data
        Task RestartAllAuctions(List<AuctionDTO> auctions);
        // Showing a certain message to the user
        Task ShowUserMessage(string Message);
        // Notifying the user to update the auction with the new data
        Task UpdateAuction(AuctionDTO auction);
    }
    public class AuctionHub : Hub<IAuctionNotificationClient>
    {
        private readonly AuctionsManager auctionsManager;

        public AuctionHub(AuctionsManager auctionsManager)
        {
            this.auctionsManager = auctionsManager;
        }
        public override async Task<object> OnConnectedAsync()
        {
            var auctions = await auctionsManager.GetAllAuctions();
            // Letting the client know the auctions data and their user id
            await Clients.Caller.Initialize(auctions, Context.ConnectionId);
            // Subscribing the client to all the auctions groups to receive the updates of all the auctions
            for (int i = 0; i < auctions.Count; i++)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"{i}");

            return base.OnConnectedAsync();
        }

        public async Task Bid(string auctionID, double value)
        {
            BidResult result = await auctionsManager.Bid(auctionID, Context.ConnectionId, value);

            // Another bid is in process, so we need to retry after a short delay
            while (result == BidResult.Retry)
            {
                await Task.Delay(250);
                result = await auctionsManager.Bid(auctionID, Context.ConnectionId, value);
            }

            if(result != BidResult.Success)
            {
                string Message = result switch
                {
                    BidResult.BadRequest => "Bad request, please check your bid value and try again",
                    BidResult.AuctionFinished => "Auction is already finished, you can't bid on it",
                    BidResult.BidTooLow => "Too low bid, please try again with a higher value"
                };

                await Clients.Caller.ShowUserMessage(Message);

                return;
            }

            // successfully placed the bid
            int id;
            int.TryParse(auctionID, out id);
            var auctionDto = await auctionsManager.GetAuctionByID(id);

            // Notifying the user to update the auction with the new data
            await Clients.All.UpdateAuction(auctionDto);
        }
        

    }
}
