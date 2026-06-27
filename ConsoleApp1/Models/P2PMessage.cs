namespace BlockChain_01.Models
{
    public enum MessageType
    {
        SyncChain,
        NewBlock,
        NewTransaction,
        SyncMempool,
        SyncWallets
    }

    public class P2PMessage
    {
        public MessageType Type { get; set; }
        public string Data { get; set; } = "";

        public P2PMessage() { }

        public P2PMessage(MessageType type, string data)
        {
            Type = type;
            Data = data;
        }
    }
}