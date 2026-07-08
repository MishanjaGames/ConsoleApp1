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
        private readonly Func<List<Block>> _blockchainAccessor;

        public WalletService(List<Block> blockchain)
            : this(() => blockchain)
        {
        }

        public WalletService(Func<List<Block>> blockchainAccessor)
        {
            _blockchainAccessor = blockchainAccessor;
        }
        public Wallet CreateWallet(string name)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();

            byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();
            byte[] privateKey = ecdsa.ExportECPrivateKey();

            string address = Convert.ToBase64String(publicKey);
            return new Wallet(name, address, publicKey, privateKey);
        }

        public bool VerifySignature(byte[] publicKey, byte[] data, byte[] signature)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();

            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            return ecdsa.VerifyData(data, signature, System.Security.Cryptography.HashAlgorithmName.SHA256);
        }

        /// <summary>
        /// Full multi-currency portfolio for an address: ticker -> balance.
        /// Token issuance credits the issuer with the full emission.
        /// Network fees are always deducted in BASE, regardless of the transaction's own currency.
        /// </summary>
        public Dictionary<string, decimal> GetPortfolio(string address)
        {
            var balances = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            void Add(string currency, decimal amount)
            {
                balances.TryGetValue(currency, out var current);
                balances[currency] = current + amount;
            }

            var blockchain = _blockchainAccessor();
            foreach (var block in blockchain)
            {
                foreach (var transaction in block.Transactions)
                {
                    if (transaction.Type == TransactionType.IssueToken)
                    {
                        if (transaction.To == address)
                            Add(transaction.Currency, transaction.Amount);
                    }
                    else
                    {
                        if (transaction.To == address)
                            Add(transaction.Currency, transaction.Amount);
                        if (transaction.From == address)
                            Add(transaction.Currency, -transaction.Amount);
                    }

                    // Miners are paid EXCLUSIVELY in BASE, no matter what currency the transaction moved.
                    if (transaction.From == address && transaction.From != "COINBASE" && transaction.Fee != 0)
                        Add("BASE", -transaction.Fee);
                }
            }

            return balances;
        }

        /// <summary>
        /// Balance for a single currency ("BASE" by default, kept for backward compatibility).
        /// </summary>
        public decimal GetBalance(string address, string currency = "BASE")
        {
            var portfolio = GetPortfolio(address);
            return portfolio.TryGetValue(currency, out var balance) ? balance : 0m;
        }
    }
}