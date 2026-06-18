namespace BlockChain_01.Models
{
    public class Block
    {
        public int Index { get; set; }
        public DateTime TimeStamp { get; set; }
        public List<Transaction> Transactions { get; set; }
        public string PreviousHash { get; set; }
        public long Nonce { get; set; }
        public string Hash { get; set; }
        public double MiningDuration { get; set; }
        public int Difficulty { get; set; }

        public Block(int index, DateTime timeStamp, List<Transaction> transactions, string previousHash, int difficulty)
        {
            Index = index;
            TimeStamp = timeStamp;
            Transactions = transactions;
            Nonce = 0;
            PreviousHash = previousHash;
            Hash = string.Empty;
            MiningDuration = 0;
            Difficulty = difficulty;
        }

        public Block Clone(int difficulty)
        {
            return new Block(Index, TimeStamp, Transactions, PreviousHash, difficulty)
            {
                Nonce = this.Nonce,
                Hash = this.Hash
            };
        }
    }
}