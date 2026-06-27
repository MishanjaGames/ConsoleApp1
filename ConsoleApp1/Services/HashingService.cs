using System.Security.Cryptography;
using System.Text;
using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            string txHash = string.Concat(block.Transactions.Select(tx => ComputeHash(tx.ToRawString())));
            string data = $"{{{block.TimeStamp:O}|{block.Index}|{block.PreviousHash}|{block.Nonce}|{block.Difficulty}|{txHash}}}";
            return ComputeHash(data);
        }

        public string ComputeHash_P(string input) => ComputeHash(input);

        private static string ComputeHash(string input)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash);
        }
    }
}