using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class BlockChainService
    {
        private readonly MiningService _miningService;
        private readonly HashingService _hashingService;
        private readonly TransactionService _transactionService;
        public List<Block> Chain { get; set; }
        public int Difficulty { get; private set; }
        public int MaxBlockSizeBytes { get; } = 256;

        private readonly double _targetBlockTime;
        private readonly int _adjustmentInterval = 2;
        private const double _miningDurationTolerance = 2.0;

        public BlockChainService(double targetBlockTime = 5)
        {
            _targetBlockTime = targetBlockTime;
            Chain = new List<Block>();
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            _transactionService = new TransactionService();
            Difficulty = 1;
            CreateGenesisBlock();
        }

        private void CreateGenesisBlock()
        {
            var genesisBlock = new Block(0, DateTime.UtcNow, new List<Transaction>(), "0", Difficulty);
            _miningService.MineBlock(genesisBlock, Difficulty);
            Chain.Add(genesisBlock);
        }

        public async Task<bool> AddBlockAsync(List<Transaction> transactions,
            CancellationToken cancellationToken = default)
        {
            //var (included, weight) = FitToByteLimit(transactions);


            foreach (var transaction in transactions)
            {
                if (!_transactionService.ValidateTransaction(transaction).IsValid)
                {
                    throw new InvalidOperationException("Invalid Transaction");
                }
            }

            AdjustDifficulty();

            var lastBlock = Chain.Last();
            var newBlock = new Block(lastBlock.Index + 1, DateTime.UtcNow, transactions, lastBlock.Hash, Difficulty);

            Console.WriteLine($"\n[Blockchain] Adding block ...");

            var result = await _miningService.MineBlockAsync(newBlock, Difficulty, cancellationToken);

            if (result == null)
                return false;

            Chain.Add(newBlock);
            return true;
        }

        public void AddBlock(List<Transaction> transactions)
        {
            AddBlockAsync(transactions).GetAwaiter().GetResult();
        }

        public void ProcessTransactions(List<Transaction> incomingTransactions)
        {
            var batch = new List<Transaction>();
            int weight = 0;
            int blockCount = 0;

            void FlushBatch()
            {
                if (batch.Count == 0) return;
                AddBlock(new List<Transaction>(batch));
                blockCount++;
                Console.WriteLine($"[ProcessTransactions] Block #{blockCount} mined: {batch.Count} tx, {weight}/{MaxBlockSizeBytes} bytes.");
                batch.Clear();
                weight = 0;
            }

            foreach (var tx in incomingTransactions)
            {
                var (isValid, error) = _transactionService.ValidateTransaction(tx);
                if (!isValid)
                {
                    Console.WriteLine($"[ProcessTransactions] Rejected tx ({tx.From} -> {tx.To}): {error}");
                    continue;
                }

                int txBytes = System.Text.Encoding.UTF8.GetByteCount(tx.ToRawString());
                if (txBytes > MaxBlockSizeBytes)
                {
                    Console.WriteLine($"[ProcessTransactions] Rejected tx: {txBytes} bytes exceeds block limit {MaxBlockSizeBytes}.");
                    continue;
                }

                if (weight + txBytes > MaxBlockSizeBytes)
                    FlushBatch();

                batch.Add(tx);
                weight += txBytes;
            }

            FlushBatch();
            Console.WriteLine($"[ProcessTransactions] Done. Total blocks mined: {blockCount}.");
        }

        private (List<Transaction> Included, int TotalBytes) FitToByteLimit(List<Transaction> transactions)
        {
            var included = new List<Transaction>();
            int total = 0;
            foreach (var tx in transactions)
            {
                int txBytes = System.Text.Encoding.UTF8.GetByteCount(tx.ToRawString());
                if (total + txBytes > MaxBlockSizeBytes)
                    break;
                included.Add(tx);
                total += txBytes;
            }
            return (included, total);
        }

        private void AdjustDifficulty()
        {
            var recentBlocks = Chain.Skip(Math.Max(0, Chain.Count - _adjustmentInterval)).ToList();
            double avgTime = recentBlocks.Average(b => b.MiningDuration);

            if (avgTime < _targetBlockTime)
                Difficulty = Difficulty + 1;
            else if (avgTime > _targetBlockTime)
                Difficulty = Math.Max(1, Difficulty - 1);
        }

        public bool IsValid()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var cur = Chain[i];
                var prev = Chain[i - 1];

                if (cur.Hash != _hashingService.ComputeHash(cur)) return false;
                if (cur.PreviousHash != prev.Hash) return false;
                if (!cur.Hash.StartsWith(_miningService.VanityTarget)) return false;

                if (cur.MiningDuration < 0) return false;

                if (cur.TimeStamp <= prev.TimeStamp) return false;

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