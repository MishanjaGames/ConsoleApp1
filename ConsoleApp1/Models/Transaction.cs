using System.Text;
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

        public Transaction() { }

        public Transaction(string from, string to, decimal amount, byte[] senderPublicKey)
        {
            From = from;
            To = to;
            Amount = amount;
            SenderPublicKey = senderPublicKey;
            TimeStamp = DateTime.UtcNow;
            Id = new HashingService().ComputeHash_P($"{From}>|>{To}|{Amount}|{Fee}");
        }

        public byte[] GetDataToSign() =>
            Encoding.UTF8.GetBytes($"[{TimeStamp:O}] {Id} | {From} -> {To} | {Amount} | {Fee}");

        public string ToRawString()
        {
            string sig = Signature != null ? Convert.ToHexString(Signature) : Convert.ToHexString(Array.Empty<byte>());
            return $"[{TimeStamp:O}] {Id} | {From} -> {To} | {Amount} | {Fee} | {sig}";
        }
    }
}