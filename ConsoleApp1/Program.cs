using BlockChain_01.Models;
using BlockChain_01.Services;

// ── Startup ───────────────────────────────────────────────────────────────────
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.CursorVisible = false;
Banner();

// ── Services (needed before login) ───────────────────────────────────────────
var storage = new FileStorageService();
var logger = new LoggingService("debug.log");

// ── Login / Register ──────────────────────────────────────────────────────────
var savedWallets = storage.LoadWallets();
Wallet device = LoginScreen(savedWallets);

// ── Blockchain + P2P ──────────────────────────────────────────────────────────
var blockchain = new BlockChainService(logger);
var walletService = new WalletService(blockchain.Chain);
var txService = new TransactionService(blockchain);

const int BASE_PORT = 5000;
var p2p = new TCPP2PService(blockchain, BASE_PORT, storage, logger);
p2p.Start();

// Register this wallet with the network
p2p.RegisterWallet(new PeerWalletInfo
{
    Name = device.Name,
    Address = device.Address,
    PublicKey = device.PublicKey
});

// Auto-connect to bootstrap node if we're not it
if (p2p.ListenPort != BASE_PORT)
{
    Console.WriteLine($"[P2P] Connecting to bootstrap at {BASE_PORT}...");
    await p2p.ConnectToPeerAsync("127.0.0.1", BASE_PORT);
}

Console.CursorVisible = true;
Console.Write("\n  Connect to additional peer port (blank to skip): ");
string? extraPeer = Console.ReadLine()?.Trim();
Console.CursorVisible = false;
if (int.TryParse(extraPeer, out int extraPort) && extraPort != p2p.ListenPort)
    await p2p.ConnectToPeerAsync("127.0.0.1", extraPort);

logger.Info("Node", $"Node ready — user '{device.Name}' on port {p2p.ListenPort}");

// Build wallet lookup (all wallets this node manages)
var localWallets = new Dictionary<string, Wallet>(StringComparer.OrdinalIgnoreCase);
foreach (var w in savedWallets) localWallets[w.Name] = w;
localWallets[device.Name] = device; // ensure active wallet is present

// ── Log buffer ────────────────────────────────────────────────────────────────
const int LOG_LINES = 16;
var log = new List<(string text, ConsoleColor color)>();

void Log(string text, ConsoleColor color = ConsoleColor.Gray) =>
    log.Add(("  " + text, color));
void LogOk(string msg) => Log("✓ " + msg, ConsoleColor.Green);
void LogWarn(string msg) => Log("⚠ " + msg, ConsoleColor.Yellow);
void LogInfo(string msg) => Log("  " + msg, ConsoleColor.DarkGray);
void LogLine(string msg = "") => Log(msg, ConsoleColor.DarkGray);
void LogDiv() => Log(new string('─', 54), ConsoleColor.DarkGray);
void LogHead(string title)
{
    LogLine();
    Log($"── {title.ToUpper()} " + new string('─', Math.Max(0, 40 - title.Length)), ConsoleColor.Cyan);
}

// ── Render ────────────────────────────────────────────────────────────────────
void Render()
{
    var visible = log.TakeLast(LOG_LINES).ToList();
    for (int i = visible.Count; i < LOG_LINES; i++) Console.WriteLine();
    int maxW = Math.Max(20, Console.WindowWidth - 1);
    foreach (var (text, color) in visible)
    {
        Console.ForegroundColor = color;
        Console.WriteLine((text.Length > maxW ? text[..maxW] : text).PadRight(maxW));
    }
    Console.ResetColor();
    PrintMenu(device, blockchain);
}

string Ask(string label)
{
    Console.CursorVisible = true;
    Console.Write($"  {label}: ");
    string? v = Console.ReadLine()?.Trim();
    Console.CursorVisible = false;
    return v ?? "";
}

string AskPassword(string label)
{
    Console.CursorVisible = true;
    Console.Write($"  {label}: ");
    var sb = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && sb.Length > 0) { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); }
        else if (key.Key != ConsoleKey.Backspace) { sb.Append(key.KeyChar); Console.Write('*'); }
    }
    Console.WriteLine();
    Console.CursorVisible = false;
    return sb.ToString();
}

// ── Main loop ─────────────────────────────────────────────────────────────────
string? choice;
do
{
    Render();
    Console.CursorVisible = true;
    choice = Console.ReadLine()?.Trim();
    Console.CursorVisible = false;

    switch (choice)
    {
        case "1": HandleViewChain(); break;
        case "2": HandleViewBlock(); break;
        case "3": await HandleMine(); break;
        case "4": HandleViewMempool(); break;
        case "5": await HandleSendTx(); break;
        case "6": HandleBalances(); break;
        case "7": HandleValidate(); break;
        case "8": HandleEconomyAudit(); break;
        case "9": HandleCreateWallet(); break;
        case "P": HandleViewPeers(); break;
        case "S": HandleSwitchWallet(); break;
        case "0": break;
        default:
            if (choice != null) LogWarn("Unknown option.");
            break;
    }
}
while (choice != "0");

Console.CursorVisible = true;
logger.Info("Node", "Shutdown");
Goodbye();

// ── Login screen ──────────────────────────────────────────────────────────────

Wallet LoginScreen(List<Wallet> saved)
{
    Console.CursorVisible = true;
    while (true)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("  ╔══════════════════════════════════════════╗");
        Console.WriteLine("  ║           ACCOUNT LOGIN                  ║");
        Console.WriteLine("  ╠══════════════════════════════════════════╣");
        Console.ResetColor();

        if (saved.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  ║  (no saved wallets)                      ║");
            Console.ResetColor();
        }
        else
        {
            for (int i = 0; i < saved.Count; i++)
            {
                string line = $"  [{i + 1}] {saved[i].Name}";
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write("  ║  ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{i + 1}]");
                Console.ResetColor();
                Console.Write($" {saved[i].Name}".PadRight(37));
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("║");
                Console.ResetColor();
            }
        }

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("  ╠══════════════════════════════════════════╣");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("  ║  ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[N]");
        Console.ResetColor();
        Console.Write(" Create new account".PadRight(37));
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("║");
        Console.WriteLine("  ╚══════════════════════════════════════════╝");
        Console.ResetColor();

        Console.Write("  › ");
        string? pick = Console.ReadLine()?.Trim();

        // Create new account
        if (pick?.ToUpper() == "N" || pick == "0" && saved.Count == 0)
        {
            Console.Write("  Name: ");
            string newName = Console.ReadLine()?.Trim() ?? "User";
            if (string.IsNullOrEmpty(newName)) newName = "User";

            // Check name not taken
            if (saved.Any(w => w.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                PrintColorLine($"  ✗ Account '{newName}' already exists.", ConsoleColor.Red);
                continue;
            }

            string pwd = ReadPassword("  Password: ");
            string pwd2 = ReadPassword("  Confirm:  ");
            if (pwd != pwd2) { PrintColorLine("  ✗ Passwords do not match.", ConsoleColor.Red); continue; }
            if (pwd.Length < 4) { PrintColorLine("  ✗ Password too short (min 4).", ConsoleColor.Red); continue; }

            var wallet = Wallet.Create(newName, pwd);
            saved.Add(wallet);
            storage.SaveWallets(saved);
            PrintColorLine($"  ✓ Account '{newName}' created.", ConsoleColor.Green);
            logger.Info("Login", $"New account created: {newName}");
            Console.CursorVisible = false;
            return wallet;
        }

        // Login to existing account by number
        if (int.TryParse(pick, out int idx) && idx >= 1 && idx <= saved.Count)
        {
            var candidate = saved[idx - 1];
            string pwd = ReadPassword($"  Password for '{candidate.Name}': ");

            if (candidate.Unlock(pwd))
            {
                PrintColorLine($"  ✓ Logged in as '{candidate.Name}'.", ConsoleColor.Green);
                logger.Info("Login", $"Login OK: {candidate.Name}");
                Console.CursorVisible = false;
                return candidate;
            }
            else
            {
                PrintColorLine("  ✗ Wrong password.", ConsoleColor.Red);
                logger.Warn("Login", $"Failed login attempt for '{candidate.Name}'");
            }
        }
        else if (pick != null)
        {
            PrintColorLine("  ✗ Invalid choice.", ConsoleColor.Red);
        }
    }
}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var sb = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && sb.Length > 0) { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); }
        else if (key.Key != ConsoleKey.Backspace) { sb.Append(key.KeyChar); Console.Write('*'); }
    }
    Console.WriteLine();
    return sb.ToString();
}

static void PrintColorLine(string msg, ConsoleColor color)
{
    Console.ForegroundColor = color;
    Console.WriteLine(msg);
    Console.ResetColor();
    System.Threading.Thread.Sleep(800);
}

// ── Handlers ──────────────────────────────────────────────────────────────────

void HandleViewChain()
{
    log.Clear();
    LogHead("Blockchain");
    LogDiv();
    foreach (var block in blockchain.Chain)
    {
        LogLine();
        Log($"  Block #{block.Index}", ConsoleColor.Cyan);
        LogInfo($"  Hash:     {block.Hash}");
        LogInfo($"  PrevHash: {block.PreviousHash}");
        LogInfo($"  Diff: {block.Difficulty}  Nonce: {block.Nonce}  Time: {block.MiningDuration:F2}s");
        LogInfo($"  Stamp: {block.TimeStamp}");
        if (block.Transactions.Count == 0)
            LogInfo("  [Genesis Block]");
        else
            foreach (var tx in block.Transactions)
                LogInfo("  " + tx.ToRawString());
        LogDiv();
    }
    LogInfo($"Total blocks: {blockchain.Chain.Count}");
}

void HandleViewBlock()
{
    string raw = Ask("Block index");
    log.Clear();
    LogHead("View Block");
    if (!int.TryParse(raw, out int idx)) { LogWarn("Invalid index."); return; }
    var block = blockchain.Chain.ElementAtOrDefault(idx);
    if (block == null) { LogWarn($"Block #{idx} not found."); return; }
    LogDiv();
    Log($"  Block #{block.Index}", ConsoleColor.Cyan);
    LogInfo($"  Hash:     {block.Hash}");
    LogInfo($"  PrevHash: {block.PreviousHash}");
    LogInfo($"  Diff: {block.Difficulty}  Nonce: {block.Nonce}  Mined in: {block.MiningDuration:F2}s");
    LogInfo($"  Stamp: {block.TimeStamp}");
    LogDiv();
    if (block.Transactions.Count == 0)
        LogInfo("  [Genesis Block]");
    else
        foreach (var tx in block.Transactions)
            LogInfo("  " + tx.ToRawString());
}

async Task HandleMine()
{
    string mode = Ask("[1] local  [2] network race");
    string rawCount = Ask("How many blocks");
    log.Clear();
    LogHead("Mine Block");
    if (!int.TryParse(rawCount, out int count) || count < 1) { LogWarn("Invalid count."); return; }

    if (mode == "1")
    {
        for (int i = 0; i < count; i++)
        {
            LogInfo($"Mining block {i + 1}/{count}...");
            Render();
            var block = await blockchain.MineBlockAsync(device.Address);
            if (block != null)
            {
                p2p.BroadcastNewBlock(block);
                p2p.BroadcastMempool();
                LogOk($"Block #{block.Index} mined — {block.Hash[..12]}… ({block.MiningDuration:F2}s)");
            }
        }
    }
    else if (mode == "2")
    {
        for (int i = 0; i < count; i++)
        {
            using var cts = new CancellationTokenSource();
            int delay = Random.Shared.Next(2000, 8000);
            LogInfo($"[Net] Simulated peer mining (~{delay / 1000}s)");
            Render();
            _ = Task.Delay(delay).ContinueWith(_ =>
            {
                if (!cts.IsCancellationRequested) { LogWarn("[Net] Other node won."); cts.Cancel(); }
            });
            var mined = await blockchain.MineBlockAsync(device.Address, cts.Token);
            if (mined != null)
            {
                LogOk("[Net] You won!"); p2p.BroadcastNewBlock(mined); p2p.BroadcastMempool(); cts.Cancel();
            }
        }
    }
    else { LogWarn("Invalid mode."); }
}

void HandleViewMempool()
{
    log.Clear();
    LogHead("Mempool");
    if (blockchain.PendingTransactions.Count == 0) { LogLine(); LogInfo("(empty)"); return; }
    LogInfo($"{blockchain.PendingTransactions.Count}/{blockchain.MaxMempoolSize} transactions");
    LogDiv();
    foreach (var tx in blockchain.PendingTransactions.OrderByDescending(t => t.Fee))
        LogInfo($"  {ResolveAddress(tx.From),-14} → {ResolveAddress(tx.To),-14}  {tx.Amount,8} | fee {tx.Fee}");
}

async Task HandleSendTx()
{
    string from = Ask("From (wallet name)");
    string to = Ask("To   (name or offline peer)");
    string rawAmt = Ask("Amount");
    string rawFee = Ask("Fee");
    log.Clear();
    LogHead("Send Transaction");

    if (!localWallets.TryGetValue(from, out var senderWallet))
    { LogWarn($"Wallet '{from}' not found."); return; }

    if (!senderWallet.IsUnlocked)
    {
        string pwd = AskPassword($"Password for '{from}'");
        if (!senderWallet.Unlock(pwd)) { LogWarn("Wrong password."); return; }
    }

    string toAddress;
    if (localWallets.TryGetValue(to, out var localTarget))
        toAddress = localTarget.Address;
    else
    {
        var peer = p2p.KnownWallets.Values.FirstOrDefault(w =>
            w.Name.Equals(to, StringComparison.OrdinalIgnoreCase) || w.Address == to);
        if (peer != null) toAddress = peer.Address;
        else { LogWarn($"Recipient '{to}' not found."); return; }
    }

    if (!decimal.TryParse(rawAmt, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
    { LogWarn("Invalid amount."); return; }
    if (!decimal.TryParse(rawFee, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal fee))
    { LogWarn("Invalid fee."); return; }

    try
    {
        var tx = txService.CreateTransaction(senderWallet, toAddress, amount, senderWallet.PublicKey);
        if (tx == null) { LogWarn("Transaction creation failed."); return; }
        tx.Fee = fee;
        tx.Signature = senderWallet.Sign(tx.GetDataToSign());
        blockchain.AddTransactionToMempool(tx);
        p2p.BroadcastTransaction(tx);
        p2p.BroadcastMempool();
        LogOk($"Sent {amount:F4} from {from} → {to}  (fee {fee:F4})");
        LogInfo($"  Pending balance: {blockchain.GetPendingBalance(senderWallet.Address):F4}");
        logger.Info("Tx", $"{from}→{to} amount={amount} fee={fee}");
    }
    catch (Exception ex) { LogWarn(ex.Message); }
}

void HandleBalances()
{
    log.Clear();
    LogHead("Wallet Balances");
    int w = localWallets.Keys.Max(k => k.Length) + 2;
    LogLine();
    LogInfo($"  {"Name".PadRight(w)} {"Confirmed",12}  {"Pending",12}  (local)");
    LogDiv();
    foreach (var (n, wallet) in localWallets)
    {
        decimal c = walletService.GetBalance(wallet.Address);
        decimal p = blockchain.GetPendingBalance(wallet.Address);
        LogInfo($"  {n.PadRight(w)} {c,12:F4}  {p,12:F4}" + (n == device.Name ? "  ◀ active" : ""));
    }
    if (p2p.KnownWallets.Count > 0)
    {
        LogLine();
        LogInfo($"  {"Name".PadRight(w)} {"Confirmed",12}  (peers)");
        LogDiv();
        foreach (var pw in p2p.KnownWallets.Values)
            LogInfo($"  {pw.Name.PadRight(w)} {walletService.GetBalance(pw.Address),12:F4}");
    }
}

void HandleValidate()
{
    log.Clear();
    LogHead("Validate Blockchain");
    LogDiv();
    bool valid = blockchain.IsValid();
    if (valid) { LogOk("Blockchain is valid."); }
    else
    {
        LogWarn("Integrity compromised!");
        int bad = blockchain.GetInvalidBlockIndex();
        if (bad >= 0)
        {
            LogWarn($"First bad block: #{bad}");
            var b = blockchain.Chain.ElementAtOrDefault(bad);
            if (b != null) { LogDiv(); LogInfo($"  Hash: {b.Hash}"); LogInfo($"  Prev: {b.PreviousHash}"); }
        }
    }
}

void HandleEconomyAudit()
{
    log.Clear();
    LogHead("Economy Audit");
    LogDiv();
    bool ok = blockchain.ValidateEconomy();
    if (ok) LogOk("Economy consistent. Proof of Reserves passed.");
    else LogWarn("Economy mismatch! Possible double-spend or minting error.");
}

void HandleCreateWallet()
{
    string wname = Ask("New wallet name");
    log.Clear();
    LogHead("Create Wallet");
    if (string.IsNullOrEmpty(wname)) { LogWarn("Name cannot be empty."); return; }
    if (localWallets.ContainsKey(wname)) { LogWarn($"'{wname}' already exists."); return; }

    string pwd = AskPassword("Password");
    string pwd2 = AskPassword("Confirm password");
    if (pwd != pwd2) { LogWarn("Passwords do not match."); return; }
    if (pwd.Length < 4) { LogWarn("Password too short (min 4)."); return; }

    var wallet = Wallet.Create(wname, pwd);
    localWallets[wname] = wallet;

    // Persist
    var allWallets = localWallets.Values.ToList();
    storage.SaveWallets(allWallets);

    p2p.RegisterWallet(new PeerWalletInfo
    {
        Name = wname,
        Address = wallet.Address,
        PublicKey = wallet.PublicKey
    });

    LogOk($"Wallet '{wname}' created and saved.");
    LogInfo($"  Address: {wallet.Address[..20]}…");
    logger.Info("Wallet", $"Created '{wname}' addr={wallet.Address[..16]}…");
}

void HandleViewPeers()
{
    log.Clear();
    LogHead("Known Peer Wallets");
    LogDiv();
    if (p2p.KnownWallets.Count == 0) { LogInfo("No peer wallets known yet."); return; }
    foreach (var pw in p2p.KnownWallets.Values)
        LogInfo($"  {pw.Name,-16}  {pw.Address[..20]}…");
    LogLine();
    LogInfo($"Total: {p2p.KnownWallets.Count} peer(s)");
}

void HandleSwitchWallet()
{
    log.Clear();
    LogHead("Switch Active Wallet");
    LogDiv();
    var names = localWallets.Keys.ToList();
    for (int i = 0; i < names.Count; i++)
        LogInfo($"  [{i + 1}] {names[i]}" + (names[i] == device.Name ? "  ◀ active" : ""));
    LogLine();
    string raw = Ask("Pick number");
    if (!int.TryParse(raw, out int idx) || idx < 1 || idx > names.Count)
    { LogWarn("Invalid choice."); return; }

    var candidate = localWallets[names[idx - 1]];
    if (!candidate.IsUnlocked)
    {
        string pwd = AskPassword($"Password for '{candidate.Name}'");
        if (!candidate.Unlock(pwd)) { LogWarn("Wrong password."); return; }
    }
    device = candidate;
    LogOk($"Switched to wallet '{device.Name}'.");
    logger.Info("Login", $"Switched active wallet to '{device.Name}'");
}

// ── Helpers ───────────────────────────────────────────────────────────────────

string ResolveAddress(string address)
{
    if (address == "COINBASE") return "COINBASE";
    var local = localWallets.FirstOrDefault(w => w.Value.Address == address).Key;
    if (local != null) return local;
    var peer = p2p.KnownWallets.Values.FirstOrDefault(w => w.Address == address);
    if (peer != null) return peer.Name;
    return address.Length > 12 ? address[..12] + "…" : address;
}

// ── UI ────────────────────────────────────────────────────────────────────────

void PrintMenu(Wallet w, BlockChainService bc)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine("  ╔══════════════════════════════════════════════╗");
    Console.WriteLine($"  ║  ₿ BLOCKCHAIN NODE  ·  {w.Name,-20}║");
    Console.WriteLine($"  ║  Port: {p2p.ListenPort,-5}  Blocks: {bc.Chain.Count,-5}  Diff: {bc.Difficulty,-5}    ║");
    Console.WriteLine($"  ║  Mempool: {bc.PendingTransactions.Count}/{bc.MaxMempoolSize,-3}  Balance: {walletService.GetBalance(w.Address),-16}║");
    Console.WriteLine("  ╠══════════════════════════════════════════════╣");
    Console.ResetColor();
    MenuItem("1", "View full blockchain");
    MenuItem("2", "View block by index");
    MenuItem("3", "Mine block(s)");
    MenuItem("4", "View mempool");
    MenuItem("5", "Send transaction");
    MenuItem("6", "Wallet balances");
    MenuItem("7", "Validate blockchain");
    MenuItem("8", "Economy audit");
    MenuItem("9", "Create wallet");
    MenuItem("P", "Peer wallets");
    MenuItem("S", "Switch active wallet");
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine("  ╠══════════════════════════════════════════════╣");
    Console.ResetColor();
    MenuItem("0", "Exit");
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine("  ╚══════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.Write("  › ");
}

void MenuItem(string key, string label)
{
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.Write("  ║  ");
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write($"[{key}]");
    Console.ResetColor();
    Console.Write($" {label}".PadRight(39));
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine("║");
    Console.ResetColor();
}

void Banner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine();
    Console.WriteLine("  ██████╗ ██╗      ██████╗  ██████╗██╗  ██╗");
    Console.WriteLine("  ██╔══██╗██║     ██╔═══██╗██╔════╝██║ ██╔╝");
    Console.WriteLine("  ██████╔╝██║     ██║   ██║██║     █████╔╝ ");
    Console.WriteLine("  ██╔══██╗██║     ██║   ██║██║     ██╔═██╗ ");
    Console.WriteLine("  ██████╔╝███████╗╚██████╔╝╚██████╗██║  ██╗");
    Console.WriteLine("  ╚═════╝ ╚══════╝ ╚═════╝  ╚═════╝╚═╝  ╚═╝");
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  Blockchain Node  ·  C#  ·  P2P  ·  ECDSA");
    Console.WriteLine();
    Console.ResetColor();
}

void Goodbye()
{
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine("\n  Node shutting down. Goodbye.\n");
    Console.ResetColor();
}