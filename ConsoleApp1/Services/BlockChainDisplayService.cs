using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class BlockChainDisplayService
    {
        private BlockChainService BlockChain { get; set; }

        public BlockChainDisplayService(BlockChainService blockChain)
        {
            BlockChain = blockChain;
        }

        public void PrintBlockChain(List<Block> chain)
        {
            foreach (var block in chain)
            {
                Console.WriteLine();
                Console.WriteLine($"Index: {block.Index}");
                Console.WriteLine($"PreviousHash: {block.PreviousHash}");
                Console.WriteLine($"Hash: {block.Hash}");
                Console.WriteLine($"Difficulty: {block.Difficulty}");
                Console.WriteLine($"Nonce: {block.Nonce}");
                Console.WriteLine($"TimeStamp: {block.TimeStamp}");
                Console.WriteLine($"Generating Time: {block.MiningDuration}");
                Console.WriteLine($"Initiator: {block.Author}");
                Console.WriteLine($"Data: {block.Data}");
                Console.WriteLine(new string('-', 50));
            }
        }

        public void PrintBlock(Block? block, string? hash = null, int index = -1)
        {
            if (block == null)
                block = hash == null ? BlockChain.Chain[index] : BlockChain.FindBlockByHash(hash);

            if (block == null) { Console.WriteLine("Block not found."); return; }

            Console.WriteLine();
            Console.WriteLine($"Index: {block.Index}");
            Console.WriteLine($"PreviousHash: {block.PreviousHash}");
            Console.WriteLine($"Hash: {block.Hash}");
            Console.WriteLine($"Difficulty: {block.Difficulty}");
            Console.WriteLine($"Nonce: {block.Nonce}");
            Console.WriteLine($"TimeStamp: {block.TimeStamp}");
            Console.WriteLine($"Generating Time: {block.MiningDuration}");
            Console.WriteLine($"Initiator: {block.Author}");
            Console.WriteLine($"Data: {block.Data}");
            Console.WriteLine(new string('-', 50));
        }

        public void PrintValidationResult(bool isValid)
        {
            Console.WriteLine(isValid ? "The BlockChain is valid." : "The Blockchain isn't valid.");
        }
    }
}