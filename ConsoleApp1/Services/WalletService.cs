using System.Security.Cryptography;
using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class WalletService
    {
        private readonly List<Block> _blockchain;

        public WalletService(List<Block> blockchain) => _blockchain = blockchain;

        public Wallet CreateWallet(string name)
        {
            using var ecdsa = ECDsa.Create();
            byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();
            byte[] privateKey = ecdsa.ExportECPrivateKey();
            return new Wallet() { Name = name, Address = Convert.ToBase64String(publicKey), PublicKey = publicKey, EncryptedPrivateKey = privateKey };
        }

        public bool VerifySignature(byte[] publicKey, byte[] data, byte[] signature)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
        }

        public decimal GetBalance(string address)
        {
            decimal balance = 0;
            foreach (var block in _blockchain)
                foreach (var tx in block.Transactions)
                {
                    if (tx.To == address)
                        balance += tx.Amount;
                    else if (tx.From == address)
                        balance -= tx.Amount + tx.Fee;
                }
            return balance;
        }
    }
}