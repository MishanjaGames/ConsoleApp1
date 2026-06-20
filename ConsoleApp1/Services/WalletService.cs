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
        public Wallet CreateWallet(string name)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();

            byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();
            byte[] privateKey = ecdsa.ExportECPrivateKey();

            string address = Convert.ToBase64String(publicKey);
            return new Wallet(name, address, publicKey, privateKey);
        }

        public bool VerifySignature(byte [] publicKey, byte[] data, byte[] signature)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();

            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            return ecdsa.VerifyData(data, signature, System.Security.Cryptography.HashAlgorithmName.SHA256);
        }
    }
}
