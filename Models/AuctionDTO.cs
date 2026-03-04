namespace Auction.Models
{
    public class AuctionDTO
    {
        public int ID { get; set; }
        public double HighestBidValue { get; set; }
        public DateTime EndingTime { get; set; }
        // The last id in the list is the higher bidder
        public List<string> WinnersIDs { get; set; }
    }
}
