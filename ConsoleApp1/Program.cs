using BlockChain_01.Models;
using BlockChain_01.Services;
using System.Text;


Console.WriteLine("Awaiting port:");
var port = int.Parse(Console.ReadLine() ?? "5000");
Console.WriteLine("Input username");
var name = Console.ReadLine();



var blockchain = new BlockChainService();
var display = new BlockChainDisplayService(blockchain);
var walletService = new WalletService(blockchain.Chain);
var systemWallet = new WalletService(blockchain.Chain).CreateWallet("COINBASE");
var transactionService = new TransactionService(blockchain);

var users = new List<Wallet>();
var walletRegistry = new Dictionary<string, Wallet>();


Console.WriteLine("Blockchain initiated :)");

Console.WriteLine($"Total cores count: {Environment.ProcessorCount}");
Console.WriteLine($"Total cores in use count: {Environment.ProcessorCount / 2}");

var w_Mark = walletService.CreateWallet("Mark");
var w_Alice = walletService.CreateWallet("Alice");
var device = new Wallet();
var testdevice = new Wallet();
if (name == "user") {
    device = walletService.CreateWallet(name);
    testdevice = walletService.CreateWallet(name+1);
}
else if (name == "user1")
{
    device = walletService.CreateWallet(name);
    testdevice = walletService.CreateWallet("user");
}
users.Add(w_Mark);
users.Add(w_Alice);
users.Add(device);
users.Add(testdevice);
walletRegistry["Mark"] = w_Mark;
walletRegistry["Alice"] = w_Alice;
walletRegistry[systemWallet.Name] = systemWallet;
walletRegistry[device.Name] = device;
walletRegistry[testdevice.Name] = testdevice;

var p2pService = new TCPP2PService(blockchain, port);
p2pService.Start();

Console.WriteLine($"Input port and address to connect");
var portToConnect = int.Parse(Console.ReadLine() ?? "5000");
if (portToConnect != null)
{
    Console.WriteLine($"Connecting to 127.0.0.1:{portToConnect}...");
    await p2pService.ConnectToPeerAsync("127.0.0.1", portToConnect);
}

string? choice;

do
{
    Console.WriteLine(new string('=', 50));
    Console.WriteLine($"|| {device.Name}:{walletService.GetBalance(device.Address)} ||");
    Console.WriteLine(new string('=', 50));
    Console.WriteLine("1: Display Blockchain");
    Console.WriteLine("2: Add Block");
    Console.WriteLine("3: Add Transaction");
    Console.WriteLine("4: View Banace");
    Console.WriteLine("5: Validate BlockChain");
    Console.WriteLine("6: Get Block by Index");
    Console.WriteLine("7: Initiate testing");
    Console.WriteLine("8: Vanity Mining Demo");
    Console.WriteLine("9: Smart Chunking + Address Validation Demo");
    Console.WriteLine("10: Economy Audit (Double Spend + Hard Cap + Proof of Reserves)");
    Console.WriteLine("11: Merkle Tree Demo (Merkle Root / Proof / CVE-2012-2459)");
    Console.WriteLine("12: [Attack]: Block spoof");
    Console.WriteLine("0: Exit");
    Console.WriteLine(new string('-', 50));
    choice = Console.ReadLine();
    Console.WriteLine(new string('=', 50));

    switch (choice)
    {
        case "1":
            display.PrintBlockChain(blockchain.Chain);
            break;

        case "2":
            //var trans = transactionService.CreateTransaction(w_Mark, w_Alice.Address, 10, w_Mark.PublicKey);
            //var trans1 = transactionService.CreateTransaction(w_Alice, w_Mark.Address, 10, w_Alice.PublicKey);
            //if (trans == null || trans1 == null)
            //{
            //    Console.WriteLine("Transaction creation failed. Check balances and try again.");
            //    break;
            //}
            Console.Write("Enter (1) for multithreaded (2) for generating in net: ");
            string select = Console.ReadLine() ?? "";
            if (select == "1")
            {
                Console.Write("How many coins to generate: ");
                if (!int.TryParse(Console.ReadLine(), out int c)) break;

                for (int i = 0; i < c; i++)
                {
                    p2pService.BroadcastNewBlock(await blockchain.MineBlockAsync(device.Address));
                }
            }
            else if (select == "2")
            {
                Console.Write("How many coins to generate: ");
                if (!int.TryParse(Console.ReadLine(), out int count)) break;

                for (int i = 0; i < count; i++)
                {
                    using var cts = new CancellationTokenSource();

                    var networkSimTask = Task.Run(async () =>
                    {
                        int delay = Random.Shared.Next(2000, 8000);
                        Console.WriteLine($"[Network] Other node is generating block. Awaiting answer in ~{delay / 1000}s");
                        await Task.Delay(delay);
                        if (!cts.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("\n[Network] Block already generated. Canceling...");
                            cts.Cancel();
                        }
                    });

                    var mined = await blockchain.MineBlockAsync(device.Address, cts.Token);

                    if (mined != null)
                    {
                        Console.WriteLine("[Network] Block generated. Adding it.");
                        p2pService.BroadcastNewBlock(mined);
                        cts.Cancel();
                    }

                    try { await networkSimTask; } catch { }
                }
            }
            else
            {
                Console.WriteLine("Wrong input");
            }
            break;

        case "3":
            Console.Write("From (wallet name): ");
            string from = Console.ReadLine() ?? "";
            Console.Write("To (wallet name or address): ");
            string to = Console.ReadLine() ?? "";
            Console.Write("Amount: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal amount)) break;

            if (!walletRegistry.ContainsKey(from))
            {
                Console.WriteLine($"Wallet '{from}' not found. Available wallets: {string.Join(", ", walletRegistry.Keys)}");
                break;
            }

            var walletFrom = walletRegistry[from];

            // Convert wallet name to address if needed
            string toAddress = to;
            if (walletRegistry.ContainsKey(to))
            {
                toAddress = walletRegistry[to].Address;
            }

            try
            {
                var transaction = transactionService.CreateTransaction(walletFrom, toAddress, amount, walletFrom.PublicKey);
                if (transaction == null)
                {
                    Console.WriteLine("Transaction creation failed.");
                    break;
                }

                // Add transaction to mempool
                blockchain.AddTransactionToMempool(transaction);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            break;

        case "4":
            Console.WriteLine("\n=== Wallet Balances ===");
            foreach (var kvp in walletRegistry)
            {
                decimal balance = walletService.GetBalance(kvp.Value.Address);
                Console.WriteLine($"{kvp.Key}: {balance}");
            }
            break;

        case "5":
            if (blockchain.IsValid())
                Console.WriteLine("All Blockchain is valid");
            else
            {
                Console.WriteLine("Integrity compromised!");
                display.PrintBlock(null, null, blockchain.GetInvalidBlockIndex());
            }
            break;

        case "6":
            Console.Write("Block Index: ");
            if (int.TryParse(Console.ReadLine(), out int idx))
                display.PrintBlock(null, null, idx - 1);
            break;
        case "7":
            //await RunMempoolDemo();
            break;
        case "8":
            Console.WriteLine("FIX THIS.");
            //await TestVanityMining();
            break;
        case "9":
            Console.WriteLine("FIX THIS.");
            //RunSmartChunkingDemo();
            break;
        case "10":
            //await RunEconomyAudit();
            break;
        case "11":
            //RunMerkleTreeDemo();
            break;
        case "12":
            var attackBlock = blockchain.Chain.Last();
            if (attackBlock.Transactions.Count == 0) { Console.WriteLine("Transactions hasnt been found."); break; }
            var attackjson = System.Text.Json.JsonSerializer.Serialize(attackBlock);
            var tamperedBlock = System.Text.Json.JsonSerializer.Deserialize<Block>(attackjson)!;
            tamperedBlock.Transactions[0].Amount = 999999;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Block spoof Attack] Sending tempered block #{tamperedBlock.Index} : (Amount changed to 999999)...");
            Console.ResetColor();
            p2pService.BroadcastNewBlock(tamperedBlock);
            break;
        default:
            if (choice != "0")
                Console.WriteLine("Incorrect. Try again.");
            break;
    }
}
while (choice != "0");

//void RunMerkleTreeDemo()
//{
//    var hs = new HashingService();

//    Console.WriteLine("=== Tasks 2: display of Merkle Root ===");
//    var txs = new List<Transaction>();
//    for (int i = 0; i < 10; i++)
//        txs.Add(new Transaction($"Addr{i}A", $"Addr{i}B", 1 + i, new byte[0]));

//    string root = hs.GetMerkleRoot(txs);
//    Console.WriteLine($"Merkle Root: {root}");

//    Console.WriteLine("\n=== Tasks 3: Merkle Proof for Node ===");
//    var target = txs[5];
//    var proof = hs.GetMerkleProof(txs, target.Id);
//    foreach (var (h, isLeft) in proof)
//        Console.WriteLine($"  Neighbor: {h[..12]}... | IsLeft={isLeft}");

//    Console.WriteLine("\n=== Task 3: Proof check for client ===");
//    string targetHash = hs.ComputeHash_P(target.ToRawString());
//    bool valid = hs.VerifyMerkleProof(targetHash, root, proof);
//    Console.WriteLine($"VerifyMerkleProof (validation): {valid}");

//    Console.WriteLine("\n=== Task 4: CVE-2012-2459 Attack (transaction duplication) ===");
//    var attackTxs = new List<Transaction>(txs.Take(3));
//    attackTxs.Add(attackTxs[2]); // dumb duplication
//    try
//    {
//        hs.GetMerkleRoot(attackTxs);
//        Console.WriteLine("ATTENTION: Attack has succeded!");
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"Attack failed: {ex.Message}");
//    }
//}

//Console.WriteLine(new string('=', 20));
//blockchain.Chain[1].Data = "Bob -> ???: 99999999999";
//displayblockchain.PrintValidationResult(blockchain.IsValid());
//displayblockchain.PrintBlock(null, null, blockchain.GetInvalidBlockIndex());


//Console.WriteLine(new string('=', 20));
//blockchain.Chain[2].Author = "John";
//displayblockchain.PrintValidationResult(blockchain.IsValid());
//displayblockchain.PrintBlock(null, null, blockchain.GetInvalidBlockIndex());


//Console.WriteLine(new string('=', 20));
//displayblockchain.PrintBlock(blockchain.FindBlockByHash(blockchain.Chain[1].Hash));


//Console.WriteLine(new string('=', 20));

//var hashingService = new HashingService();

//string str1 = "Hello";
//string str2 = "hello";

//string hash1 = hashingService.ComputeHash_P(str1);
//string hash2 = hashingService.ComputeHash_P(str2);

//Console.WriteLine($"Hash 1: \"{str1}\" => {hash1}");
//Console.WriteLine($"Hash 2: \"{str2}\" => {hash2}");

//int diffCount = 0;
//for (int i = 0; i < hash1.Length; i++)
//{
//    if (hash1[i] != hash2[i])
//        diffCount++;
//}

//double changePercent = (double)diffCount / hash1.Length * 100;

//Console.WriteLine($"Different chars: {diffCount} / {hash1.Length}");
//Console.WriteLine($"Hash change: {changePercent}%");

/*
static void PrintResult(string testName, bool passed)
{
    string status = passed ? "[TEST COMPLETED]" : "[TEST FAILED]";
    Console.WriteLine($"{status} {testName}\n");
}
void TestSystem()
{
    Console.WriteLine("=== Test 1: Default workload ===");
    var bc = new BlockChainService();
    bc.AddBlock("Alice", "TX: Alice->Bob: 10");
    bc.AddBlock("Bob", "TX: Bob->Carol: 5");
    bc.AddBlock("Carol", "TX: Carol->Alice: 2");
    bool valid = bc.IsValid();
    Console.WriteLine($"IsValid = {valid}");
    PrintResult("Default workload", valid == true);

    Console.WriteLine("=== Test 2: Corrupted Duration ===");
    bc = new BlockChainService();
    bc.AddBlock("Alice", "TX: Alice->Bob: 10");
    bc.AddBlock("Bob", "TX: Bob->Carol: 5");

    Block last = bc.Chain.Last();
    last.MiningDuration = 9999;
    var hs = new HashingService();
    last.Hash = hs.ComputeHash(last);

    valid = bc.IsValid();
    Console.WriteLine($"IsValid = {valid} — Duration Corrupted!");
    PrintResult("[Attack]: Corrupted Duration", valid == false);

    Console.WriteLine("=== Test 3: Negative Duration ===");
    bc = new BlockChainService();
    bc.AddBlock("Alice", "TX: Alice->Bob: 10");
    bc.AddBlock("Bob", "TX: Bob->Carol: 5");

    last = bc.Chain.Last();
    last.MiningDuration = -10;
    hs = new HashingService();
    last.Hash = hs.ComputeHash(last);

    valid = bc.IsValid();
    Console.WriteLine($"IsValid = {valid}");
    PrintResult("[Attack]: Negative Duration", valid == false);

    Console.WriteLine("=== Test 4: Difficulty limit ===");
    bc = new BlockChainService(targetBlockTime: 500);
    Console.WriteLine($"Initial difficulty: {bc.Difficulty}");

    bc.AddBlock("A", "block 1");
    bc.AddBlock("B", "block 2");
    bc.AddBlock("C", "block 3");
    bc.AddBlock("D", "block 4");

    int d = bc.Difficulty;
    Console.WriteLine($"Difficulty after 4 blocks: {d}");
    Console.WriteLine($"Max possible with +1 limit: 5");

    bool limitHeld = d >= 2 && d <= 5;
    PrintResult("[Attack]: Difficulty limit", limitHeld);
}

async Task TestVanityMining()
{
    Console.WriteLine("=== Vanity Mining Demo (target prefix: \"cafe\") ===");
    var bc = new BlockChainService();
    await bc.AddBlockAsync("Alice", "TX: Alice->Bob: 10");
    await bc.AddBlockAsync("Bob", "TX: Bob->Carol: 5");
    await bc.AddBlockAsync("Carol", "TX: Carol->Alice: 2");

    foreach (var b in bc.Chain)
        Console.WriteLine($"Index {b.Index} | Hash: {b.Hash}");

    PrintResult("Vanity Mining", bc.IsValid());
}


 * void RunMalleabilityDemo()
{
    Console.WriteLine("=== Частина 1: Атака колізії (наївна конкатенація From+To+Amount) ===");
    static string NaiveId(string from, string to, decimal amount)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{from}{to}{amount}");
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    var tx1 = new Transaction("Ali", "ceBob", 100);
    var tx2 = new Transaction("Alice", "Bob", 100);

    string naive1 = NaiveId(tx1.From, tx1.To, tx1.Amount);
    string naive2 = NaiveId(tx2.From, tx2.To, tx2.Amount);

    Console.WriteLine($"Tx1: {tx1.From} -> {tx1.To}, {tx1.Amount} | NaiveId: {naive1}");
    Console.WriteLine($"Tx2: {tx2.From} -> {tx2.To}, {tx2.Amount} | NaiveId: {naive2}");
    Console.WriteLine(naive1 == naive2 ? "Увага! Знайдено колізію: Tx1.Id == Tx2.Id" : "Колізії не знайдено.");

    Console.WriteLine("\n=== Частина 2: Виправлена версія (Transaction.Id = SHA256(ToRawString())) ===");
    Console.WriteLine($"Tx1 FixedId: {tx1.Id}");
    Console.WriteLine($"Tx2 FixedId: {tx2.Id}");
    Console.WriteLine(tx1.Id != tx2.Id ? "Колізію усунено: Id тепер відрізняються." : "Помилка: колізія досі існує!");

    Console.WriteLine("\n=== Частина 3: Byte-ліміт блоку (MaxBlockSizeBytes) ===");
    var bigTxs = new List<Transaction>();
    for (int i = 0; i < 10; i++)
        bigTxs.Add(new Transaction(FakeAddress(2000 + i), FakeAddress(3000 + i), 1000 + i));

    blockchain.AddBlock(bigTxs);
    var lastBlock = blockchain.Chain.Last();
    int weight = lastBlock.Transactions.Sum(t => System.Text.Encoding.UTF8.GetByteCount(t.ToRawString()));
    Console.WriteLine($"Передано транзакцій: {bigTxs.Count}, влізло у блок: {lastBlock.Transactions.Count}");
    Console.WriteLine($"Фінальна вага блоку: {weight} байт (ліміт {blockchain.MaxBlockSizeBytes})");
}


static string FakeAddress(int n) => "0x" + n.ToString("x").PadLeft(40, '0');

void RunSmartChunkingDemo()
{
    Console.WriteLine("=== Smart Chunking Demo: 15 valid transactions ===");
    var txs = new List<Transaction>();
    for (int i = 0; i < 15; i++)
        txs.Add(new Transaction(FakeAddress(100 + i), FakeAddress(200 + i), 1 + i));

    int before = blockchain.Chain.Count;
    blockchain.ProcessTransactions(txs);
    int after = blockchain.Chain.Count;
    Console.WriteLine($"\nResult: {after - before} new block(s) mined, no transactions lost.");

    Console.WriteLine("\n=== Invalid address rejection demo ===");
    var badTx = new Transaction("Bob", FakeAddress(999), 5);
    blockchain.ProcessTransactions(new List<Transaction> { badTx });
}

var trans1 = new Transaction(FakeAddress(1), FakeAddress(2), 10);
var trans2 = new Transaction(FakeAddress(2), FakeAddress(3), 100);
var trans3 = new Transaction(FakeAddress(4), FakeAddress(5), 50);


async Task RunEconomyAudit()
{
    Console.WriteLine("=== Part 1: Attack Double Spend ===");
    var bc = new BlockChainService();
    var ws = new WalletService(bc.Chain);
    var ts = new TransactionService(bc);
    var aliceW = ws.CreateWallet("Alice");
    var bobW = ws.CreateWallet("Bob");
    var carloW = ws.CreateWallet("Carlo");
    var minerW = ws.CreateWallet("Miner");

    await bc.MineBlockAsync(aliceW.Address);
    Console.WriteLine($"Balance of Alice: {ws.GetBalance(aliceW.Address)}");

    var tx1 = ts.CreateTransaction(aliceW, bobW.Address, 50, aliceW.PublicKey);
    var tx2 = ts.CreateTransaction(aliceW, carloW.Address, 50, aliceW.PublicKey);
    try
    {
        bc.AddTransactionToMempool(tx1);
        bc.AddTransactionToMempool(tx2);
        await bc.MineBlockAsync(minerW.Address);
        Console.WriteLine("ERROR: attack had an effect!");
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Attack blocked: {ex.Message}");
    }

    Console.WriteLine("\n=== Part 2: Hard Cap (MaxSupply=1000) ===");
    for (int i = 1; i <= 22; i++)
    {
        await bc.MineBlockAsync(minerW.Address);
        if (i is >= 18 and <= 21)
            Console.WriteLine($"Block #{i}: TotalMinted={bc.TotalMinted}, Miners balance={ws.GetBalance(minerW.Address)}");
    }

    Console.WriteLine("\n=== Part 3: Audit of economics (Proof of Reserves) ===");
    bool ok = bc.ValidateEconomy();
    Console.WriteLine($"ValidateEconomy(): {ok}");
}


async Task RunMempoolDemo()
{
    Console.WriteLine("\n" + new string('=', 60));
    Console.WriteLine("=== MEMPOOL DEMO: DDoS + RBF + Shadow Balance ===");
    Console.WriteLine(new string('=', 60));

    var bc = new BlockChainService();
    var ws = new WalletService(bc.Chain);
    var ts = new TransactionService(bc);

    var hacker = ws.CreateWallet("Hacker");
    var alice = ws.CreateWallet("Alice");
    var bob = ws.CreateWallet("Bob");
    var carlo = ws.CreateWallet("Carlo");
    var miner = ws.CreateWallet("Miner");

    // Fund wallets: mine 2 blocks to alice, 2 to bob
    Console.WriteLine("\n[Setup] Mining initial blocks to fund wallets...");
    await bc.MineBlockAsync(alice.Address);
    await bc.MineBlockAsync(alice.Address);
    await bc.MineBlockAsync(bob.Address);
    await bc.MineBlockAsync(bob.Address);
    // Clear mempool coinbase txs by mining them
    await bc.MineBlockAsync(miner.Address);

    Console.WriteLine($"[Setup] Alice balance: {ws.GetBalance(alice.Address)}");
    Console.WriteLine($"[Setup] Bob balance:   {ws.GetBalance(bob.Address)}");

    // ── Part 1: Spam / DDoS ──────────────────────────────────────
    Console.WriteLine("\n--- Part 1: DDoS Spam Attack (10 txs, Fee=0) ---");
    int spamAccepted = 0, spamRejected = 0;
    for (int i = 0; i < 10; i++)
    {
        try
        {

            var spamTx = ts.CreateTransaction(alice, hacker.Address, 1, alice.PublicKey);
            spamTx.GetType().GetProperty("Fee")?.SetValue(spamTx, 0m);

            var rawSpam = new Transaction(alice.Address, hacker.Address, 1, alice.PublicKey);
            rawSpam.Fee = 0;
            rawSpam.Signature = alice.Sign(rawSpam.GetDataToSign());
            bc.AddTransactionToMempool(rawSpam);
            spamAccepted++;
            Console.WriteLine($"  Spam tx #{i + 1}: ACCEPTED (mempool={bc.PendingTransactions.Count})");
        }
        catch (Exception ex)
        {
            spamRejected++;
            Console.WriteLine($"  Spam tx #{i + 1}: REJECTED — {ex.Message}");
        }
    }
    Console.WriteLine($"[Result] Accepted={spamAccepted}, Rejected={spamRejected}, Mempool size={bc.PendingTransactions.Count}/{bc.MaxMempoolSize}");

    // ── Part 2a: Alice evicts cheapest spam ───────────────────────
    Console.WriteLine("\n--- Part 2: Alice sends tx with Fee=10 (evicts cheapest spam) ---");
    try
    {
        var aliceTx = new Transaction(alice.Address, carlo.Address, 5, alice.PublicKey);
        aliceTx.Fee = 10;
        aliceTx.Signature = alice.Sign(aliceTx.GetDataToSign());
        bc.AddTransactionToMempool(aliceTx);
        Console.WriteLine($"[Result] Alice tx ACCEPTED. Mempool size={bc.PendingTransactions.Count}");
        Console.WriteLine($"  Min fee in mempool: {bc.PendingTransactions.Min(t => t.Fee)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Result] Alice tx REJECTED — {ex.Message}");
    }

    // ── Part 2b: RBF ─────────────────────────────────────────────
    Console.WriteLine("\n--- Part 3: RBF — Bob sends 20 to Carlo, Fee=1, then bumps to Fee=15 ---");
    try
    {
        var bobTx1 = new Transaction(bob.Address, carlo.Address, 20, bob.PublicKey);
        bobTx1.Fee = 1;
        bobTx1.Signature = bob.Sign(bobTx1.GetDataToSign());
        bc.AddTransactionToMempool(bobTx1);
        Console.WriteLine($"  Bob tx1 (Fee=1) ACCEPTED. Mempool={bc.PendingTransactions.Count}");
    }
    catch (Exception ex) { Console.WriteLine($"  Bob tx1 REJECTED — {ex.Message}"); }

    try
    {
        var bobTx2 = new Transaction(bob.Address, carlo.Address, 20, bob.PublicKey);
        bobTx2.Fee = 15;
        bobTx2.Signature = bob.Sign(bobTx2.GetDataToSign());
        bc.AddTransactionToMempool(bobTx2);
        Console.WriteLine($"  Bob tx2 (Fee=15) — RBF applied. Mempool={bc.PendingTransactions.Count}");
    }
    catch (Exception ex) { Console.WriteLine($"  Bob tx2 REJECTED — {ex.Message}"); }

    // ── Part 3: Shadow / Pending Balance ─────────────────────────
    Console.WriteLine("\n--- Part 4: Shadow Balance — Alice tries to double-spend via mempool ---");
    Console.WriteLine($"  Alice confirmed balance: {ws.GetBalance(alice.Address)}");
    Console.WriteLine($"  Alice pending balance:   {bc.GetPendingBalance(alice.Address)}");

    // Alice already has a pending tx (5 + fee=10 = 15 reserved).
    // Try to send another large amount that would exceed pending balance.
    try
    {
        var aliceTx2 = new Transaction(alice.Address, bob.Address, 90, alice.PublicKey);
        aliceTx2.Fee = 5;
        aliceTx2.Signature = alice.Sign(aliceTx2.GetDataToSign());
        bc.AddTransactionToMempool(aliceTx2);
        Console.WriteLine("  Alice tx2: ACCEPTED (unexpected)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Alice tx2 REJECTED (shadow balance protection) — {ex.Message}");
    }

    // ── Part 4: Miner picks top 2 txs ────────────────────────────
    Console.WriteLine("\n--- Part 5: Miner mines block (takes top fee txs) ---");
    Console.WriteLine($"  Mempool before mining ({bc.PendingTransactions.Count} txs):");
    foreach (var tx in bc.PendingTransactions.OrderByDescending(t => t.Fee))
        Console.WriteLine($"    {tx.From[..8]}... -> {tx.To[..8]}... | Amount={tx.Amount} Fee={tx.Fee}");

    // Limit block to 2 txs by temporarily working with top 2
    var top2 = bc.PendingTransactions.OrderByDescending(t => t.Fee).Take(2).ToList();
    var remaining = bc.PendingTransactions.Except(top2).ToList();
    bc.PendingTransactions.Clear();
    foreach (var tx in top2) bc.PendingTransactions.Add(tx);

    await bc.MineBlockAsync(miner.Address);

    // Restore remaining spam to mempool
    foreach (var tx in remaining) bc.PendingTransactions.Add(tx);

    Console.WriteLine($"\n  Mempool after mining ({bc.PendingTransactions.Count} txs remain — spam stays):");
    foreach (var tx in bc.PendingTransactions)
        Console.WriteLine($"    Fee={tx.Fee}");

    Console.WriteLine("\n" + new string('=', 60));
    Console.WriteLine("=== DEMO COMPLETE ===");
    Console.WriteLine(new string('=', 60) + "\n");
}
*/