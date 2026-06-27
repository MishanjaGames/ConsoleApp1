using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class BlockChainService
    {
        private readonly MiningService _miningService;
        private readonly HashingService _hashingService;
        private readonly TransactionService _transactionService;
        private readonly WalletService _walletService;
        private readonly FileStorageService _storageService;
        private readonly LoggingService _log;

        public List<Block> Chain { get; set; } = new();
        public List<Transaction> PendingTransactions { get; set; } = new();
        public int Difficulty { get; private set; } = 1;
        public int MaxBlockSizeBytes { get; } = 10240;
        public int MaxMempoolSize { get; } = 5;
        public decimal MaxSupply { get; } = 1000;
        public decimal TotalMinted { get; private set; } = 0;

        private const double MiningDurationTolerance = 2.0;
        private readonly double _targetBlockTime;
        private readonly int _adjustmentInterval = 2;
        private readonly decimal _miningReward = 50m;
        private readonly int _halvingInterval = 5;

        public BlockChainService(LoggingService log, double targetBlockTime = 5)
        {
            _log = log;
            _targetBlockTime = targetBlockTime;
            _storageService = new FileStorageService();
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            _transactionService = new TransactionService(this);
            _walletService = new WalletService(Chain);

            LoadOrInit();
        }

        // ── Init ─────────────────────────────────────────────────────

        private void LoadOrInit()
        {
            var loaded = _storageService.LoadBlockchain();
            if (loaded is { Count: > 0 })
            {
                Chain = loaded;
                if (!IsValid())
                {
                    _log.Error("Blockchain", "Corrupted chain — attempting backup restore");
                    PrintError("⚠️  Blockchain corrupted! Attempting backup restore...");
                    _storageService.ArchiveCorrupted();
                    loaded = _storageService.TryLoadBackup();
                    Chain = loaded ?? new List<Block>();
                    if (Chain.Count == 0 || !IsValid())
                    {
                        _log.Error("Blockchain", "Backup also invalid — starting fresh");
                        Chain = new List<Block>();
                        CreateGenesisBlock();
                    }
                }
                else
                {
                    _log.Info("Blockchain", $"Loaded {Chain.Count} blocks from disk");
                }
            }
            else
            {
                _log.Info("Blockchain", "No chain found — creating genesis");
                CreateGenesisBlock();
            }
        }

        private void CreateGenesisBlock()
        {
            var genesis = new Block(0, DateTime.Parse("01.01.1990"), new List<Transaction>(), "0", Difficulty);
            _miningService.MineBlock(genesis, Difficulty);
            Chain.Add(genesis);
            _storageService.SaveBlockchain(Chain);
            _log.Info("Blockchain", $"Genesis block created: {genesis.Hash[..16]}…");
        }

        // ── Mining ───────────────────────────────────────────────────

        public async Task<Block?> MineBlockAsync(string miningAddress, CancellationToken cancellationToken = default)
        {
            ValidatePendingTransactions();
            CheckDoubleSpend();
            AdjustDifficulty();

            decimal minerSubsidy = GetMinerReward();
            decimal totalFees = PendingTransactions.Sum(tx => tx.Fee);
            decimal rewardAmount = Math.Min(minerSubsidy, MaxSupply - TotalMinted);

            var included = PendingTransactions.OrderByDescending(tx => tx.Fee).ToList();

            if (rewardAmount > 0)
            {
                var coinbase = new Transaction("COINBASE", miningAddress, rewardAmount + totalFees, Array.Empty<byte>());
                included.Insert(0, coinbase);
                TotalMinted += rewardAmount;
            }
            else
            {
                Console.WriteLine("[Blockchain] MaxSupply reached — no reward.");
                _log.Warn("Blockchain", "MaxSupply reached — no coinbase reward");
            }

            var last = Chain.Last();
            var newBlock = new Block(last.Index + 1, DateTime.UtcNow, included, last.Hash, Difficulty);

            Console.WriteLine("\n[Blockchain] Mining block...");
            _log.Info("Mining", $"Starting block #{newBlock.Index} diff={Difficulty}");
            var result = await _miningService.MineBlockAsync(newBlock, Difficulty, cancellationToken);
            if (result == null) return null;

            Chain.Add(newBlock);
            PendingTransactions.RemoveAll(tx => included.Contains(tx));
            _storageService.SaveBlockchain(Chain);
            _log.Info("Mining", $"Block #{newBlock.Index} mined nonce={newBlock.Nonce} hash={newBlock.Hash[..16]}… in {newBlock.MiningDuration:F2}s");
            return newBlock;
        }

        public void MineBlock(string miningAddress) =>
            MineBlockAsync(miningAddress).GetAwaiter().GetResult();

        // ── Mempool ──────────────────────────────────────────────────

        public void AddTransactionToMempool(Transaction tx)
        {
            var (isValid, error) = _transactionService.ValidateTransaction(tx);
            if (!isValid)
            {
                _log.Warn("Mempool", $"Rejected tx: {error}");
                throw new InvalidOperationException($"Invalid transaction: {error}");
            }

            if (tx.From != "COINBASE")
            {
                decimal pending = GetPendingBalance(tx.From);
                if (pending < tx.Amount + tx.Fee)
                {
                    _log.Warn("Mempool", $"Insufficient funds from {tx.From[..16]}…: has {pending} needs {tx.Amount + tx.Fee}");
                    throw new InvalidOperationException($"Insufficient funds (pending): has {pending}, needs {tx.Amount + tx.Fee}");
                }

                var existing = PendingTransactions.FirstOrDefault(t =>
                    t.From == tx.From && t.To == tx.To && t.Amount == tx.Amount);

                if (existing != null)
                {
                    if (tx.Fee <= existing.Fee)
                        throw new InvalidOperationException("Duplicate tx. Increase fee to replace (RBF).");

                    PendingTransactions.Remove(existing);
                    PendingTransactions.Add(tx);
                    _log.Info("Mempool", $"RBF: replaced tx with fee={tx.Fee}");
                    Console.WriteLine("[Mempool] RBF: transaction replaced with higher fee.");
                    return;
                }
            }

            if (PendingTransactions.Count >= MaxMempoolSize)
            {
                var cheapest = PendingTransactions.MinBy(t => t.Fee)!;
                if (tx.Fee <= cheapest.Fee)
                    throw new InvalidOperationException("Mempool full. Increase fee.");

                PendingTransactions.Remove(cheapest);
                _log.Info("Mempool", $"Evicted tx fee={cheapest.Fee}");
                Console.WriteLine($"[Mempool] Evicted tx with fee={cheapest.Fee}.");
            }

            PendingTransactions.Add(tx);
            _log.Info("Mempool", $"Added tx {tx.Id[..12]}… {tx.From[..Math.Min(12, tx.From.Length)]}→{tx.To[..Math.Min(12, tx.To.Length)]} amount={tx.Amount} fee={tx.Fee}");
        }

        // ── Validation ───────────────────────────────────────────────

        public bool IsValid()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var cur = Chain[i];
                var prev = Chain[i - 1];

                if (cur.Hash != _hashingService.ComputeHash(cur)) return false;
                if (cur.PreviousHash != prev.Hash) return false;

                string prefix = _miningService.UseDifficulty
                    ? new string('0', Difficulty)
                    : _miningService.VanityTarget;
                if (!cur.Hash.StartsWith(prefix)) return false;
                if (cur.MiningDuration < 0) return false;
                if (cur.TimeStamp <= prev.TimeStamp) return false;

                double physicalDiff = (cur.TimeStamp - prev.TimeStamp).TotalSeconds;
                if (cur.MiningDuration > physicalDiff + MiningDurationTolerance) return false;

                foreach (var tx in cur.Transactions)
                {
                    if (tx.From == "COINBASE") continue;
                    if (!_walletService.VerifySignature(tx.SenderPublicKey, tx.GetDataToSign(), tx.Signature))
                    {
                        _log.Error("Blockchain", $"Forged tx in block #{cur.Index}!");
                        PrintError($"[THREAT] Forged transaction in block {cur.Index}!");
                        return false;
                    }
                }
            }
            return true;
        }

        public int GetInvalidBlockIndex()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var cur = Chain[i]; var prev = Chain[i - 1];
                if (cur.Hash != _hashingService.ComputeHash(cur)) return cur.Index;
                if (cur.PreviousHash != prev.Hash) return cur.Index;
                if (cur.MiningDuration < 0) return cur.Index;
                if (cur.TimeStamp <= prev.TimeStamp) return cur.Index;
                double pd = (cur.TimeStamp - prev.TimeStamp).TotalSeconds;
                if (cur.MiningDuration > pd + MiningDurationTolerance) return cur.Index;
            }
            return -1;
        }

        public bool ValidateEconomy()
        {
            var addresses = Chain
                .SelectMany(b => b.Transactions)
                .SelectMany(tx => new[] { tx.From, tx.To })
                .Where(a => a != "COINBASE")
                .ToHashSet();

            var audit = new WalletService(Chain);
            decimal total = addresses.Sum(audit.GetBalance);
            Console.WriteLine($"[Audit] Addresses: {addresses.Count} | On wallets: {total} | Minted: {TotalMinted}");
            _log.Info("Audit", $"Addresses: {addresses.Count} | Wallets: {total} | Minted: {TotalMinted}");
            return total == TotalMinted;
        }

        // ── Queries ──────────────────────────────────────────────────

        public Block? FindBlockByHash(string hash) =>
            Chain.FirstOrDefault(b => b.Hash == hash);

        public decimal GetPendingBalance(string address)
        {
            decimal balance = _walletService.GetBalance(address);
            foreach (var tx in PendingTransactions)
                if (tx.From == address)
                    balance -= tx.Amount + tx.Fee;
            return balance;
        }

        // ── Private helpers ──────────────────────────────────────────

        private void AdjustDifficulty()
        {
            var recent = Chain.Skip(Math.Max(0, Chain.Count - _adjustmentInterval)).ToList();
            double avg = recent.Average(b => b.MiningDuration);
            int prev = Difficulty;
            Difficulty = avg < _targetBlockTime ? Difficulty + 1 : Math.Max(1, Difficulty - 1);
            if (Difficulty != prev)
                _log.Info("Mining", $"Difficulty adjusted {prev}→{Difficulty} (avg={avg:F2}s)");
        }

        private decimal GetMinerReward()
        {
            int halvings = (Chain.Count - 1) / _halvingInterval;
            return _miningReward / (decimal)Math.Pow(2, halvings);
        }

        private void ValidatePendingTransactions()
        {
            foreach (var tx in PendingTransactions)
            {
                var (isValid, error) = _transactionService.ValidateTransaction(tx);
                if (!isValid) throw new InvalidOperationException($"Invalid tx in mempool: {error}");
            }
        }

        private void CheckDoubleSpend()
        {
            var balances = new Dictionary<string, decimal>();
            var wallet = new WalletService(Chain);
            foreach (var tx in PendingTransactions.Where(t => t.From != "COINBASE"))
            {
                if (!balances.TryGetValue(tx.From, out var bal))
                    bal = wallet.GetBalance(tx.From);
                if (bal < tx.Amount)
                    throw new InvalidOperationException($"Double spend: {tx.From} has {bal}, tried {tx.Amount}");
                balances[tx.From] = bal - tx.Amount;
            }
        }

        private static void PrintError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
    }
}