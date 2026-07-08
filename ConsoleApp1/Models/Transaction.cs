using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BlockChain_01.Services;

namespace BlockChain_01.Models
{
    public enum TransactionType
    {
        Transfer,
        IssueToken
    }

    public class Transaction
    {
        public string Id { get; set; } = string.Empty;
        public TransactionType Type { get; set; } = TransactionType.Transfer;

        // Ticker of the currency this transaction operates on. "BASE" is the native coin.
        public string Currency { get; set; } = "BASE";

        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        // Only used for Type == IssueToken: total emission created for the new ticker.
        public decimal? TotalSupply { get; set; }

        public DateTime TimeStamp { get; set; }
        public byte[] SenderPublicKey { get; set; } = new byte[0];
        public byte[] Signature { get; set; } = new byte[0];

        // Network fee. ALWAYS paid in BASE, regardless of the transaction's Currency.
        public decimal Fee { get; set; }

        public string ToRawString()
        {
            string sigHex = Signature == null ? Convert.ToHexString(new byte[0]) : Convert.ToHexString(Signature);
            string supplyPart = TotalSupply.HasValue ? TotalSupply.Value.ToString() : "-";
            return $"[{TimeStamp.ToString("O")}] {Id} | {Type} | {Currency} | {From:5} -> {To:5} | {Amount} | Supply={supplyPart} | Fee={Fee} | {sigHex:5}";
        }

        public byte[] GetDataToSign()
        {
            string supplyPart = TotalSupply.HasValue ? TotalSupply.Value.ToString() : "-";
            return Encoding.UTF8.GetBytes($"[{TimeStamp.ToString("O")}] {Id:5} | {Type} | {Currency} | {From:5} -> {To:5} | {Amount} | {supplyPart} | {Fee}");
        }

        public Transaction() { }

        /// <summary>
        /// Standard transfer of an existing currency ("BASE" by default, or any previously issued token).
        /// </summary>
        public Transaction(string from, string to, decimal amount, byte[] senderPublicKey, string currency = "BASE")
        {
            Type = TransactionType.Transfer;
            Currency = string.IsNullOrWhiteSpace(currency) ? "BASE" : currency.ToUpperInvariant();
            From = from;
            To = to;
            Amount = amount;
            TimeStamp = DateTime.UtcNow;
            Id = new HashingService().ComputeHash_P($"{From}>|>{To}|{Amount}|{Currency}|{Fee}|{TimeStamp:O}");
            SenderPublicKey = senderPublicKey;
        }

        /// <summary>
        /// ICO / token-issuance transaction. The issuer receives the entire emission
        /// on their own address (From == To == issuer) and pays a fixed 100 BASE tax
        /// via the Fee field, which goes to the miner.
        /// </summary>
        public static Transaction CreateIssueTransaction(string issuerAddress, string currency, decimal totalSupply, byte[] senderPublicKey, decimal icoTax = 100m)
        {
            var tx = new Transaction
            {
                Type = TransactionType.IssueToken,
                Currency = string.IsNullOrWhiteSpace(currency) ? currency : currency.ToUpperInvariant(),
                From = issuerAddress,
                To = issuerAddress,
                Amount = totalSupply,
                TotalSupply = totalSupply,
                TimeStamp = DateTime.UtcNow,
                SenderPublicKey = senderPublicKey,
                Fee = icoTax
            };
            tx.Id = new HashingService().ComputeHash_P($"{tx.From}|ISSUE|{tx.Currency}|{totalSupply}|{tx.TimeStamp:O}");
            return tx;
        }
    }
}