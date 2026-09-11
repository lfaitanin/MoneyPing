using System.Globalization;
using MoneyPing.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text;

var builder = Host.CreateApplicationBuilder(args);

var telegramToken = builder.Configuration["Telegram:BotToken"];

if (string.IsNullOrWhiteSpace(telegramToken))
{
    Console.Error.WriteLine("Missing TELEGRAM_BOT_TOKEN environment variable.");
    return;
}

var openAiApiKey =
    builder.Configuration["OpenAI:ApiKey"];

var openAiModel =
    builder.Configuration["OpenAI:Model"]
    ?? "gpt-5.1";

if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    throw new InvalidOperationException(
        "OpenAI:ApiKey is required.");
}

var allowedUserId =
    builder.Configuration.GetValue<long?>(
        "Telegram:AllowedUserId");

var cashewAccount = builder.Configuration["Cashew:Account"];
var expenseRulesPath = Path.Combine(AppContext.BaseDirectory, "merchant-rules.json");
var incomeRulesPath = Path.Combine(AppContext.BaseDirectory, "income-rules.json");

var ruleBasedParser =
    new RuleBasedTransactionParser();

var llmParser =
    new LlmTransactionParser(
        openAiApiKey,
        openAiModel);

ITransactionParser parser =
    new HybridTransactionParser(
        ruleBasedParser,
        llmParser);

var expenseCategoryResolver =
    new CategoryResolver(expenseRulesPath);

var incomeCategoryResolver =
    new CategoryResolver(incomeRulesPath);

var linkBuilder = new CashewLinkBuilder();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

var bot = new TelegramBotClient(telegramToken, cancellationToken: cts.Token);
var me = await bot.GetMe();

await bot.DeleteWebhook();
await bot.DropPendingUpdates();

bot.OnError += (exception, source) =>
{
    Console.Error.WriteLine($"Telegram error ({source}): {exception}");
    return Task.CompletedTask;
};

bot.OnMessage += async (message, _) =>
{
    if (message.Text is not { } text || message.From is null)
        return;

    if (allowedUserId.HasValue && message.From.Id != allowedUserId.Value)
    {
        await bot.SendMessage(message.Chat, "⛔ This is a private bot.");
        return;
    }

    if (text.Equals("/whoami", StringComparison.OrdinalIgnoreCase))
    {
        await bot.SendMessage(message.Chat,
            $"Your Telegram user ID is: {message.From.Id}\n" +
            "Configure ALLOWED_TELEGRAM_USER_ID with this value.");
        return;
    }

    if (text.Equals("/start", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("/help", StringComparison.OrdinalIgnoreCase))
    {
        await bot.SendMessage(
            message.Chat,
            "💸 MoneyPing\n\n" +
            "Send me a transaction in natural language:\n\n" +
            "• spent 18.50 at Lidl\n" +
            "• paid €4.20 for Luas today\n" +
            "• spent 32.40 at Tesco using AIB\n" +
            "• received 150 from freelance work\n\n" +
            "I'll parse it, prepare the transaction, and generate a button to add it to Cashew."
        );
        return;
    }

    var result = await parser.ParseAsync(message.Text);
        
    if (!result.Success || result.Transaction is null)
    {
        await bot.SendMessage(message.Chat, $"❌ {result.Error}");
        return;
    }

    foreach (var transaction in result.Transactions)
    {
        var isIncome = transaction.Amount > 0;

        transaction.Category = isIncome
            ? incomeCategoryResolver.Resolve(transaction.Title)
            : expenseCategoryResolver.Resolve(transaction.Title);
    }
    var sendText = new StringBuilder();

    sendText.AppendLine($"💸 {result.Transactions.Count} transactions found");
    sendText.AppendLine();

    var buttons = new List<InlineKeyboardButton[]>();

    for (var i = 0; i < result.Transactions.Count; i++)
    {
        var transaction = result.Transactions[i];

        var isIncome = transaction.Amount > 0;

        var typeLabel = isIncome
            ? "Income"
            : "Expense";

        var categoryLabel =
            transaction.Category ?? "Category not detected.";

        var accountLabel =
            transaction.Account ?? "Account not detected.";

        sendText.AppendLine($"{i + 1}️⃣ {transaction.Title}");
        sendText.AppendLine(
            $"💶 €{Math.Abs(transaction.Amount):0.00} · {typeLabel}");
        sendText.AppendLine($"🏷 {categoryLabel}");
        sendText.AppendLine($"🏦 {accountLabel}");
        sendText.AppendLine();

        var cashewUrl = linkBuilder.Build(transaction);
        buttons.Add(
        [
            InlineKeyboardButton.WithUrl(
                    $"✅ Add {transaction.Title}",
                    cashewUrl)
        ]);
    }

    if (result.Transactions.Count > 1)
    {
        var addAllUrl =
            linkBuilder.BuildMany(result.Transactions);

        buttons.Add(
        [
            InlineKeyboardButton.WithUrl(
                "✅ Add all to Cashew",
                addAllUrl)
        ]);
    }
    
    var keyboard = new InlineKeyboardMarkup(buttons);

    await bot.SendMessage(
        message.Chat,
        sendText.ToString(),
        replyMarkup: keyboard);
};

Console.WriteLine($"@{me.Username} is running. Press Ctrl+C to stop.");

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}
