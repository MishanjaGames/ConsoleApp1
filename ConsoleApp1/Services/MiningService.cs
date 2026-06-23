using BlockChain_01.Models;
using System.Diagnostics;

namespace BlockChain_01.Services
{
    public class MiningService
    {
        private readonly HashingService _hashingService;
        public string VanityTarget { get; set; } = "QWERTY123";
        public bool useDifficulty { get; } = true;

        public MiningService(HashingService hashingService)
        {
            _hashingService = hashingService;
        }

        public async Task<long?> MineBlockAsync(Block block, int difficulty = 4,
            CancellationToken cancellationToken = default)
        {
            string target = string.Empty;
            int threadCount = Environment.ProcessorCount / 2;
            if (!useDifficulty)
            {
                target = VanityTarget;
                Console.WriteLine($"[Mining] Launching {threadCount} threads, using vanity target: \"{target}\" ...");
            }
            else
            {
                target = new string('0', difficulty);
                Console.WriteLine($"[Mining] Launching {threadCount} threads, using difficulty: {difficulty} ...");
            }

            Stopwatch sw = Stopwatch.StartNew();

            long winnerNonce = 0;
            string? winnerHash = null;

            using var internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken token = internalCts.Token;

            var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
            {
                Block localBlock = block.Clone(difficulty);

                var localHashingService = new HashingService();

                for (long nonce = threadIndex; nonce <= int.MaxValue; nonce += threadCount)
                {
                    if (token.IsCancellationRequested)
                        return;

                    localBlock.Nonce = (int)nonce;
                    string hash = localHashingService.ComputeHash(localBlock);

                    if (hash.StartsWith(target))
                    {
                        if (Interlocked.CompareExchange(ref winnerHash, hash, null) == null)
                        {
                            Interlocked.Exchange(ref winnerNonce, nonce);
                            Console.WriteLine($"\n[Thread {threadIndex}] Generating complete! Attempts={nonce}, Result Hash={hash[..12]}...");
                            internalCts.Cancel();
                        }
                        return;
                    }

                    if (nonce % (100_000 * threadCount) == 0)
                        Console.Write(".");
                }
            }, token)).ToArray();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }

            sw.Stop();
            block.MiningDuration = sw.Elapsed.TotalSeconds;

            if (cancellationToken.IsCancellationRequested && winnerHash == null)
            {
                Console.WriteLine("\n[Mining] X Block canceled by network — other node has generated it earlier.");
                return null;
            }

            block.Nonce = (int)winnerNonce!;
            block.Hash = winnerHash!;

            Console.WriteLine($"[Mining] Block completed {sw.Elapsed.TotalSeconds}s | Attempts={block.Nonce}");
            return winnerNonce;
        }

        public long MineBlock(Block block, int difficulty = 4)
        {
            return MineBlockAsync(block, difficulty).GetAwaiter().GetResult() ?? -1;
        }
    }
}