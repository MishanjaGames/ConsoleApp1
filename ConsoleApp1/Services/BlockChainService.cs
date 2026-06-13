using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class BlockChainService
    {
        private MiningService _miningService { get; set; }
        private HashingService _hashingService { get; set; }
        public List<Block> Chain { get; set; }
        public int Difficulty { get; private set; }

        private readonly double _targetBlockTime;
        private readonly int _adjustmentInterval = 2;
        private const double _miningDurationTolerance = 2.0; // seconds

        public BlockChainService(double targetBlockTime = 5)
        {
            _targetBlockTime = targetBlockTime;
            Chain = new List<Block>();
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            Difficulty = 1;
            CreateGenesisBlock();
        }

        private void CreateGenesisBlock()
        {
            var genesisBlock = new Block(0, DateTime.UtcNow, "SYSTEM", "Genesis Block", "0", Difficulty);
            _miningService.MineBlock(genesisBlock, Difficulty);
            Chain.Add(genesisBlock);
        }

        public async Task<bool> AddBlockAsync(string author, string data,
            CancellationToken cancellationToken = default)
        {
            AdjustDifficulty();

            var lastBlock = Chain.Last();
            var newBlock = new Block(lastBlock.Index + 1, DateTime.UtcNow, author, data, lastBlock.Hash, Difficulty);

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

        private void AdjustDifficulty()
        {
            var recentBlocks = Chain.Skip(Math.Max(0, Chain.Count - _adjustmentInterval)).ToList();
            double avgTime = recentBlocks.Average(b => b.MiningDuration);

            if (avgTime < _targetBlockTime)
                Difficulty = Difficulty + 1;           // max +1 per adjustment
            else if (avgTime > _targetBlockTime)
                Difficulty = Math.Max(1, Difficulty - 1); // max -1, never below 1
        }

        public bool IsValid()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var cur = Chain[i];
                var prev = Chain[i - 1];

                // Hash integrity
                if (cur.Hash != _hashingService.ComputeHash(cur)) return false;
                if (cur.PreviousHash != prev.Hash) return false;
                if (!cur.Hash.StartsWith(new string('0', cur.Difficulty))) return false;

                // 1. MiningDuration cannot be negative
                if (cur.MiningDuration < 0) return false;

                // 2. Timestamp must go forward
                if (cur.TimeStamp <= prev.TimeStamp) return false;

                // 3. Cross-check: MiningDuration must not exceed physical timestamp diff + tolerance
                double physicalDiff = (cur.TimeStamp - prev.TimeStamp).TotalSeconds;
                if (cur.MiningDuration > physicalDiff + _miningDurationTolerance) return false;
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
                if (cur.MiningDuration < 0) return cur.Index;
                if (cur.TimeStamp <= prev.TimeStamp) return cur.Index;
                double physicalDiff = (cur.TimeStamp - prev.TimeStamp).TotalSeconds;
                if (cur.MiningDuration > physicalDiff + _miningDurationTolerance) return cur.Index;
            }
            return -1;
        }

        public Block? FindBlockByHash(string targetHash)
            => Chain.FirstOrDefault(b => b.Hash == targetHash);
    }
}