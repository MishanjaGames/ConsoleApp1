using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChain_01.Models
{
    public class Wallet
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public byte[] PublicKey { get; set; } = new byte[0];
        private byte[] PrivateKey { get; set; } = new byte[0];

        public Wallet () { }

        public Wallet(string name, string address, byte[] publicKey, byte[] privateKey)
        {
            Name = name;
            Address = address;
            PublicKey = publicKey;
            PrivateKey = privateKey;
        }

        public byte[] Sign(byte[] data)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();
                ecdsa.ImportECPrivateKey(PrivateKey, out _);
                return ecdsa.SignData(data, System.Security.Cryptography.HashAlgorithmName.SHA256);
        }
    }
}
