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
        private readonly TCPP2PService _p2pService;
        public List<Block> Chain { get; set; }
        public List<Transaction> PendingTransactions { get; set; } = new List<Transaction>();
        public int Difficulty { get; private set; }
        public int MaxBlockSizeBytes { get; } = 10240;
        public decimal MaxSupply { get; } = 1000;
        public decimal TotalMinted { get; private set; } = 0;

        private readonly double _targetBlockTime;
        private readonly int _adjustmentInterval = 2;
        private const double _miningDurationTolerance = 2.0;
        private readonly decimal _miningReward = 50m;
        private readonly decimal maxTransactionAmount = 2m;
        private readonly int howingInterval = 5;
        public BlockChainService(string uname, int p, double targetBlockTime = 5)
        {
            Chain = new List<Block>();
            _storageService = new FileStorageService(uname, p);
            _targetBlockTime = targetBlockTime;
            _hashingService = new HashingService();
            _miningService = new MiningService(_hashingService);
            _transactionService = new TransactionService(this);
            _walletService = new WalletService(Chain);
            Difficulty = 1;


            var loadedChain = _storageService.LoadBlockchain();
            if (loadedChain != null && loadedChain.Count > 0)
            {
                Chain = loadedChain;

                if (!this.IsValid())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("⚠️ CRITICAL: File is corrupted!");
                    Console.ResetColor();

                    _storageService.fixBackup();

                    loadedChain = _storageService.useBackup();
                    Chain = loadedChain;
                    if (!this.IsValid())
                    {
                        Chain = new List<Block>();
                        CreateGenesisBlock();
                    }
                }
            }
            else { CreateGenesisBlock(); }

        }

        private void CreateGenesisBlock()
        {
            var genesisBlock = new Block(0, DateTime.UtcNow, new List<Transaction>(), "0", Difficulty);
            _miningService.MineBlock(genesisBlock, Difficulty);
            Chain.Add(genesisBlock);
            _storageService.SaveBlockchain(Chain);
        }

        public async Task<Block> MineBlockAsync(string miningAddress,
            CancellationToken cancellationToken = default)
        {
            //var (included, weight) = FitToByteLimit(transactions);

            var spentWallet = new WalletService(Chain);
            var tempBalances = new Dictionary<string, decimal>();

            var sortedTransactions = PendingTransactions.OrderByDescending(tx => tx.Fee).ToList();
            var totalFees = sortedTransactions.Sum(tx => tx.Fee);
            var minerSubsidy = GetMinerReward();
            var totalreward = totalFees + minerSubsidy;

            foreach (var transaction in PendingTransactions)
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

            decimal remainingSupply = MaxSupply - TotalMinted;
            decimal rewardAmount = remainingSupply >= minerSubsidy ? minerSubsidy : Math.Max(0, remainingSupply);

            var includedTransactions = sortedTransactions.ToList();
            Transaction rewardTx = null;
            if (rewardAmount > 0)
            {
                rewardTx = new Transaction("COINBASE", miningAddress, totalreward, new byte[0]);
                includedTransactions.Insert(0, rewardTx);
                TotalMinted += rewardAmount;
            }
            else
            {
                Console.WriteLine("[Blockchain] MaxSupply reached — mining without reward.");
            }

            var lastBlock = Chain.Last();
            var newBlock = new Block(lastBlock.Index + 1, DateTime.UtcNow, includedTransactions, lastBlock.Hash, Difficulty);

            Console.WriteLine($"\n[Blockchain] Adding block ...");

            var result = await _miningService.MineBlockAsync(newBlock, Difficulty, cancellationToken);

            if (result == null)
                return null;

            Chain.Add(newBlock);
            PendingTransactions.RemoveAll(tx => sortedTransactions.Contains(tx));
            _storageService.SaveBlockchain(Chain);
            return newBlock;
        }

        public void MineBlock(string miningAddress)
        {
            MineBlockAsync(miningAddress).GetAwaiter().GetResult();
        }

        public void ProcessTransactions(List<Transaction> incomingTransactions, string miningAddress)
        {
            var batch = new List<Transaction>();
            int weight = 0;
            int blockCount = 0;

            void FlushBatch()
            {
                if (batch.Count == 0) return;
                MineBlock(miningAddress);
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

                AddTransactionToMempool(tx);
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
                if (!cur.Hash.StartsWith((_miningService.useDifficulty) ? new string('0', Difficulty) : _miningService.VanityTarget)) return false;

                if (cur.MiningDuration < 0) return false;

                if (cur.TimeStamp <= prev.TimeStamp) return false;

                double physicalDiff = (cur.TimeStamp - prev.TimeStamp).TotalSeconds;
                if (cur.MiningDuration > physicalDiff + _miningDurationTolerance) return false;

                foreach (var tx in cur.Transactions)
                {
                    if (tx.From == "COINBASE") continue;
                    if (!_walletService.VerifySignature(tx.SenderPublicKey, tx.GetDataToSign(), tx.Signature))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CRITICAL]: Found corrupted transaction in {cur.Index}!");
                        Console.ResetColor();
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

        public int MaxMempoolSize { get; } = 5;

        public decimal GetPendingBalance(string address)
        {
            decimal balance = _walletService.GetBalance(address);
            foreach (var tx in PendingTransactions)
            {
                if (tx.From == address)
                    balance -= (tx.Amount + tx.Fee);
            }
            return balance;
        }

        public void AddTransactionToMempool(Transaction transaction)
        {
            var (isValid, error) = _transactionService.ValidateTransaction(transaction);
            if (!isValid)
                throw new InvalidOperationException($"Invalid transaction: {error}");

            if (transaction.From != "COINBASE")
            {
                var pendingBalance = GetPendingBalance(transaction.From);
                if (pendingBalance < transaction.Amount + transaction.Fee)
                    throw new InvalidOperationException($"Insufficient funds (pending): {transaction.From} has {pendingBalance}, tried to spend {transaction.Amount + transaction.Fee}");

                var existing = PendingTransactions.FirstOrDefault(tx =>
                    tx.From == transaction.From &&
                    tx.To == transaction.To &&
                    tx.Amount == transaction.Amount);

                if (existing != null)
                {
                    if (transaction.Fee > existing.Fee)
                    {
                        PendingTransactions.Remove(existing);
                        PendingTransactions.Add(transaction);
                        Console.WriteLine("Transaction has been updated with higher fee!");
                        return;
                    }
                    else
                    {
                        throw new InvalidOperationException("A similar transaction already exists. Increase fee to replace.");
                    }
                }
            }

            // Part 1: Mempool eviction
            if (PendingTransactions.Count >= MaxMempoolSize)
            {
                var cheapest = PendingTransactions.OrderBy(tx => tx.Fee).First();
                if (transaction.Fee > cheapest.Fee)
                {
                    PendingTransactions.Remove(cheapest);
                    Console.WriteLine($"[Mempool] Evicted tx with fee={cheapest.Fee} to make room.");
                }
                else
                {
                    throw new InvalidOperationException("Mempool is full. Fee is too low.");
                }
            }

            PendingTransactions.Add(transaction);
        }

        private decimal GetMinerReward()
        {
            int halvingCount = (Chain.Count - 1) / howingInterval;
            return _miningReward / (decimal)Math.Pow(2, halvingCount);
        }

        public double GetChainWeight(List<Block> incomeChain)
        {
            double weight = 0;
            foreach (var block in incomeChain)
            {
                weight += Math.Pow(2, block.Difficulty);
            }
            return weight;
        }

        public bool IsChainValid(List<Block> externalChain)
        {
            for (int i = 1; i < externalChain.Count; i++)
            {
                Block curr = externalChain[i];
                Block prev = externalChain[i - 1];
                if (curr.Hash != _hashingService.ComputeHash(curr) || curr.PreviousHash != prev.Hash) return false;
            }
            double currweight = GetChainWeight(Chain);
            double externalweight = GetChainWeight(externalChain);
            return externalweight > currweight;
        }

        public bool ResolveConflicts(List<Block> externalChain)
        {
            if (IsChainValid(externalChain))
            {
                var currWork = GetChainWeight(Chain);
                var extWork = GetChainWeight(externalChain);

                if (extWork <= currWork)
                {
                    Console.WriteLine("[Blockchain] Received chain is not heavier. Ignoring.");
                    return false;
                }

                int commonIndex = Math.Min(Chain.Count, externalChain.Count) - 1;
                for (int i = Math.Min(Chain.Count, externalChain.Count) - 1; i >= 0; i--)
                {
                    if (Chain[i].Hash == externalChain[i].Hash) { commonIndex = i; break; }
                }

                var orphanedTransactions = new List<Transaction>();
                for (int i = commonIndex + 1; i < Chain.Count; i++)
                {
                    orphanedTransactions.AddRange(Chain[i].Transactions.Where(tx => tx.From != "COINBASE"));
                }

                Chain = externalChain;
                _storageService.SaveBlockchain(Chain);

                var auditWallet = new WalletService(Chain);
                var tempBalances = new Dictionary<string, decimal>();
                foreach (var tx in orphanedTransactions)
                {
                    if (!tempBalances.TryGetValue(tx.From, out var bal))
                        bal = auditWallet.GetBalance(tx.From);

                    if (bal >= tx.Amount + tx.Fee)
                    {
                        tempBalances[tx.From] = bal - (tx.Amount + tx.Fee);
                        PendingTransactions.Add(tx);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[SECURITY] 🚨 Denied return of {tx.Id} into mempool (Doubled payment / Insufficient funds)!");
                        Console.ResetColor();
                    }
                }

                return true;
            }
            return false;
        }
    }
}