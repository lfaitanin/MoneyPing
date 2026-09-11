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

var intentDetector =
    new AdvancedIntentDetector();

ITransactionParser parser =
    new HybridTransactionParser(
        ruleBasedParser,
        llmParser,
        intentDetector);

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

    if (result.Transfers.Count > 0)
    {
        var transfer =
            result.Transfers[0];

        var sendTextTransfer =
            new StringBuilder();

        sendTextTransfer.AppendLine("🔄 Transfer detected");
        sendTextTransfer.AppendLine();

        sendTextTransfer.AppendLine(
            $"💶 €{transfer.Amount:0.00}");

        sendTextTransfer.AppendLine(
            $"🏦 {transfer.SourceAccount} → {transfer.DestinationAccount}");

        sendTextTransfer.AppendLine(
            $"📅 {transfer.Date:dd MMM yyyy}");

        var url =
            linkBuilder.BuildTransfer(transfer);

        var keyboardTransfer =
            new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithUrl(
                        "✅ Add transfer to Cashew",
                        url)
                ]
            ]);

        await bot.SendMessage(
            message.Chat,
            sendTextTransfer.ToString(),
            replyMarkup: keyboardTransfer);

        return;
    }

    if (result.Transactions.Count == 0)
    {
        await bot.SendMessage(
            message.Chat, "❌ No transaction found.");

        return;
    }
    
    if (!result.Success)
    {
        await bot.SendMessage(message.Chat,$"❌ {result.Error}");
        return;
    }
    
    var sendText = new StringBuilder();

    foreach (var transaction in result.Transactions)
    {
        var isIncome = transaction.Amount > 0;

        var classification = isIncome ? incomeCategoryResolver.Resolve(transaction.Title)
                                        : expenseCategoryResolver.Resolve(transaction.Title);

        transaction.Category = classification?.Category;
        transaction.Subcategory = classification?.Subcategory;

    }
    

    sendText.AppendLine( result.Transactions.Count == 1 ? "💸 1 transaction found" 
                        : $"💸 {result.Transactions.Count} transactions found");
                        sendText.AppendLine();

    var buttons = new List<InlineKeyboardButton[]>();
    var recurrenceScheduleGenerator = new RecurrenceScheduleGenerator();

    for (var i = 0; i < result.Transactions.Count; i++)
    {
        var transaction = result.Transactions[i];

        var isIncome = transaction.Amount > 0;

        var typeLabel = isIncome
            ? "Income"
            : "Expense";

        var categoryLabel =
            transaction.Category ?? "Category not detected.";

        var accountLabel = string.IsNullOrWhiteSpace(transaction.Account)
                            ? "Account not detected."
                            : transaction.Account;

        sendText.AppendLine($"{i + 1}️⃣ {transaction.Title}");
        sendText.AppendLine($"💶 €{Math.Abs(transaction.Amount):0.00} · {typeLabel}");
        sendText.AppendLine($"🏷 {categoryLabel}");
        if (!string.IsNullOrWhiteSpace(transaction.Subcategory))
            sendText.AppendLine($"↳ {transaction.Subcategory}");
        sendText.AppendLine($"🏦 {accountLabel}");
        sendText.AppendLine($"📅 {transaction.Date:dd MMM yyyy}");
        sendText.AppendLine();

        if (transaction.Recurrence is not null)
        {
            sendText.AppendLine(
                    $"🔁 {transaction.Recurrence}");

            if (transaction.Recurrence.IsBounded)
            {
                var schedule =
                    recurrenceScheduleGenerator.Generate(
                        transaction);
                var firstPayment = schedule.First().Date;
                var lastPayment = schedule.Last().Date;

                var total =
                    schedule.Sum(x => Math.Abs(x.Amount));

                sendText.AppendLine($"📅 {schedule.Count} scheduled payments");
                sendText.AppendLine($"🗓 {firstPayment:dd MMM yyyy} → {lastPayment:dd MMM yyyy}");
                sendText.AppendLine($"💰 Total: €{total:0.00}");

                var addAllUrl =
                    linkBuilder.BuildMany(schedule);

                buttons.Add(
                [
                    InlineKeyboardButton.WithUrl(
                        $"✅ Add all {schedule.Count} payments",
                        addAllUrl)
                ]);
            }
            else
            {
                var recurringUrl =
                    linkBuilder.BuildRoute(transaction);

                buttons.Add(
                [
                    InlineKeyboardButton.WithUrl(
                        $"🔁 Configure {transaction.Title}",
                        recurringUrl)
                ]);
            }
        }
        else
        {
            var url = linkBuilder.Build(transaction);
            buttons.Add(
            [
                InlineKeyboardButton.WithUrl(
                    $"✅ Add {transaction.Title}",
                    url)
            ]);
        }
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
