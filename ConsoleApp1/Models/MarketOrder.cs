using System;

namespace BlockChain_01.Models
{
    public class MarketOrder
    {
        public string Id { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerAddress { get; set; } = string.Empty;
        public bool IsBuyOrder { get; set; }
        public string Currency { get; set; } = "BASE"; // token ticker or "BASE"
        public decimal Amount { get; set; }
        public decimal PricePerUnit { get; set; } // price denominated in BASE
        public bool IsNFT { get; set; } = false;
        public string NFTId { get; set; } = string.Empty;
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    }
}
