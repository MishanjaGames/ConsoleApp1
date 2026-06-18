using BlockChain_01.Models;
using System.Security.Cryptography;
using System.Text;

namespace BlockChain_01.Services
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            var totalTransactionHash = "";
            foreach (var transaction in block.Transactions)
            {
                totalTransactionHash += ComputeHash(transaction.ToRawString());
            }
            string blockData = "{"+$"({block.TimeStamp.ToString("O")})|{block.Index}|{block.PreviousHash}|{block.Nonce}|{block.Difficulty}|{totalTransactionHash}"+"}";
            return ComputeHash(blockData);
        }

        public string ComputeHash_P(string input) => ComputeHash(input);

        private string ComputeHash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}