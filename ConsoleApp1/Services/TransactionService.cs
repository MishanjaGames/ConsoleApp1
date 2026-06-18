using BlockChain_01.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BlockChain_01.Services
{
    public class TransactionService
    {
        public Transaction CreateTransaction(string from, string to, decimal amount) {
            var tx = new Transaction(from, to, amount);
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
            if (string.IsNullOrEmpty(transaction.From)) { return (false, "Field From is null"); }
            if (string.IsNullOrEmpty(transaction.To)) { return (false, "Field To is null"); }
            if (transaction.Amount <= 0) { return (false, "Field Amount is null"); }
            return (true, string.Empty);
        }
    }
}
