using System.Text.Json;
using BlockChain_01.Models;
using BlockChain_01.Services;

namespace BlockChain_01.Services
{
    public class FileStorageService
    {
        private const string ChainFile = "blockchain_data.json";
        private const string WalletsFile = "wallets_data.json";
        private const string PeerWalletsFile = "peer_wallets.json";
        private const string BackupFile = "blockchain_backup.json";
        private const string CorruptedFile = "blockchain_corrupted.json";

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        // ── Blockchain ───────────────────────────────────────────────

        public void SaveBlockchain(List<Block> chain)
        {
            if (File.Exists(ChainFile))
                File.Copy(ChainFile, BackupFile, overwrite: true);
            File.WriteAllText(ChainFile, JsonSerializer.Serialize(chain, JsonOptions));
            Console.WriteLine("[Storage] Blockchain saved.");
        }

        public List<Block>? LoadBlockchain()
        {
            Console.WriteLine("[Storage] Loading blockchain...");
            return TryLoad<List<Block>>(ChainFile) ?? TryLoadBackup();
        }

        public void ArchiveCorrupted() =>
            File.Move(ChainFile, CorruptedFile, overwrite: true);

        public List<Block>? TryLoadBackup()
        {
            Console.WriteLine("[Storage] Trying backup...");
            if (!File.Exists(BackupFile)) { Warn("No backup found. Starting fresh."); return null; }
            var result = TryLoad<List<Block>>(BackupFile);
            if (result != null) Console.WriteLine("[Storage] Restored from backup.");
            return result;
        }

        // ── Local wallets (encrypted private keys) ───────────────────

        public void SaveWallets(IEnumerable<Wallet> wallets)
        {
            File.WriteAllText(WalletsFile, JsonSerializer.Serialize(wallets.ToList(), JsonOptions));
            Console.WriteLine("[Storage] Wallets saved.");
        }

        public List<Wallet> LoadWallets() =>
            TryLoad<List<Wallet>>(WalletsFile) ?? new List<Wallet>();

        // ── Peer wallet registry ─────────────────────────────────────

        public void SavePeerWallets(List<PeerWalletInfo> wallets) =>
            File.WriteAllText(PeerWalletsFile, JsonSerializer.Serialize(wallets, JsonOptions));

        public List<PeerWalletInfo> LoadPeerWallets() =>
            TryLoad<List<PeerWalletInfo>>(PeerWalletsFile) ?? new List<PeerWalletInfo>();

        // ── Helpers ──────────────────────────────────────────────────

        private static T? TryLoad<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)); }
            catch { return null; }
        }

        private static void Warn(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Storage] {msg}");
            Console.ResetColor();
        }
    }
}