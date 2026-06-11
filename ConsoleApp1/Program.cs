using BlockChain_01.Services;

var blockchain = new BlockChainService();
var display = new BlockChainDisplayService(blockchain);

Console.WriteLine("Blockchain initiated :)");
Console.WriteLine($"Total cores count: {Environment.ProcessorCount}");
Console.WriteLine($"Total cores in use count: {Environment.ProcessorCount/2}");

string? choice;

do
{
    Console.WriteLine(new string('=', 50));
    Console.WriteLine("1: Display Blockchain");
    Console.WriteLine("2: Add Block (multithreaded)");
    Console.WriteLine("3: Add Block in simulated net");
    Console.WriteLine("4: Validate BlockChain");
    Console.WriteLine("5: Get Block by Index");
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
            Console.Write("Data: "); var data = Console.ReadLine() ?? "";
            Console.Write("Initiator: "); var author = Console.ReadLine() ?? "";
            await blockchain.AddBlockAsync(author, data);
            break;

        case "3":
            Console.Write("Data: "); var netData = Console.ReadLine() ?? "";
            Console.Write("Initiator: "); var netAuthor = Console.ReadLine() ?? "";

            using (var cts = new CancellationTokenSource())
            {
                var networkSimTask = Task.Run(async () =>
                {

                    int delay = Random.Shared.Next(2000, 8000);
                    Console.WriteLine($"[Network] Other node is generating block. Awaiting answe in ~{delay / 1000}s");
                    await Task.Delay(delay);

                    if (!cts.Token.IsCancellationRequested)
                    {
                        Console.WriteLine("\n[Network] Block has  already been generated. Canceling...");
                        cts.Cancel();
                    }
                });

                bool mined = await blockchain.AddBlockAsync(netAuthor, netData, cts.Token);

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
