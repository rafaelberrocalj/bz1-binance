using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using Binance.Net.Objects.Models.Spot.Convert;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text.Json;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets(Assembly.GetExecutingAssembly());
var configuration = builder.Build();

Console.WriteLine($"bz1-binance starting");

ArgumentNullException.ThrowIfNullOrWhiteSpace(configuration["API_KEY"], "API_KEY");
ArgumentNullException.ThrowIfNullOrWhiteSpace(configuration["API_SECRET"], "API_SECRET");
ArgumentNullException.ThrowIfNullOrWhiteSpace(configuration["START_DATE"], "START_DATE");

var binanceRestClient = new BinanceRestClient(options =>
{
    options.ApiCredentials = new ApiCredentials(configuration["API_KEY"]!, configuration["API_SECRET"]!);
});

var binanceConvertTradeList = new List<BinanceConvertTrade>();
var binanceFiatWithdrawDepositList = new List<BinanceFiatWithdrawDeposit>();

var dateFormat = configuration["DATE_FORMAT"] ?? "yyyy/MM/dd";
var startDate = DateTime.Parse(configuration["START_DATE"]!);

async Task FetchData()
{
    Console.WriteLine();
    Console.WriteLine($" ~~~ {nameof(FetchData)} ~~~ ");

    while (startDate < DateTime.Today)
    {
        var endDate = startDate.AddDays(30);

        if (endDate > DateTime.Today)
            endDate = DateTime.Today.AddDays(1).AddMilliseconds(-1);

        Console.WriteLine($"* listing values from {startDate.ToString(dateFormat)} to {endDate.ToString(dateFormat)}");

        var fiatDepositWithdrawHistory = await binanceRestClient.SpotApi.Account.GetFiatDepositWithdrawHistoryAsync(TransactionType.Deposit, startTime: startDate, endTime: endDate);
        if (fiatDepositWithdrawHistory.Success)
        {
            binanceFiatWithdrawDepositList.AddRange(fiatDepositWithdrawHistory.Data);

            Console.WriteLine($"  - fiat deposit history with {fiatDepositWithdrawHistory.Data.Count()} results");
        }

        var currentTradeHistory = await binanceRestClient.SpotApi.Trading.GetConvertTradeHistoryAsync(startDate, endDate);
        if (currentTradeHistory.Success)
        {
            binanceConvertTradeList.AddRange(currentTradeHistory.Data.Data);

            Console.WriteLine($"  - convert trade history with {currentTradeHistory.Data.Data.Count()} results");
        }

        startDate = endDate;
    }
}

void ShowDepositHistory()
{
    Console.WriteLine();
    Console.WriteLine($" ~~~ {nameof(ShowDepositHistory)} ~~~ ");

    var validDepositHistory = binanceFiatWithdrawDepositList.Where(w => w.Status == FiatWithdrawDepositStatus.Successful);

    foreach (var depositItem in validDepositHistory.Select(s => new
    {
        s.CreateTime,
        s.UpdateTime,
        s.Quantity,
        s.FiatAsset
    }))
    {
        Console.WriteLine($"depositItem={depositItem}");
    }

    Console.WriteLine($"depositHistoryTotal={validDepositHistory.Sum(s => s.Quantity)}");
}

void ShowIntegrationPairsWithTickers()
{
    Console.WriteLine();
    Console.WriteLine($" ~~~ {nameof(ShowIntegrationPairsWithTickers)} ~~~ ");

    var integrationPairs = new List<string>();
    var tickers = new List<string>();

    foreach (var historyItem in binanceConvertTradeList.Select(s => new
    {
        Pair = s.QuoteAsset + s.BaseAsset,
        s.QuoteAsset,
        s.BaseAsset,
        CreateLocalTime = s.CreateTime.ToLocalTime(),
        CreateUniversalTime = s.CreateTime.ToUniversalTime()
    }))
    {
        Console.WriteLine($"historyItem={historyItem}");

        integrationPairs.Add(historyItem.Pair);
        tickers.Add(historyItem.QuoteAsset);
        tickers.Add(historyItem.BaseAsset);
    }

    Console.WriteLine($"integrationPair={JsonSerializer.Serialize(integrationPairs.Distinct().OrderBy(q => q))}");

    Console.WriteLine($"tickers={JsonSerializer.Serialize(tickers.Distinct().OrderBy(q => q))}");
}

await FetchData();
ShowDepositHistory();
ShowIntegrationPairsWithTickers();

Console.ReadLine();
