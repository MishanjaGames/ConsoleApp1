using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BlockChain_01.Models;

namespace BlockChain_01.Services
{
    public class TCPP2PService
    {
        private TcpListener _listener = null!;
        private readonly ConcurrentBag<TcpClient> _clients = new();
        private readonly BlockChainService _blockchain;
        private readonly FileStorageService _storage;
        private readonly LoggingService _log;

        public int ListenPort { get; private set; }

        // Public-key registry: address → wallet info, shared across nodes
        private readonly ConcurrentDictionary<string, PeerWalletInfo> _peerWallets = new();

        public TCPP2PService(BlockChainService blockchain, int preferredPort,
                             FileStorageService storage, LoggingService log)
        {
            _blockchain = blockchain;
            _storage = storage;
            _log = log;

            // Load saved peer wallets from disk
            foreach (var w in _storage.LoadPeerWallets())
                _peerWallets[w.Address] = w;

            StartListener(preferredPort);
        }

        // ── Startup ──────────────────────────────────────────────────

        private void StartListener(int preferred)
        {
            int port = preferred;
            while (true)
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Any, port);
                    _listener.Start();
                    ListenPort = port;
                    Console.WriteLine($"[P2P] Listening on port {port}");
                    _log.Info("P2P", $"Listener started on port {port}");
                    break;
                }
                catch (SocketException)
                {
                    Console.WriteLine($"[P2P] Port {port} in use, trying {port + 1}...");
                    _log.Warn("P2P", $"Port {port} busy, trying next");
                    port++;
                }
            }
            _ = Task.Run(AcceptClientsAsync);
        }

        public void Start() { /* listener already started in ctor */ }

        // ── Outbound connection ──────────────────────────────────────

        public async Task ConnectToPeerAsync(string ip, int port)
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(ip, port);
                _clients.Add(client);
                Console.WriteLine($"[P2P] Connected to {ip}:{port}");
                _log.Info("P2P", $"Connected to {ip}:{port}");
                _ = Task.Run(() => HandleClientAsync(client));

                // Immediately share our chain & mempool
                SendTo(client, new P2PMessage(MessageType.SyncChain,
                    JsonSerializer.Serialize(_blockchain.Chain)));
                SendTo(client, new P2PMessage(MessageType.SyncMempool,
                    JsonSerializer.Serialize(_blockchain.PendingTransactions)));
                SendTo(client, new P2PMessage(MessageType.SyncWallets,
                    JsonSerializer.Serialize(_peerWallets.Values.ToList())));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[P2P] Failed to connect to {ip}:{port} — {ex.Message}");
                _log.Warn("P2P", $"Failed connecting to {ip}:{port}: {ex.Message}");
            }
        }

        // ── Broadcasts ───────────────────────────────────────────────

        public void BroadcastNewBlock(Block block)
        {
            _log.Info("P2P", $"Broadcasting block #{block.Index}");
            Broadcast(new P2PMessage(MessageType.NewBlock, JsonSerializer.Serialize(block)));
        }

        public void BroadcastTransaction(Transaction tx)
        {
            _log.Info("P2P", $"Broadcasting tx {tx.Id[..12]}");
            Broadcast(new P2PMessage(MessageType.NewTransaction, JsonSerializer.Serialize(tx)));
        }

        public void BroadcastMempool()
        {
            _log.Debug("P2P", "Broadcasting full mempool");
            Broadcast(new P2PMessage(MessageType.SyncMempool,
                JsonSerializer.Serialize(_blockchain.PendingTransactions)));
        }

        /// <summary>Register this node's wallet so peers can send to it when offline.</summary>
        public void RegisterWallet(PeerWalletInfo info)
        {
            _peerWallets[info.Address] = info;
            _storage.SavePeerWallets(_peerWallets.Values.ToList());
            _log.Info("P2P", $"Registered wallet {info.Name} ({info.Address[..16]}…)");
            Broadcast(new P2PMessage(MessageType.SyncWallets,
                JsonSerializer.Serialize(new List<PeerWalletInfo> { info })));
        }

        public IReadOnlyDictionary<string, PeerWalletInfo> KnownWallets => _peerWallets;

        // ── Private accept/handle ────────────────────────────────────

        private async Task AcceptClientsAsync()
        {
            while (true)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _clients.Add(client);
                    string ep = client.Client.RemoteEndPoint?.ToString() ?? "?";
                    Console.WriteLine($"[P2P] New peer: {ep}");
                    _log.Info("P2P", $"Accepted connection from {ep}");
                    _ = Task.Run(() => HandleClientAsync(client));

                    // Send newcomer our full state
                    SendTo(client, new P2PMessage(MessageType.SyncChain,
                        JsonSerializer.Serialize(_blockchain.Chain)));
                    SendTo(client, new P2PMessage(MessageType.SyncMempool,
                        JsonSerializer.Serialize(_blockchain.PendingTransactions)));
                    SendTo(client, new P2PMessage(MessageType.SyncWallets,
                        JsonSerializer.Serialize(_peerWallets.Values.ToList())));
                }
                catch { /* listener closed */ break; }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var stream = client.GetStream();
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            while (client.Connected)
            {
                try
                {
                    int len = reader.ReadInt32();
                    string json = Encoding.UTF8.GetString(reader.ReadBytes(len));
                    if (!string.IsNullOrEmpty(json)) ProcessMessage(json);
                }
                catch
                {
                    string ep = client.Client.RemoteEndPoint?.ToString() ?? "?";
                    Console.WriteLine($"[P2P] Peer disconnected: {ep}");
                    _log.Info("P2P", $"Peer disconnected: {ep}");
                    break;
                }
            }
        }

        private void ProcessMessage(string json)
        {
            var msg = JsonSerializer.Deserialize<P2PMessage>(json);
            if (msg == null) return;

            switch (msg.Type)
            {
                case MessageType.NewBlock:
                    {
                        var block = JsonSerializer.Deserialize<Block>(msg.Data);
                        if (block == null) return;
                        var last = _blockchain.Chain.Last();
                        if (block.Index == last.Index + 1 && block.PreviousHash == last.Hash)
                        {
                            _blockchain.Chain.Add(block);
                            _log.Info("P2P", $"Accepted block #{block.Index} from network");
                            Console.WriteLine($"[P2P] Block #{block.Index} accepted from network.");
                        }
                        else
                        {
                            _log.Warn("P2P", $"Rejected block #{block.Index} (bad chain link)");
                            Console.WriteLine($"[P2P] Block #{block.Index} rejected.");
                        }
                        break;
                    }

                case MessageType.NewTransaction:
                    {
                        var tx = JsonSerializer.Deserialize<Transaction>(msg.Data);
                        if (tx == null) return;
                        if (_blockchain.PendingTransactions.Any(t => t.Id == tx.Id)) return;
                        try
                        {
                            _blockchain.AddTransactionToMempool(tx);
                            _log.Info("P2P", $"Accepted mempool tx {tx.Id[..12]}");
                            Console.WriteLine($"[P2P] Tx {tx.Id[..12]}… added to mempool.");
                        }
                        catch (Exception ex)
                        {
                            _log.Warn("P2P", $"Rejected tx: {ex.Message}");
                        }
                        break;
                    }

                case MessageType.SyncMempool:
                    {
                        var txs = JsonSerializer.Deserialize<List<Transaction>>(msg.Data);
                        if (txs == null) return;
                        int added = 0;
                        foreach (var tx in txs)
                        {
                            if (_blockchain.PendingTransactions.Any(t => t.Id == tx.Id)) continue;
                            try { _blockchain.AddTransactionToMempool(tx); added++; }
                            catch { /* skip invalid */ }
                        }
                        if (added > 0)
                        {
                            _log.Info("P2P", $"Mempool sync: +{added} txs");
                            Console.WriteLine($"[P2P] Mempool synced: +{added} txs.");
                        }
                        break;
                    }

                case MessageType.SyncChain:
                    {
                        var chain = JsonSerializer.Deserialize<List<Block>>(msg.Data);
                        if (chain != null && chain.Count > _blockchain.Chain.Count)
                        {
                            _blockchain.Chain = chain;
                            _log.Info("P2P", $"Chain synced to {chain.Count} blocks");
                            Console.WriteLine($"[P2P] Chain synced: {chain.Count} blocks.");
                        }
                        break;
                    }

                case MessageType.SyncWallets:
                    {
                        var wallets = JsonSerializer.Deserialize<List<PeerWalletInfo>>(msg.Data);
                        if (wallets == null) return;
                        bool changed = false;
                        foreach (var w in wallets)
                        {
                            if (!_peerWallets.ContainsKey(w.Address))
                            {
                                _peerWallets[w.Address] = w;
                                changed = true;
                                _log.Info("P2P", $"Learned wallet {w.Name} ({w.Address[..16]}…)");
                                Console.WriteLine($"[P2P] Learned wallet: {w.Name}");
                            }
                        }
                        if (changed)
                            _storage.SavePeerWallets(_peerWallets.Values.ToList());
                        break;
                    }

                default:
                    _log.Warn("P2P", $"Unknown message type: {msg.Type}");
                    break;
            }
        }

        private void SendTo(TcpClient client, P2PMessage msg)
        {
            try
            {
                var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
                var len = BitConverter.GetBytes(json.Length);
                var stream = client.GetStream();
                stream.Write(len, 0, len.Length);
                stream.Write(json, 0, json.Length);
                stream.Flush();
            }
            catch { /* client gone */ }
        }

        private void Broadcast(P2PMessage msg)
        {
            var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
            var len = BitConverter.GetBytes(json.Length);

            foreach (var client in _clients.Where(c => c.Connected))
            {
                try
                {
                    var stream = client.GetStream();
                    stream.Write(len, 0, len.Length);
                    stream.Write(json, 0, json.Length);
                    stream.Flush();
                }
                catch (Exception ex)
                {
                    _log.Warn("P2P", $"Broadcast error: {ex.Message}");
                }
            }
        }
    }

    /// <summary>Shareable wallet info (no private key). Lets any peer send coins to this address.</summary>
    public class PeerWalletInfo
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    }
}