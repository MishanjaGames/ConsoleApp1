using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BlockChain_01.Services;

namespace BlockChain_01.Models
{
    public class Transaction
    {
        public string Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public decimal Amount { get; set; }
        public DateTime TimeStamp { get; set; }
        public byte[] SenderPublicKey { get; set; }
        public byte[] Signature { get; set; }
        public decimal Fee { get; set; }

        public string ToRawString()
        {
            if (Signature == null)
                return $"[{TimeStamp.ToString("O")}] {Id} | {From:10} -> {To:10} | {Amount} | {Fee} | {Convert.ToHexString(new byte[0])}";
            return $"[{TimeStamp.ToString("O")}] {Id} | {From:10} -> {To:10} | {Amount} | {Fee} | {Convert.ToHexString(Signature)}";
        }

        public byte[] GetDataToSign()
        {
            return Encoding.UTF8.GetBytes($"[{TimeStamp.ToString("O")}] {Id:10} | {From:10} -> {To:10} | {Amount} | {Fee}");
        }

        public Transaction(string from, string to, decimal amount, byte[] senderPublicKey)
        {
            From = from;
            To = to;
            Amount = amount;
            TimeStamp = DateTime.UtcNow;
            Id = new HashingService().ComputeHash_P($"{From}>|>{To}|{Amount}|{Fee}");
            SenderPublicKey = senderPublicKey;
        }
    }
}