using BlockChain_01.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChain_01.Services
{
    public class WalletService
    {
        private readonly List<Block> _blockchain;
        public WalletService(List<Block> blockchain)
        {
            _blockchain = blockchain;
        }
        public Wallet CreateWallet(string name)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();

            byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();
            byte[] privateKey = ecdsa.ExportECPrivateKey();

            string address = Convert.ToBase64String(publicKey);
            return new Wallet(name, address, publicKey, privateKey);
        }

        public bool VerifySignature(byte [] publicKey, byte[] data, byte[] signature)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();

            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            return ecdsa.VerifyData(data, signature, System.Security.Cryptography.HashAlgorithmName.SHA256);
        }

        public decimal GetBalance(string address)
        {
            decimal balance = 0;
            foreach (var block in _blockchain)
            {
                foreach (var transaction in block.Transactions)
                {
                    if (transaction.To == address)
                        balance += transaction.Amount;
                    if (transaction.From == address) {
                        balance -= (transaction.Amount+transaction.Fee);
                    }
                }
            }
            return balance;
        }
    }
}
