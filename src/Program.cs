using System.Reflection;
using System.Text.Json;

using Binance.Net.Clients;

using CryptoExchange.Net.Authentication;

using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets(Assembly.GetExecutingAssembly());
var configuration = builder.Build();

var binanceRestClient = new BinanceRestClient(options =>
{
    options.ApiCredentials = new ApiCredentials(configuration["API_KEY"], configuration["API_SECRET"]);
});

var binanceConvertTradeList = new List<Binance.Net.Objects.Models.Spot.Convert.BinanceConvertTrade>();

var startDate = DateTime.Parse(configuration["START_DATE"]);

while (startDate <= DateTime.Today)
{
    var endDate = startDate.AddDays(30);

    Console.WriteLine($"listing trade history from {startDate.ToString("dd/MM/yyyy")} to {endDate.ToString("dd/MM/yyyy")}");

    var currentTradeHistory = await binanceRestClient.SpotApi.Trading.GetConvertTradeHistoryAsync(startDate, endDate);
    if (currentTradeHistory.Success)
        binanceConvertTradeList.AddRange(currentTradeHistory.Data.Data);

    startDate = endDate;
}

Console.WriteLine();
foreach (var historyItem in binanceConvertTradeList.Select(s => new { Pair = s.QuoteAsset + s.BaseAsset, s.QuoteAsset, s.BaseAsset, s.CreateTime }))
{
    Console.WriteLine($"historyItem={historyItem}");
}

var integrationPairs = JsonSerializer.Serialize(binanceConvertTradeList.Select(s => s.QuoteAsset + s.BaseAsset).OrderBy(q => q).ToList());
Console.WriteLine();
Console.WriteLine($"integrationPair={integrationPairs}");

Console.ReadLine();
