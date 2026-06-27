using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class TransactionService
    {
        private readonly WalletService _walletService;
        private readonly BlockChainService _blockchain;

        public TransactionService(BlockChainService blockchain)
        {
            _blockchain = blockchain;
            _walletService = new WalletService(blockchain.Chain);
        }

        public Transaction? CreateTransaction(Wallet walletFrom, string to, decimal amount, byte[] senderPublicKey)
        {
            bool isCoinbase = walletFrom.Name == "COINBASE";
            if (!isCoinbase && _walletService.GetBalance(walletFrom.Address) < amount)
            {
                Console.WriteLine($"Insufficient funds: {walletFrom.Name}");
                return null;
            }

            string from = isCoinbase ? "COINBASE" : walletFrom.Address;
            var tx = new Transaction(from, to, amount, senderPublicKey);
            tx.Signature = walletFrom.Sign(tx.GetDataToSign());

            var (isValid, error) = ValidateTransaction(tx);
            if (!isValid) throw new ArgumentException(error);

            return tx;
        }

        public (bool IsValid, string ErrorMessage) ValidateTransaction(Transaction tx)
        {
            if (tx == null) return (false, "Transaction is null");
            if (string.IsNullOrEmpty(tx.To)) return (false, "Field 'To' is empty");
            if (tx.From == "COINBASE") return (true, string.Empty);
            if (string.IsNullOrEmpty(tx.From)) return (false, "Field 'From' is empty");
            if (tx.Amount <= 0) return (false, "Amount must be > 0");
            if (!_walletService.VerifySignature(tx.SenderPublicKey, tx.GetDataToSign(), tx.Signature))
                return (false, "Invalid signature");
            return (true, string.Empty);
        }
    }
}