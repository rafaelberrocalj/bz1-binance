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
Console.WriteLine();

ArgumentNullException.ThrowIfNullOrWhiteSpace(configuration["API_KEY"], "API_KEY");
ArgumentNullException.ThrowIfNullOrWhiteSpace(configuration["API_SECRET"], "API_SECRET");
ArgumentNullException.ThrowIfNullOrWhiteSpace(configuration["START_DATE"], "START_DATE");

var binanceRestClient = new BinanceRestClient(options =>
{
    options.ApiCredentials = new ApiCredentials(configuration["API_KEY"]!, configuration["API_SECRET"]!);
});

var jsonSerializerSettings = new JsonSerializerOptions
{
    WriteIndented = true
};
var localDataStorageFileName = "localDataStorage.json";

Func<string, LocalDataStorage> LoadLocalDataStorage = (string _localDataStorageFileName) =>
{
    Console.WriteLine($" ~~~ {nameof(LoadLocalDataStorage)} ~~~ ");
    if (File.Exists(_localDataStorageFileName))
    {
        var offlineDataFileContent = File.ReadAllText(_localDataStorageFileName);
        return JsonSerializer.Deserialize<LocalDataStorage>(offlineDataFileContent) ?? new LocalDataStorage();
    }
    return new LocalDataStorage();
};

var localDataStorageModel = LoadLocalDataStorage(localDataStorageFileName);

Action<DateTime, DateTime, IEnumerable<BinanceFiatWithdrawDeposit>, IEnumerable<BinanceFiatWithdrawDeposit>, IEnumerable<BinanceConvertTrade>> SaveLocalDataStorage = (startDate, endDate, fiatDeposits, fiatWithdrawals, convertTrades) =>
{
    localDataStorageModel.LastRunDate = endDate;
    localDataStorageModel.FetchedDataList.Add(new LocalDataStorage.FetchedData
    {
        StartDate = startDate,
        EndDate = endDate,
        BinanceConvertTradeList = convertTrades,
        BinanceFiatDepositList = fiatDeposits,
        BinanceFiatWithdrawList = fiatWithdrawals
    });
    var offlineDataFileContent = JsonSerializer.Serialize(localDataStorageModel, jsonSerializerSettings);
    File.WriteAllText(localDataStorageFileName, offlineDataFileContent);
    Console.WriteLine($" ~~~ {nameof(SaveLocalDataStorage)} ~~~ ");
};

var binanceConvertTradeList = new List<BinanceConvertTrade>();
var binanceFiatDepositList = new List<BinanceFiatWithdrawDeposit>();
var binanceFiatWithdrawList = new List<BinanceFiatWithdrawDeposit>();

var dateFormat = configuration["DATE_FORMAT"] ?? "yyyy/MM/dd";
var startDate = DateTime.Parse(configuration["START_DATE"]!);

if (localDataStorageModel.LastRunDate.HasValue)
{
    startDate = localDataStorageModel.LastRunDate.Value;
}

async Task FetchData()
{
    Console.WriteLine($" ~~~ {nameof(FetchData)} ~~~ ");

    while (startDate < DateTime.Today)
    {
        var endDate = startDate.AddDays(60);

        if (endDate > DateTime.Today)
            endDate = DateTime.Today.AddDays(1).AddMilliseconds(-1);

        Console.WriteLine($"* listing values from {startDate.ToString(dateFormat)} to {endDate.ToString(dateFormat)}");

        var fiatDepositHistoryTask = binanceRestClient.SpotApi.Account.GetFiatDepositWithdrawHistoryAsync(TransactionType.Deposit, startTime: startDate, endTime: endDate);
        var fiatWithdrawHistoryTask = binanceRestClient.SpotApi.Account.GetFiatDepositWithdrawHistoryAsync(TransactionType.Withdrawal, startTime: startDate, endTime: endDate);
        var currentTradeHistoryTask = binanceRestClient.SpotApi.Trading.GetConvertTradeHistoryAsync(startDate, endDate);

        await Task.WhenAll(fiatDepositHistoryTask, fiatWithdrawHistoryTask, currentTradeHistoryTask);

        if (fiatDepositHistoryTask.Result.Success)
        {
            binanceFiatDepositList.AddRange(fiatDepositHistoryTask.Result.Data);

            Console.WriteLine($"  - fiat deposit history with {fiatDepositHistoryTask.Result.Data.Count()} results");
        }

        if (fiatWithdrawHistoryTask.Result.Success)
        {
            binanceFiatWithdrawList.AddRange(fiatWithdrawHistoryTask.Result.Data);

            Console.WriteLine($"  - fiat withdrawal history with {fiatWithdrawHistoryTask.Result.Data.Count()} results");
        }

        if (currentTradeHistoryTask.Result.Success)
        {
            binanceConvertTradeList.AddRange(currentTradeHistoryTask.Result.Data.Data);

            Console.WriteLine($"  - convert trade history with {currentTradeHistoryTask.Result.Data.Data.Count()} results");
        }

        SaveLocalDataStorage(
            startDate,
            endDate,
            fiatDepositHistoryTask.Result.Data,
            fiatWithdrawHistoryTask.Result.Data,
            currentTradeHistoryTask.Result.Data.Data
        );

        startDate = endDate;
    }
}

binanceConvertTradeList = localDataStorageModel.FetchedDataList.SelectMany(sm => sm.BinanceConvertTradeList).ToList();
binanceFiatDepositList = localDataStorageModel.FetchedDataList.SelectMany(sm => sm.BinanceFiatDepositList).ToList();
binanceFiatWithdrawList = localDataStorageModel.FetchedDataList.SelectMany(sm => sm.BinanceFiatWithdrawList).ToList();

void ShowDepositHistory()
{
    Console.WriteLine();
    Console.WriteLine($" ~~~ {nameof(ShowDepositHistory)} ~~~ ");

    var validDepositHistory = binanceFiatDepositList
        .Where(w => w.Status == FiatWithdrawDepositStatus.Successful)
        .OrderBy(q => q.CreateTime)
        .ThenBy(q => q.UpdateTime);

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

void ShowWithdrawHistory()
{
    Console.WriteLine();
    Console.WriteLine($" ~~~ {nameof(ShowWithdrawHistory)} ~~~ ");

    var validWithdrawHistory = binanceFiatWithdrawList
        .Where(w => w.Status == FiatWithdrawDepositStatus.Successful)
        .OrderBy(q => q.CreateTime)
        .ThenBy(q => q.UpdateTime);

    foreach (var withdrawItem in validWithdrawHistory.Select(s => new
    {
        s.CreateTime,
        s.UpdateTime,
        s.Quantity,
        s.FiatAsset
    }))
    {
        Console.WriteLine($"withdrawItem={withdrawItem}");
    }

    Console.WriteLine($"withdrawItemHistoryTotal={validWithdrawHistory.Sum(s => s.Quantity)}");
}

void ShowIntegrationPairsWithTickers()
{
    Console.WriteLine();
    Console.WriteLine($" ~~~ {nameof(ShowIntegrationPairsWithTickers)} ~~~ ");

    var integrationPairs = new List<string>();
    var tickers = new List<string>();

    var validBinanceConvertTradeList = binanceConvertTradeList
        .OrderBy(q => q.CreateTime);

    foreach (var historyItem in validBinanceConvertTradeList.Select(s => new
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
ShowWithdrawHistory();
ShowIntegrationPairsWithTickers();

Console.WriteLine();
Console.WriteLine($"bz1-binance ending");
