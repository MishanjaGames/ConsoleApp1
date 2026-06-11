using BlockChain_01.Models;
using System.Security.Cryptography;
using System.Text;

namespace BlockChain_01.Services
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            string blockData = $"{block.Index}{block.TimeStamp}{block.Author}{block.Data}{block.Nonce}{block.PreviousHash}";
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