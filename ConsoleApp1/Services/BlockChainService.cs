using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class BlockChainService
    {
        public MiningService _miningService { get; set; }
        public HashingService _hashingService { get; set; }
        public List<Block> Chain { get; set; }
        public int Difficulty { get; private set; }

        public BlockChainService()
        {
            Chain = new List<Block>();
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            Difficulty = 7;
            CreateGenesisBlock();
        }

        private void CreateGenesisBlock()
        {
            var genesisBlock = new Block(0, DateTime.Now, "SYSTEM", "Genesis Block", "0");
            genesisBlock.Hash = _hashingService.ComputeHash(genesisBlock);
            Chain.Add(genesisBlock);
        }

        public async Task<bool> AddBlockAsync(string author, string data,
            CancellationToken cancellationToken = default)
        {
            var lastBlock = Chain.Last();
            var newBlock = new Block(lastBlock.Index + 1, DateTime.UtcNow, author, data, lastBlock.Hash);

            Console.WriteLine($"\n[Blockchain] Adding block ...");

            var result = await _miningService.MineBlockAsync(newBlock, Difficulty, cancellationToken);

            if (result == null)
                return false;

            Chain.Add(newBlock);
            return true;
        }

        public void AddBlock(string author, string data)
        {
            AddBlockAsync(author, data).GetAwaiter().GetResult();
        }

        public bool IsValid()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var cur = Chain[i];
                var prev = Chain[i - 1];

                if (cur.Hash != _hashingService.ComputeHash(cur)) return false;
                if (cur.PreviousHash != prev.Hash) return false;
                if (!cur.Hash.StartsWith(new string('0', Difficulty))) return false;
            }
            return true;
        }

        public int GetInvalidBlockIndex()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var cur = Chain[i];
                var prev = Chain[i - 1];
                if (cur.Hash != _hashingService.ComputeHash(cur)) return cur.Index;
                if (cur.PreviousHash != prev.Hash) return cur.Index;
            }
            return -1;
        }

        public Block? FindBlockByHash(string targetHash)
            => Chain.FirstOrDefault(b => b.Hash == targetHash);
    }
}