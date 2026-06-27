using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlockChain_01.Models;
using BlockChain_01.Services;

namespace BlockChain_01.Services
{
    public class TCPP2PService
    {
        public readonly TcpListener _listener;
        private readonly ConcurrentBag<TcpClient> _clients = new ConcurrentBag<TcpClient>();
        private readonly BlockChainService _blockChainService;

        public TCPP2PService(BlockChainService blockChainService, int port)
        {
            _blockChainService = blockChainService;
            _listener = new TcpListener(System.Net.IPAddress.Any, port);
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"P2P service started on port {_listener.LocalEndpoint}");
            Task.Run(async () => AcceptClientAsync());
        }

        public async Task ConnectToPeerAsync(string ipAddress, int port)
        {
            try
            {
                var client = new TcpClient();
                client.Connect(ipAddress, port);
                _clients.Add(client);
                Console.WriteLine($"Connected to peer: {ipAddress}:{port}");
                Task.Run(() => HandleClientAsync(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to peer {ipAddress}:{port} - {ex.Message}");
            }
        }

        private async Task AcceptClientAsync()
        {
            while (true)
            {
                var client = _listener.AcceptTcpClient();
                _clients.Add(client);
                Console.WriteLine($"New client connected: {client.Client.RemoteEndPoint}");
                Task.Run(()=>HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var stream = client.GetStream();
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            while (client.Connected)
            {
                try
                {
                    var messageLength = reader.ReadInt32();
                    var messageBytes = reader.ReadBytes(messageLength);
                    var messageJson = Encoding.UTF8.GetString(messageBytes);
                    if (!string.IsNullOrEmpty(messageJson))
                    {
                        Console.WriteLine($"Received message from {client.Client.RemoteEndPoint}: {messageJson}");
                        ProcessMessage(messageJson);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                    break;
                }
            }
        }

        private void ProcessMessage(string messageJson)
        {
            var message = JsonSerializer.Deserialize<Models.P2PMessage>(messageJson);
            if (message == null) return;

            switch (message.Type)
            {
                case Models.MessageType.NewBlock:
                    var newBlock = JsonSerializer.Deserialize<Models.Block>(message.Data);
                    if (newBlock == null) return;
                    var lastBlock = _blockChainService.Chain.Last();
                    if (newBlock.Index == lastBlock.Index + 1 && newBlock.PreviousHash == lastBlock.Hash)
                    {
                        _blockChainService.Chain.Add(newBlock);
                        Console.WriteLine($"New block added to the chain: {newBlock.Index}");
                    }
                    else
                    {
                        Console.WriteLine($"Received invalid block: {newBlock.Index}");
                    }
                    break;
                case Models.MessageType.SyncChain:
                    var receivedChain = JsonSerializer.Deserialize<List<Models.Block>>(message.Data);
                    if (receivedChain == null) return;
                    if (receivedChain.Count > _blockChainService.Chain.Count)
                    {
                        _blockChainService.Chain = receivedChain;
                        Console.WriteLine($"Chain synchronized with {receivedChain.Count} blocks.");
                    }
                    break;
                default:
                    Console.WriteLine($"Unknown message type: {message.Type}");
                    break;
            }
        }

        private void BroadcastMessage(P2PMessage message)
        {
            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            var messageLength = BitConverter.GetBytes(messageBytes.Length);
            foreach (var client in _clients)
            {
                if (client.Connected)
                {
                    try
                    {
                        var stream = client.GetStream();
                        stream.Write(messageLength, 0, messageLength.Length);
                        stream.Write(messageBytes, 0, messageBytes.Length);
                        Console.WriteLine($"Broadcasted message to {client.Client.RemoteEndPoint}");
                        stream.Flush();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error broadcasting to {client.Client.RemoteEndPoint}: {ex.Message}");
                    }
                }
            }
        }

        public void BroadcastNewBlock(Block block)
        {
            var message = new P2PMessage(Models.MessageType.NewBlock, JsonSerializer.Serialize(block));
            BroadcastMessage(message);
        }

    }
}
