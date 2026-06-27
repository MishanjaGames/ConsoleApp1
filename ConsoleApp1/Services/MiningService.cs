using System.Diagnostics;
using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class MiningService
    {
        private readonly HashingService _hashingService;
        public bool UseDifficulty { get; } = true;
        public string VanityTarget { get; set; } = "QWERTY123";

        public MiningService(HashingService hashingService) => _hashingService = hashingService;

        public async Task<long?> MineBlockAsync(Block block, int difficulty = 4, CancellationToken cancellationToken = default)
        {
            string target = UseDifficulty ? new string('0', difficulty) : VanityTarget;
            int threadCount = Math.Max(1, Environment.ProcessorCount / 2);

            Console.WriteLine(UseDifficulty
                ? $"[Mining] {threadCount} threads | difficulty: {difficulty}"
                : $"[Mining] {threadCount} threads | vanity: \"{target}\"");

            var sw = Stopwatch.StartNew();
            long winnerNonce = 0;
            string? winnerHash = null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = cts.Token;

            var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
            {
                var localBlock = block.Clone(difficulty);
                var localHasher = new HashingService();

                for (long nonce = threadIndex; nonce <= int.MaxValue; nonce += threadCount)
                {
                    if (token.IsCancellationRequested) return;

                    localBlock.Nonce = nonce;
                    string hash = localHasher.ComputeHash(localBlock);

                    if (hash.StartsWith(target))
                    {
                        if (Interlocked.CompareExchange(ref winnerHash, hash, null) == null)
                        {
                            Interlocked.Exchange(ref winnerNonce, nonce);
                            Console.WriteLine($"\n[Thread {threadIndex}] Done! nonce={nonce} hash={hash[..12]}...");
                            cts.Cancel();
                        }
                        return;
                    }

                    if (nonce % (100_000 * threadCount) == 0)
                        Console.Write(".");
                }
            }, token)).ToArray();

            try { await Task.WhenAll(tasks); }
            catch (OperationCanceledException) { }

            sw.Stop();
            block.MiningDuration = sw.Elapsed.TotalSeconds;

            if (cancellationToken.IsCancellationRequested && winnerHash == null)
            {
                Console.WriteLine("\n[Mining] Canceled — another node was faster.");
                return null;
            }

            block.Nonce = winnerNonce;
            block.Hash = winnerHash!;
            Console.WriteLine($"[Mining] Done in {sw.Elapsed.TotalSeconds:F2}s | nonce={block.Nonce}");
            return winnerNonce;
        }

        public long MineBlock(Block block, int difficulty = 4) =>
            MineBlockAsync(block, difficulty).GetAwaiter().GetResult() ?? -1;
    }
}