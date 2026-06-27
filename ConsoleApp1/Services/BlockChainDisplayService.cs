using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class BlockChainDisplayService
    {
        private readonly BlockChainService _blockchain;

        public BlockChainDisplayService(BlockChainService blockchain) => _blockchain = blockchain;

        public void PrintChain(List<Block> chain)
        {
            foreach (var block in chain)
                PrintBlock(block);
        }

        public void PrintBlock(Block? block = null, string? hash = null, int index = -1)
        {
            block ??= hash != null ? _blockchain.FindBlockByHash(hash) : _blockchain.Chain.ElementAtOrDefault(index);

            if (block == null) { Console.WriteLine("Block not found."); return; }

            Console.WriteLine();
            Console.WriteLine($"  Index:    {block.Index}");
            Console.WriteLine($"  PrevHash: {block.PreviousHash}");
            Console.WriteLine($"  Hash:     {block.Hash}");
            Console.WriteLine($"  Difficulty: {block.Difficulty}  Nonce: {block.Nonce}");
            Console.WriteLine($"  Time:     {block.TimeStamp}  ({block.MiningDuration:F2}s to mine)");
            Console.WriteLine($"  Tx count: {block.Transactions.Count}");

            if (block.Transactions.Count == 0)
                Console.WriteLine("  [Genesis Block]");
            else
                foreach (var tx in block.Transactions)
                    Console.WriteLine($"  {tx.ToRawString()}");

            Console.WriteLine(new string('─', 60));
        }

        public void PrintValidation(bool isValid)
        {
            if (isValid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Blockchain is valid.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Blockchain integrity compromised!");
            }
            Console.ResetColor();
        }
    }
}