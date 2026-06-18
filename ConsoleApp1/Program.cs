using BlockChain_01.Models;
using BlockChain_01.Services;

//static void PrintResult(string testName, bool passed)
//{
//    string status = passed ? "[TEST COMPLETED]" : "[TEST FAILED]";
//    Console.WriteLine($"{status} {testName}\n");
//}
//void TestSystem()
//{
//    Console.WriteLine("=== Test 1: Default workload ===");
//    var bc = new BlockChainService();
//    bc.AddBlock("Alice", "TX: Alice->Bob: 10");
//    bc.AddBlock("Bob", "TX: Bob->Carol: 5");
//    bc.AddBlock("Carol", "TX: Carol->Alice: 2");
//    bool valid = bc.IsValid();
//    Console.WriteLine($"IsValid = {valid}");
//    PrintResult("Default workload", valid == true);

//    Console.WriteLine("=== Test 2: Corrupted Duration ===");
//    bc = new BlockChainService();
//    bc.AddBlock("Alice", "TX: Alice->Bob: 10");
//    bc.AddBlock("Bob", "TX: Bob->Carol: 5");

//    Block last = bc.Chain.Last();
//    last.MiningDuration = 9999;
//    var hs = new HashingService();
//    last.Hash = hs.ComputeHash(last);

//    valid = bc.IsValid();
//    Console.WriteLine($"IsValid = {valid} — Duration Corrupted!");
//    PrintResult("[Attack]: Corrupted Duration", valid == false);

//    Console.WriteLine("=== Test 3: Negative Duration ===");
//    bc = new BlockChainService();
//    bc.AddBlock("Alice", "TX: Alice->Bob: 10");
//    bc.AddBlock("Bob", "TX: Bob->Carol: 5");

//    last = bc.Chain.Last();
//    last.MiningDuration = -10;
//    hs = new HashingService();
//    last.Hash = hs.ComputeHash(last);

//    valid = bc.IsValid();
//    Console.WriteLine($"IsValid = {valid}");
//    PrintResult("[Attack]: Negative Duration", valid == false);

//    Console.WriteLine("=== Test 4: Difficulty limit ===");
//    bc = new BlockChainService(targetBlockTime: 500);
//    Console.WriteLine($"Initial difficulty: {bc.Difficulty}");

//    bc.AddBlock("A", "block 1");
//    bc.AddBlock("B", "block 2");
//    bc.AddBlock("C", "block 3");
//    bc.AddBlock("D", "block 4");

//    int d = bc.Difficulty;
//    Console.WriteLine($"Difficulty after 4 blocks: {d}");
//    Console.WriteLine($"Max possible with +1 limit: 5");

//    bool limitHeld = d >= 2 && d <= 5;
//    PrintResult("[Attack]: Difficulty limit", limitHeld);
//}

//async Task TestVanityMining()
//{
//    Console.WriteLine("=== Vanity Mining Demo (target prefix: \"cafe\") ===");
//    var bc = new BlockChainService();
//    await bc.AddBlockAsync("Alice", "TX: Alice->Bob: 10");
//    await bc.AddBlockAsync("Bob", "TX: Bob->Carol: 5");
//    await bc.AddBlockAsync("Carol", "TX: Carol->Alice: 2");

//    foreach (var b in bc.Chain)
//        Console.WriteLine($"Index {b.Index} | Hash: {b.Hash}");

//    PrintResult("Vanity Mining", bc.IsValid());
//}


var blockchain = new BlockChainService();
var display = new BlockChainDisplayService(blockchain);

void RunMalleabilityDemo()
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
        bigTxs.Add(new Transaction(new string('A', 20) + i, new string('B', 20) + i, 1000 + i));

    blockchain.AddBlock(bigTxs);
    var lastBlock = blockchain.Chain.Last();
    int weight = lastBlock.Transactions.Sum(t => System.Text.Encoding.UTF8.GetByteCount(t.ToRawString()));
    Console.WriteLine($"Передано транзакцій: {bigTxs.Count}, влізло у блок: {lastBlock.Transactions.Count}");
    Console.WriteLine($"Фінальна вага блоку: {weight} байт (ліміт {blockchain.MaxBlockSizeBytes})");
}

Console.WriteLine("Blockchain initiated :)");
Console.WriteLine($"Total cores count: {Environment.ProcessorCount}");
Console.WriteLine($"Total cores in use count: {Environment.ProcessorCount / 2}");


var trans1 = new Transaction("Alice", "Bob", 10);
var trans2 = new Transaction("Bob", "Mark", 100);
var trans3 = new Transaction("Anton", "Marie", 50);


string? choice;

do
{
    Console.WriteLine(new string('=', 50));
    Console.WriteLine("1: Display Blockchain");
    Console.WriteLine("2: Add Block (multithreaded)");
    Console.WriteLine("3: Add Block in simulated net");
    Console.WriteLine("4: Validate BlockChain");
    Console.WriteLine("5: Get Block by Index");
    Console.WriteLine("6: Initiate testing");
    Console.WriteLine("7: Vanity Mining Demo");
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
            Console.Write("How many coins to generate: ");
            if (!int.TryParse(Console.ReadLine(), out int c)) break;

            for (int i = 0; i < c; i++)
            {
                await blockchain.AddBlockAsync(new List<Transaction> { trans1, trans2, trans3 });
            }
            break;

        case "3":
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

                bool mined = await blockchain.AddBlockAsync(new List<Transaction> { trans1, trans2, trans3 }, cts.Token);

                if (mined)
                {
                    Console.WriteLine("[Network] Block generated. Adding it.");
                    cts.Cancel();
                }

                try { await networkSimTask; } catch { }
            }
            break;

        case "4":
            if (blockchain.IsValid())
                Console.WriteLine("All Blockchain is valid");
            else
            {
                Console.WriteLine("Integrity compromised!");
                display.PrintBlock(null, null, blockchain.GetInvalidBlockIndex());
            }
            break;

        case "5":
            Console.Write("Block Index: ");
            if (int.TryParse(Console.ReadLine(), out int idx))
                display.PrintBlock(null, null, idx - 1);
            break;
        case "6":
            RunMalleabilityDemo();
            break;
        case "7":
            Console.WriteLine("FIX THIS.");
            //await TestVanityMining();
            break;
        default:
            if (choice != "0")
                Console.WriteLine("Incorrect. Try again.");
            break;
    }
}
while (choice != "0");

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