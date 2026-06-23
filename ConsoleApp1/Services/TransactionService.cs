using BlockChain_01.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace BlockChain_01.Services
{
    public class TransactionService
    {
        private readonly WalletService _walletService;
        private readonly BlockChainService _blockchain;
        private static readonly Regex AddressPattern = new Regex(@"^0x[a-zA-Z0-9]{40}$", RegexOptions.Compiled);

        public TransactionService(BlockChainService blockchain)
        {
            _blockchain = blockchain;
            _walletService = new WalletService(blockchain.Chain);
        }

        public Transaction CreateTransaction(Wallet walletFrom, string to, decimal amount, byte[] senderPublicKey)
        {

            var ballance = _walletService.GetBalance(walletFrom.Address);
            if (ballance < amount)
            {
                if (walletFrom.Name != "COINBASE")
                {
                    Console.WriteLine($"Insufficient funds: {ballance} < {amount}");
                    return null;
                    //throw new ArgumentException($"Insufficient funds: {ballance} < {amount}")
                }
            }

            // For COINBASE transactions, use the wallet name as the From field instead of address
            string fromField = walletFrom.Name == "COINBASE" ? "COINBASE" : walletFrom.Address;
            var tx = new Transaction(fromField, to, amount, senderPublicKey);
            tx.Signature = walletFrom.Sign(tx.GetDataToSign());
            var valid = ValidateTransaction(tx);
            if (!valid.IsValid)
            {
                throw new ArgumentException(valid.ErrorMessage);
            }

            return tx;
        }

        public (bool IsValid, string ErrorMessage) ValidateTransaction(Transaction transaction)
        {
            if (transaction == null) { return (false, "Transaction is null"); }
            if (string.IsNullOrEmpty(transaction.To)) { return (false, "Field To is null"); }
            if (transaction.From == "COINBASE") { return (true, string.Empty); }
            if (string.IsNullOrEmpty(transaction.From)) { return (false, "Field From is null"); }
            if (!AddressPattern.IsMatch(transaction.From)) { return (false, $"Invalid From address: '{transaction.From}' (must be 0x + 40 alphanumeric chars)"); }
            if (!AddressPattern.IsMatch(transaction.To)) { return (false, $"Invalid To address: '{transaction.To}' (must be 0x + 40 alphanumeric chars)"); }
            if (transaction.Amount <= 0) { return (false, "Field Amount is null"); }
            if (!_walletService.VerifySignature(transaction.SenderPublicKey, transaction.GetDataToSign(), transaction.Signature)) { return (false, "Invalid Signature"); }

            return (true, string.Empty);
        }
    }
}