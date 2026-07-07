using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class FileStorageService
    {
        private readonly string _blockchainFilePath = "bchain_dat.json";
        private readonly string _walletsFilePath = "wallets_data.json";

        private readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        private readonly string _backupFilePath = "bchain_backup.json";
        private readonly string _corruptedFilePath = "bchain_corrupted.json";

        private string username {  get; set; }
        private int port { get; set; }

        public FileStorageService(string username, int port)
        {
            this.username = username;
            this.port = port;
            _blockchainFilePath = $"bchain_{username}_{port}.json";
            _walletsFilePath = $"wallets_{username}_{port}.json";
            _backupFilePath = $"blockchain_backup_{username}_{port}.json";
        }

        public void SaveBlockchain(List<Block> blockchain)
        {
            Console.WriteLine($"Saving blockchain.");
            // Part 4: backup current file before overwriting
            if (File.Exists(_blockchainFilePath))
                File.Copy(_blockchainFilePath, _backupFilePath, overwrite: true);

            var json = JsonSerializer.Serialize(blockchain, jsonSerializerOptions);
            File.WriteAllText(_blockchainFilePath, json);
            Console.WriteLine($"Updates Saved");
        }

        public void fixBackup() {
            File.Move(_blockchainFilePath, _corruptedFilePath, overwrite: true);
        }

        public List<Block>? useBackup()
        {
            // Part 4: try backup
            Console.WriteLine("🔄 Спроба відновлення бази даних з резервної копії...");
            if (File.Exists(_backupFilePath))
            {
                var backupJson = File.ReadAllText(_backupFilePath);
                var list = JsonSerializer.Deserialize<List<Block>>(backupJson);
                if (list == null) { return null; }
                if (list != null && list.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✅ Блокчейн успішно відновлено з резервної копії!");
                    Console.ResetColor();
                    return list;
                }
                else
                {
                    return null;
                }
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Резервна копія відсутня.");
            Console.WriteLine("Очікуємо блокчейн від інших користувачів");
            Console.WriteLine("Користувачів не знайдено");
            Console.WriteLine("Створення нового блокчейну");
            Console.ResetColor();
            return null;
        }

        public List<Block>? LoadBlockchain()
        {
            Console.WriteLine($"Reading blockchain.");

            if (!File.Exists(_blockchainFilePath)) {
                var backupList = useBackup();
                return backupList;
            }
            var json = File.ReadAllText(_blockchainFilePath);
            var list = JsonSerializer.Deserialize<List<Block>>(json);
            if (list == null) {
                var backupList = useBackup();
                return backupList;
            }
            if (list != null && list.Count > 0)
            {
                Console.WriteLine($"Blockchain loaded successfully.");
                Console.WriteLine($"Applying updated");
                return list;
            }
            else
            {
                var backupList = useBackup();
                return backupList;
            }
        }

        public void SaveWallets(List<Wallet> wallets)
        {
            Console.WriteLine($"Saving Wallets");
            var json = JsonSerializer.Serialize(wallets, jsonSerializerOptions);
            File.WriteAllText(_walletsFilePath, json);
            Console.WriteLine($"Updates Saved");
        }

        public List<Wallet> LoadWallets()
        {
            Console.WriteLine($"Reading Wallets");
            if (!File.Exists(_walletsFilePath))
                return new List<Wallet>();
            var json = File.ReadAllText(_walletsFilePath);
            Console.WriteLine($"Applying updated");
            return JsonSerializer.Deserialize<List<Wallet>>(json) ?? new List<Wallet>();
        }
    }
}