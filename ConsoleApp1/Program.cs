using BlockChain_01.Models;
using BlockChain_01.Services;

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


var blockchain = new BlockChainService();
var display = new BlockChainDisplayService(blockchain);

Console.WriteLine("Blockchain initiated :)");
Console.WriteLine($"Total cores count: {Environment.ProcessorCount}");
Console.WriteLine($"Total cores in use count: {Environment.ProcessorCount / 2}");

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
                await blockchain.AddBlockAsync("SYSTEM",
                    $"Autocoin gen. Coin# {blockchain.Chain.Last().Index + 1}");
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

                bool mined = await blockchain.AddBlockAsync(
                    "SYSTEM",
                    $"Autocoin gen. Coin# {blockchain.Chain.Last().Index + 1}",
                    cts.Token);

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
            TestSystem();
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
