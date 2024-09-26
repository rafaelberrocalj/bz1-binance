using Binance.Net.Clients;
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

var binanceConvertTradeList = new List<Binance.Net.Objects.Models.Spot.Convert.BinanceConvertTrade>();

var startDate = DateTime.Parse(configuration["START_DATE"]!);
var dateFormat = "yyyy/MM/dd";

Console.WriteLine();
while (startDate < DateTime.Today)
{
    var endDate = startDate.AddDays(30);

    if (endDate > DateTime.Today)
        endDate = DateTime.Today.AddDays(1).AddMilliseconds(-1);

    var currentTradeHistory = await binanceRestClient.SpotApi.Trading.GetConvertTradeHistoryAsync(startDate, endDate);
    if (currentTradeHistory.Success)
    {
        binanceConvertTradeList.AddRange(currentTradeHistory.Data.Data);

        Console.WriteLine($"listing trade history from {startDate.ToString(dateFormat)} to {endDate.ToString(dateFormat)} with {currentTradeHistory.Data.Data.Count()} results");
    }
    else
    {
        Console.WriteLine($"listing trade history from {startDate.ToString(dateFormat)} to {endDate.ToString(dateFormat)} with error {currentTradeHistory.Error?.Message}");
    }

    startDate = endDate;
}

var integrationPairs = new List<string>();
var tickers = new List<string>();

Console.WriteLine();
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

Console.WriteLine();
Console.WriteLine($"integrationPair={JsonSerializer.Serialize(integrationPairs.Distinct().OrderBy(q => q))}");

Console.WriteLine();
Console.WriteLine($"tickers={JsonSerializer.Serialize(tickers.Distinct().OrderBy(q => q))}");

Console.ReadLine();
