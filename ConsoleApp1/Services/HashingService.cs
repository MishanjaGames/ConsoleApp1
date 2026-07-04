using BlockChain_01.Models;
using System.Security.Cryptography;
using System.Text;

namespace BlockChain_01.Services
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            string merkleRoot = GetMerkleRoot(block.Transactions);
            string blockData = "{" + $"({block.TimeStamp.ToString("O")})|{block.Index}|{block.PreviousHash}|{block.Nonce}|{block.Difficulty}|{merkleRoot}" + "}";
            return ComputeHash(blockData);
        }

        public string ComputeHash_P(string input) => ComputeHash(input);

        private string ComputeHash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes);
        }

        public string GetMerkleRoot(List<Transaction> transactions)
        {
            if (transactions == null || transactions.Count == 0)
                return string.Empty;

            for (int i = 0; i < transactions.Count - 1; i += 2)
            {
                string h1 = ComputeHash(transactions[i].ToRawString());
                string h2 = ComputeHash(transactions[i + 1].ToRawString());
                if (h1 == h2)
                    throw new Exception("Attack CVE-2012-2459 has been found. Fixing system...");
            }

            var currentLayer = new List<string>();
            foreach (var transaction in transactions)
                currentLayer.Add(ComputeHash(transaction.ToRawString()));

            //int level = 0;
            //string levelName = currentLayer.Count == 1 ? "Root" : "Branch";
            //Console.WriteLine($"Level {level} ({levelName}): {currentLayer.Count} hash");

            while (currentLayer.Count > 1)
            {
                var nextLayer = new List<string>();
                for (int i = 0; i < currentLayer.Count; i += 2)
                {
                    string left = currentLayer[i];
                    string right = (i + 1 < currentLayer.Count) ? currentLayer[i + 1] : left;
                    nextLayer.Add(ComputeHash(left + right));
                }
                currentLayer = nextLayer;
                //level++;
                //levelName = currentLayer.Count == 1 ? "Root" : "Branch";
                //Console.WriteLine($"Level {level} ({levelName}): {currentLayer.Count} hash");
            }

            return currentLayer[0];
        }

        public List<(string Hash, bool IsLeft)> GetMerkleProof(List<Transaction> transactions, string targetTransactionId)
        {
            if (transactions == null || transactions.Count == 0)
                throw new ArgumentException("Transaction list is empty. Awaiting new.");

            int index = transactions.FindIndex(t => t.Id == targetTransactionId);
            if (index == -1)
                throw new ArgumentException("Transaction has been lost. Try again.");

            var proof = new List<(string Hash, bool IsLeft)>();
            var currentLayer = transactions.Select(t => ComputeHash(t.ToRawString())).ToList();

            while (currentLayer.Count > 1)
            {
                var nextLayer = new List<string>();
                for (int i = 0; i < currentLayer.Count; i += 2)
                {
                    string left = currentLayer[i];
                    string right = (i + 1 < currentLayer.Count) ? currentLayer[i + 1] : left;

                    if (i == index || i + 1 == index)
                    {
                        bool targetIsLeft = (index == i);
                        string sibling = targetIsLeft ? right : left;
                        proof.Add((sibling, !targetIsLeft));
                        index = nextLayer.Count;
                    }

                    nextLayer.Add(ComputeHash(left + right));
                }
                currentLayer = nextLayer;
            }

            return proof;
        }

        public bool VerifyMerkleProof(string targetTxHash, string expectedRoot, List<(string Hash, bool IsLeft)> proof)
        {
            string currentHash = targetTxHash;
            foreach (var (siblingHash, isLeft) in proof)
            {
                currentHash = isLeft
                    ? ComputeHash(siblingHash + currentHash)
                    : ComputeHash(currentHash + siblingHash);
            }
            return currentHash == expectedRoot;
        }
    }
}