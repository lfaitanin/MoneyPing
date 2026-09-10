# 💸 MoneyPing

> **Ping your expenses. MoneyPing handles the rest.**

MoneyPing is a lightweight personal finance automation bot built with .NET and Telegram.

Send a transaction in natural language and MoneyPing turns it into structured financial data, detects whether it is an expense or income, resolves its category and account, and generates a Cashew App Link ready for confirmation.

```text
spent 25 at Lidl using AIB
```

MoneyPing replies with something like:

```text
🧾 Lidl
💶 €25.00 · Expense
🏷 Groceries
🏦 AIB
📅 10/09/2026

[ ✅ Add to Cashew ]
```

---

## ✨ Features

- Natural-language transaction input through Telegram
- Expense and income detection
- Amount extraction and validation
- Merchant/source extraction
- Automatic category resolution
- Account detection
- Date parsing (`today` / `yesterday`)
- Ambiguous transaction rejection
- Telegram user allow-list for private usage
- Cashew App Link generation
- Inline confirmation before creating transactions
- Unit tests with xUnit
- Zero LLM/API cost in V1

MoneyPing deliberately keeps V1 deterministic and inexpensive. Transactions are currently parsed using regex, rules, and heuristics rather than an LLM.

---

## 🛠 Tech Stack

- .NET 10
- C#
- Telegram.Bot
- xUnit
- Cashew App Links
- JSON-based category rules

---

## 🧠 How It Works

```text
Telegram
    │
    ▼
Natural-language message
    │
    ▼
TransactionParser
    │
    ├── Detect transaction type
    ├── Extract amount
    ├── Extract merchant/source
    ├── Extract account
    └── Resolve date
    │
    ▼
CategoryResolver
    │
    ├── merchant-rules.json
    └── income-rules.json
    │
    ▼
CashewLinkBuilder
    │
    ▼
Telegram inline button
    │
    ▼
Cashew App Link
```

The Telegram bot does **not** directly modify a remote Cashew database.

Instead, MoneyPing generates a Cashew App Link containing the structured transaction. When the user taps the button, Cashew handles the transaction on the device.

---

# Getting Started

## 1. Requirements

Install:

- .NET 10 SDK
- Telegram
- Cashew

Verify your .NET installation:

```bash
dotnet --version
```

---

## 2. Create a Telegram Bot

Open Telegram and start a conversation with:

```text
@BotFather
```

Create a new bot:

```text
/newbot
```

Choose a bot name and username.

BotFather will return an API token similar to:

```text
1234567890:AAxxxxxxxxxxxxxxxxxxxxxxxx
```

Keep this token private.

Never commit it to Git.

---

## 3. Clone the Project

```bash
git clone https://github.com/YOUR_USERNAME/moneyping.git
cd moneyping
```

Restore dependencies:

```bash
dotnet restore
```

---

## 4. Configure Development Secrets

MoneyPing uses the standard .NET configuration system.

For local development, use **.NET User Secrets** instead of storing credentials inside `appsettings.json`.

Initialize User Secrets if needed:

```bash
dotnet user-secrets init
```

Set your Telegram token:

```bash
dotnet user-secrets set "Telegram:BotToken" "YOUR_BOT_TOKEN"
```

Initially, you can run the bot without restricting the Telegram user.

Start MoneyPing:

```bash
dotnet run
```

Then send:

```text
/whoami
```

to your Telegram bot.

MoneyPing will return your Telegram numeric user ID.

Store it:

```bash
dotnet user-secrets set "Telegram:AllowedUserId" "YOUR_TELEGRAM_USER_ID"
```

Restart the application.

Now only your Telegram account can interact with the bot.

---

## 5. Configure Categories

MoneyPing uses rule files to map merchants and income sources to Cashew categories.

### Expenses

Edit:

```text
merchant-rules.json
```

Example:

```json
{
  "lidl": "Groceries",
  "aldi": "Groceries",
  "tesco": "Groceries",
  "dunnes": "Groceries",
  "dealz": "Groceries",

  "luas": "Transport",
  "dublin bus": "Transport",
  "irish rail": "Transport",
  "uber": "Transport",
  "free now": "Transport",

  "mcdonald": "Eating Out",
  "burger king": "Eating Out",
  "deliveroo": "Eating Out",
  "just eat": "Eating Out",

  "48": "Bills",
  "chatgpt": "Subscriptions",
  "netflix": "Subscriptions",
  "spotify": "Subscriptions",

  "rent": "Bills and fees",
  "trash": "Bills and fees",
  "internet": "Bills and fees",
  "energy": "Bills and fees"
}
```

The category names must match your Cashew categories.

### Income

Edit:

```text
income-rules.json
```

Example:

```json
{
  "tk maxx": "Income",
  "salary": "Income",
  "wage": "Income",
  "paycheck": "Income",

  "freelance": "Income",
  "client": "Income",
  "consulting": "Income",

  "refund": "Income",
  "reimbursement": "Income",
  "cashback": "Income",
  "interest": "Income",

  "sale": "Income",
  "sold": "Income"
}
```

---

# Usage

Start MoneyPing:

```bash
dotnet run
```

Open your bot on Telegram and send transactions such as:

```text
spent 25 at Lidl using AIB
```

```text
spent 4.20 on Luas using Revolut
```

```text
paid 8.99 at Dealz yesterday using AIB
```

```text
received 300 from TK Maxx
```

```text
received 150 from freelance work
```

MoneyPing requires the transaction type to be clear.

For example:

```text
25 at Lidl using AIB
```

will be rejected because MoneyPing cannot safely determine whether the transaction is an expense or income.

This prevents accidental transactions from being created with the wrong type.

---

## 📅 Date Parsing

V1 intentionally supports a small set of date expressions.

Currently supported:

```text
today
yesterday
```

Examples:

```text
spent 20 at Tesco today
```

```text
spent 12 at Lidl yesterday
```

If no date is specified, MoneyPing uses the current date.

---

## 🏦 Accounts

MoneyPing can extract an account from the message.

Example:

```text
spent 25 at Lidl using AIB
```

Produces approximately:

```json
{
  "amount": -25,
  "title": "Lidl",
  "category": "Groceries",
  "account": "AIB"
}
```

Another example:

```text
spent 25 at Lidl using Revolut
```

uses Revolut instead.

If MoneyPing cannot identify an account, Cashew can handle account selection when the transaction is opened.

---

# Testing

MoneyPing uses xUnit for automated tests.

Run:

```bash
dotnet test
```

Tests currently cover scenarios such as:

```text
Expense parsing
Income parsing
Missing amount
Missing transaction type
Yesterday date parsing
```

Example:

```csharp
[Fact]
public void Should_Parse_Expense()
{
    var result = _parser.Parse(
        "spent 25 at Lidl using AIB");

    Assert.True(result.Success);

    var transaction = result.Transaction!;

    Assert.Equal(-25m, transaction.Amount);
    Assert.Equal("Lidl", transaction.Title);
}
```

Unit tests are especially important because the V1 parser is deterministic and rule-based. They help ensure that adding support for new sentence structures does not break existing behavior.

---

# 🔐 Configuration & Security

Do not store secrets inside:

```text
appsettings.json
```

or:

```text
appsettings.Production.json
```

These files should only contain non-sensitive configuration and may safely be committed to Git when no secrets are present.

For development:

```text
.NET User Secrets
```

For production:

```text
Platform Secret Store
        ↓
Environment Variables
        ↓
.NET IConfiguration
```

Example production environment variables:

```text
DOTNET_ENVIRONMENT=Production

Telegram__BotToken=YOUR_TOKEN
Telegram__AllowedUserId=YOUR_TELEGRAM_ID
```

The double underscore:

```text
Telegram__BotToken
```

maps to:

```text
Telegram:BotToken
```

inside .NET configuration.

---

# ⚠️ Current Limitations

MoneyPing V1 intentionally stays small.

It currently expects one transaction per message and relies on deterministic parsing.

Complex messages such as:

```text
spent 25 at Lidl and 4.20 on Luas using AIB
```

are not yet fully supported.

The current parser also recognizes only a limited vocabulary for transaction types, dates, accounts, and sentence structures.

---

# 🤖 Roadmap

The next major version will introduce an LLM-backed parser for complex natural-language inputs while keeping the current deterministic parser for simple transactions.

Instead of relying exclusively on regex, an LLM will convert natural language into structured transaction data.

For example:

```text
I spent 25 at Lidl and then 4.20 on the Luas, both using AIB
```

could become:

```json
{
  "transactions": [
    {
      "type": "expense",
      "amount": 25.00,
      "title": "Lidl",
      "account": "AIB"
    },
    {
      "type": "expense",
      "amount": 4.20,
      "title": "Luas",
      "account": "AIB"
    }
  ]
}
```

MoneyPing will still keep execution deterministic:

```text
LLM
 │
 │ understands natural language
 ▼
Structured transaction
 │
 ▼
.NET validation
 │
 ▼
Category resolution
 │
 ▼
Cashew integration
```

The model interprets the request.

The application validates and executes it.

Planned improvements include multi-transaction messages, richer date parsing, learned merchant rules, transaction confirmation, additional budgeting integrations, Docker support, and 24/7 deployment.

---

## 🚧 Project Status

**V1 — Working MVP**

The current version supports single expense/income transactions, merchant categorization, account detection, date parsing, Telegram confirmation, and Cashew integration.

AI-assisted parsing and multi-transaction messages are planned for V2.

---

## Why MoneyPing?

Personal finance tracking is useful.

Opening a budgeting app every time you buy something is not.

MoneyPing reduces that friction to a Telegram message:

```text
spent 4.20 on Luas using AIB
```

Ping it.

Track it.

Done. 💸

---

## 📄 License

This project is licensed under the MIT License.