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
        public const decimal IcoTax = 100m;
        //private static readonly Regex AddressPattern = new Regex(@"^0x[a-zA-Z0-9]{40}$", RegexOptions.Compiled);

        public TransactionService(BlockChainService blockchain)
        {
            _blockchain = blockchain;
            _walletService = new WalletService(() => blockchain.Chain);
        }

        public Transaction? CreateTransaction(Wallet walletFrom, string to, decimal amount, byte[] senderPublicKey, string currency = "BASE", decimal fee = 1m)
        {
            var effectiveFee = fee <= 0 ? 1m : fee;
            var ballance = _walletService.GetBalance(walletFrom.Address, currency);
            if (ballance < amount)
            {
                if (walletFrom.Name != "COINBASE")
                {
                    Console.WriteLine($"Insufficient {currency} funds: {ballance} < {amount}");
                    return null;
                }
            }

            // For COINBASE transactions, use the wallet name as the From field instead of address
            string fromField = walletFrom.Name == "COINBASE" ? "COINBASE" : walletFrom.Address;
            var tx = new Transaction(fromField, to, amount, senderPublicKey, currency);
            tx.Fee = effectiveFee;
            tx.Signature = walletFrom.Sign(tx.GetDataToSign());
            var valid = ValidateTransaction(tx);
            if (!valid.IsValid)
            {
                throw new ArgumentException(valid.ErrorMessage);
            }

            return tx;
        }

        /// <summary>
        /// Issues a brand-new token/currency (ICO). Costs a fixed 100 BASE tax, paid to the miner.
        /// The full emission is credited to the issuer's own address.
        /// </summary>
        public Transaction? CreateIssueTransaction(Wallet issuer, string currency, decimal totalSupply, byte[] senderPublicKey)
        {
            if (string.IsNullOrWhiteSpace(currency))
            {
                Console.WriteLine("Currency ticker cannot be empty.");
                return null;
            }

            currency = currency.ToUpperInvariant();

            if (currency == "BASE")
            {
                Console.WriteLine("Cannot re-issue the base currency 'BASE'.");
                return null;
            }

            if (_blockchain.CurrencyExists(currency))
            {
                Console.WriteLine($"Ticker squatting rejected: currency '{currency}' already exists.");
                return null;
            }

            var baseBalance = _walletService.GetBalance(issuer.Address, "BASE");
            if (baseBalance < IcoTax)
            {
                Console.WriteLine($"Insufficient BASE for ICO tax: {baseBalance} < {IcoTax}");
                return null;
            }

            var tx = Transaction.CreateIssueTransaction(issuer.Address, currency, totalSupply, senderPublicKey, IcoTax);
            tx.Signature = issuer.Sign(tx.GetDataToSign());

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
            if (transaction.From == "COINBASE") { return (true, string.Empty); }

            if (transaction.Type == TransactionType.IssueToken)
                return ValidateIssueTransaction(transaction);

            return ValidateTransferTransaction(transaction);
        }

        private (bool IsValid, string ErrorMessage) ValidateIssueTransaction(Transaction transaction)
        {
            if (string.IsNullOrEmpty(transaction.Currency)) { return (false, "Currency ticker is null"); }
            if (transaction.Currency.Equals("BASE", StringComparison.OrdinalIgnoreCase))
                return (false, "Cannot re-issue the base currency 'BASE'");
            if (transaction.TotalSupply == null || transaction.TotalSupply <= 0)
                return (false, "TotalSupply must be positive");
            if (transaction.Fee != IcoTax)
                return (false, $"ICO tax must be exactly {IcoTax} BASE");
            if (string.IsNullOrEmpty(transaction.From)) { return (false, "Field From is null"); }
            if (transaction.From != transaction.To)
                return (false, "Issued supply must be credited to the issuer's own address");

            // Anti-plagiarism: the ticker must not already exist in chain history (or in-flight in the mempool).
            if (_blockchain.CurrencyExists(transaction.Currency))
                return (false, $"Currency '{transaction.Currency}' already exists (ticker squatting rejected)");

            if (!_walletService.VerifySignature(transaction.SenderPublicKey, transaction.GetDataToSign(), transaction.Signature))
                return (false, "Invalid Signature");

            // ICO tax: issuer must actually have 100 BASE, or the issuance is cancelled.
            var baseBalance = _walletService.GetBalance(transaction.From, "BASE");
            if (baseBalance < transaction.Fee)
                return (false, $"Insufficient BASE for ICO tax: {baseBalance} < {transaction.Fee}");

            return (true, string.Empty);
        }

        private (bool IsValid, string ErrorMessage) ValidateTransferTransaction(Transaction transaction)
        {
            if (string.IsNullOrEmpty(transaction.To)) { return (false, "Field To is null"); }
            if (string.IsNullOrEmpty(transaction.From)) { return (false, "Field From is null"); }
            //if (!AddressPattern.IsMatch(transaction.From)) { return (false, $"Invalid From address: '{transaction.From}' (must be 0x + 40 alphanumeric chars)"); }
            //if (!AddressPattern.IsMatch(transaction.To)) { return (false, $"Invalid To address: '{transaction.To}' (must be 0x + 40 alphanumeric chars)"); }
            if (transaction.Amount <= 0) { return (false, "Field Amount is null"); }
            if (string.IsNullOrEmpty(transaction.Currency)) { return (false, "Currency ticker is null"); }

            // Protection against "air": can't transfer a token that was never issued.
            if (!transaction.Currency.Equals("BASE", StringComparison.OrdinalIgnoreCase) &&
                !_blockchain.CurrencyExists(transaction.Currency))
                return (false, $"Currency '{transaction.Currency}' doesn't exist — nobody has issued it yet");

            if (!_walletService.VerifySignature(transaction.SenderPublicKey, transaction.GetDataToSign(), transaction.Signature)) { return (false, "Invalid Signature"); }

            // Enough of the transferred currency itself.
            var tokenBalance = _walletService.GetBalance(transaction.From, transaction.Currency);
            if (tokenBalance < transaction.Amount)
                return (false, $"Insufficient {transaction.Currency} funds: {tokenBalance} < {transaction.Amount}");

            // Network fee is ALWAYS paid in BASE, even for pure token transfers.
            var isBaseCurrency = transaction.Currency.Equals("BASE", StringComparison.OrdinalIgnoreCase);
            var baseBalance = isBaseCurrency ? tokenBalance : _walletService.GetBalance(transaction.From, "BASE");
            var baseNeeded = isBaseCurrency ? transaction.Amount + transaction.Fee : transaction.Fee;
            if (baseBalance < baseNeeded)
                return (false, $"Insufficient BASE for network fee: {baseBalance} < {baseNeeded}");

            return (true, string.Empty);
        }
    }
}