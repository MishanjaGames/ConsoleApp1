using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChain_01.Models
{
    public enum MessageType
    {
        SyncChain,
        NewBlock
    }
    public class P2PMessage
    {
        public MessageType Type { get; set; }
        public string Data { get; set; } = string.Empty;
        public P2PMessage() { }
        public P2PMessage(MessageType type, string data)
        {
            Type = type;
            Data = data;
        }
    }
}
