using BlockChain_01.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockChain_01.Services
{
    public class MarketService
    {
        private readonly BlockChainService _blockchain;
        private readonly TransactionService _txService;
        private readonly WalletService _walletService;
        private readonly Func<string, Wallet?> _walletResolver;

        public List<MarketOrder> BuyOrders { get; } = new List<MarketOrder>();
        public List<MarketOrder> SellOrders { get; } = new List<MarketOrder>();

        public MarketService(BlockChainService blockchain, TransactionService txService, WalletService walletService, Func<string, Wallet?> walletResolver)
        {
            _blockchain = blockchain;
            _txService = txService;
            _walletService = walletService;
            _walletResolver = walletResolver;
        }

        public MarketOrder PlaceSellOrder(string ownerName, string ownerAddress, string currency, decimal amount, decimal pricePerUnit, bool isNft = false, string nftId = "")
        {
            var balance = _walletService.GetBalance(ownerAddress, currency);
            if (!isNft && balance < amount)
                throw new InvalidOperationException($"Insufficient {currency} balance: {balance} < {amount}");

            var ord = new MarketOrder
            {
                Id = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                OwnerName = ownerName,
                OwnerAddress = ownerAddress,
                IsBuyOrder = false,
                Currency = currency.ToUpperInvariant(),
                Amount = amount,
                PricePerUnit = pricePerUnit,
                IsNFT = isNft,
                NFTId = nftId
            };
            SellOrders.Add(ord);
            return ord;
        }

        public MarketOrder PlaceBuyOrder(string ownerName, string ownerAddress, string currency, decimal amount, decimal pricePerUnit, bool isNft = false, string nftId = "")
        {
            var cost = amount * pricePerUnit;
            var baseBal = _walletService.GetBalance(ownerAddress, "BASE");
            if (baseBal < cost)
                throw new InvalidOperationException($"Insufficient BASE to place buy order: {baseBal} < {cost}");

            var ord = new MarketOrder
            {
                Id = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                OwnerName = ownerName,
                OwnerAddress = ownerAddress,
                IsBuyOrder = true,
                Currency = currency.ToUpperInvariant(),
                Amount = amount,
                PricePerUnit = pricePerUnit,
                IsNFT = isNft,
                NFTId = nftId
            };
            BuyOrders.Add(ord);
            return ord;
        }

        public List<(MarketOrder buy, MarketOrder sell, decimal executedAmount)> MatchOrders()
        {
            var executed = new List<(MarketOrder, MarketOrder, decimal)>();

            // simple greedy matching: iterate buys and find a compatible sell
            foreach (var buy in BuyOrders.ToList())
            {
                var candidates = SellOrders
                    .Where(s => s.Currency.Equals(buy.Currency, StringComparison.OrdinalIgnoreCase)
                                && s.PricePerUnit <= buy.PricePerUnit
                                && (!s.IsNFT || s.NFTId == buy.NFTId))
                    .OrderBy(s => s.PricePerUnit)
                    .ToList();

                foreach (var sell in candidates)
                {
                    if (buy.Amount <= 0 || sell.Amount <= 0) continue;
                    var amt = Math.Min(buy.Amount, sell.Amount);

                    // check balances again
                    var sellerTokenBal = _walletService.GetBalance(sell.OwnerAddress, sell.Currency);
                    var buyerBaseBal = _walletService.GetBalance(buy.OwnerAddress, "BASE");
                    var totalPrice = amt * sell.PricePerUnit;
                    if ((!sell.IsNFT && sellerTokenBal < amt) || buyerBaseBal < totalPrice)
                        continue; // skip if funds disappeared

                    // execute: create two transactions (BASE from buyer->seller, token from seller->buyer)
                    var sellerWallet = _walletResolver(sell.OwnerAddress);
                    var buyerWallet = _walletResolver(buy.OwnerAddress);
                    if (sellerWallet == null || buyerWallet == null)
                        continue;

                    try
                    {
                        var payTx = _txService.CreateTransaction(buyerWallet, sell.OwnerAddress, totalPrice, buyerWallet.PublicKey, "BASE");
                        var tokenTx = _txService.CreateTransaction(sellerWallet, buy.OwnerAddress, amt, sellerWallet.PublicKey, sell.Currency);

                        if (payTx != null) _blockchain.AddTransactionToMempool(payTx);
                        if (tokenTx != null) _blockchain.AddTransactionToMempool(tokenTx);

                        buy.Amount -= amt;
                        sell.Amount -= amt;
                        executed.Add((buy, sell, amt));
                    }
                    catch
                    {
                        // if execution fails, skip
                        continue;
                    }

                    if (buy.Amount <= 0) break;
                }
            }

            // cleanup filled orders
            BuyOrders.RemoveAll(o => o.Amount <= 0);
            SellOrders.RemoveAll(o => o.Amount <= 0);

            return executed;
        }
    }
}
