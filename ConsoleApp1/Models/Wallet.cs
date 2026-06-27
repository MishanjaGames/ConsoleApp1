using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace BlockChain_01.Models
{
    public class Wallet
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();

        // Stored encrypted (AES-256). Plain bytes when loaded & unlocked in memory.
        public byte[] EncryptedPrivateKey { get; set; } = Array.Empty<byte>();
        public byte[] Salt { get; set; } = Array.Empty<byte>();

        // Runtime-only: not serialized
        [JsonIgnore]
        private byte[]? _privateKey;

        public Wallet() { }

        /// <summary>Create a brand-new wallet, encrypting the private key with a password.</summary>
        public static Wallet Create(string name, string password)
        {
            using var ecdsa = ECDsa.Create();
            byte[] pub = ecdsa.ExportSubjectPublicKeyInfo();
            byte[] priv = ecdsa.ExportECPrivateKey();
            string address = Convert.ToBase64String(pub);

            var wallet = new Wallet { Name = name, Address = address, PublicKey = pub };
            wallet.SetPrivateKey(priv, password);
            wallet._privateKey = priv;
            return wallet;
        }

        /// <summary>Attempt to unlock. Returns true if password is correct.</summary>
        public bool Unlock(string password)
        {
            try
            {
                _privateKey = Decrypt(EncryptedPrivateKey, password, Salt);
                return true;
            }
            catch
            {
                _privateKey = null;
                return false;
            }
        }

        public void Lock() => _privateKey = null;

        public bool IsUnlocked => _privateKey != null;

        public byte[] Sign(byte[] data)
        {
            if (_privateKey == null)
                throw new InvalidOperationException("Wallet is locked. Unlock it first.");
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(_privateKey, out _);
            return ecdsa.SignData(data, HashAlgorithmName.SHA256);
        }

        // ── Encryption helpers (AES-256-CBC + PBKDF2) ────────────────

        private void SetPrivateKey(byte[] privateKey, string password)
        {
            Salt = RandomNumberGenerator.GetBytes(16);
            EncryptedPrivateKey = Encrypt(privateKey, password, Salt);
        }

        private static byte[] Encrypt(byte[] data, string password, byte[] salt)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            byte[] cipher = enc.TransformFinalBlock(data, 0, data.Length);
            // prepend IV
            return aes.IV.Concat(cipher).ToArray();
        }

        private static byte[] Decrypt(byte[] data, string password, byte[] salt)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);
            aes.IV = data[..16];
            using var dec = aes.CreateDecryptor();
            return dec.TransformFinalBlock(data, 16, data.Length - 16);
        }

        private static byte[] DeriveKey(string password, byte[] salt) =>
            new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), salt,
                100_000, HashAlgorithmName.SHA256).GetBytes(32);
    }
}