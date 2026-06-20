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
        public int MaxBlockSizeBytes { get; } = 10240;
        public decimal MaxSupply { get; } = 1000;
        public decimal TotalMinted { get; private set; } = 0;

        private readonly double _targetBlockTime;
        private readonly int _adjustmentInterval = 2;
        private const double _miningDurationTolerance = 2.0;
        private readonly decimal _miningReward = 50m;
        public BlockChainService(double targetBlockTime = 5)
        {
            _targetBlockTime = targetBlockTime;
            Chain = new List<Block>();
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            _transactionService = new TransactionService(this);
            Difficulty = 1;
            CreateGenesisBlock();
        }

        private void CreateGenesisBlock()
        {
            var genesisBlock = new Block(0, DateTime.UtcNow, new List<Transaction>(), "0", Difficulty);
            _miningService.MineBlock(genesisBlock, Difficulty);
            Chain.Add(genesisBlock);
        }

        public async Task<bool> MineBlockAsync(List<Transaction> transactions, string miningAddress,
            CancellationToken cancellationToken = default)
        {
            //var (included, weight) = FitToByteLimit(transactions);

            var spentWallet = new WalletService(Chain);
            var tempBalances = new Dictionary<string, decimal>();

            foreach (var transaction in transactions)
            {
                if (!_transactionService.ValidateTransaction(transaction).IsValid)
                {
                    throw new InvalidOperationException("Invalid Transaction");
                }

                if (transaction.From != "COINBASE")
                {
                    if (!tempBalances.TryGetValue(transaction.From, out var bal))
                    {
                        bal = spentWallet.GetBalance(transaction.From);
                    }

                    if (bal < transaction.Amount)
                    {
                        throw new InvalidOperationException(
                            $"Double spend detected: {transaction.From} has {bal}, tried to spend {transaction.Amount}");
                    }

                    tempBalances[transaction.From] = bal - transaction.Amount;
                }
            }

            AdjustDifficulty();

            var lastBlock = Chain.Last();
            var newBlock = new Block(lastBlock.Index + 1, DateTime.UtcNow, transactions, lastBlock.Hash, Difficulty);

            Console.WriteLine($"\n[Blockchain] Adding block ...");

            decimal remainingSupply = MaxSupply - TotalMinted;
            decimal rewardAmount = remainingSupply >= _miningReward ? _miningReward : Math.Max(0, remainingSupply);

            if (rewardAmount > 0)
            {
                var reward = new Transaction("COINBASE", miningAddress, rewardAmount, new byte[0]);
                transactions.Add(reward);
                TotalMinted += rewardAmount;
            }
            else
            {
                Console.WriteLine("[Blockchain] MaxSupply reached — mining without reward.");
            }

            var result = await _miningService.MineBlockAsync(newBlock, Difficulty, cancellationToken);

            if (result == null)
                return false;

            Chain.Add(newBlock);
            return true;
        }

        public void MineBlock(List<Transaction> transactions, string miningAddress)
        {
            MineBlockAsync(transactions, miningAddress).GetAwaiter().GetResult();
        }

        public void ProcessTransactions(List<Transaction> incomingTransactions, string miningAddress)
        {
            var batch = new List<Transaction>();
            int weight = 0;
            int blockCount = 0;

            void FlushBatch()
            {
                if (batch.Count == 0) return;
                MineBlock(new List<Transaction>(batch), miningAddress);
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

        public bool ValidateEconomy()
        {
            var addresses = new HashSet<string>();
            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.From != "COINBASE") addresses.Add(tx.From);
                    addresses.Add(tx.To);
                }
            }

            var auditWallet = new WalletService(Chain);
            decimal totalOnWallets = addresses.Sum(addr => auditWallet.GetBalance(addr));

            Console.WriteLine($"[Audit] Unique addresses: {addresses.Count}, Sum on wallets: {totalOnWallets}, TotalMinted: {TotalMinted}");
            return totalOnWallets == TotalMinted;
        }


    }
}