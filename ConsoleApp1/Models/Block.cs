namespace BlockChain_01.Models
{
    public class Block
    {
        public int Index { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Author { get; set; }
        public string Data { get; set; }
        public string PreviousHash { get; set; }
        public int Nonce { get; set; }
        public string Hash { get; set; }

        public Block(int index, DateTime timeStamp, string author, string data, string previousHash)
        {
            Index = index;
            TimeStamp = timeStamp;
            Author = author;
            Data = data;
            Nonce = 0;
            PreviousHash = previousHash;
            Hash = string.Empty;
        }

        public Block Clone()
        {
            return new Block(Index, TimeStamp, Author, Data, PreviousHash)
            {
                Nonce = this.Nonce,
                Hash = this.Hash
            };
        }
    }
}