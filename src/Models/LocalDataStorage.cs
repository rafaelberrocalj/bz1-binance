using Binance.Net.Objects.Models.Spot;
using Binance.Net.Objects.Models.Spot.Convert;

public class LocalDataStorage
{
    public DateTime? LastRunDate { get; set; }
    public IList<FetchedData> FetchedDataList { get; set; } = new List<FetchedData>();
    public class FetchedData
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public IEnumerable<BinanceConvertTrade> BinanceConvertTradeList { get; set; } = new List<BinanceConvertTrade>();
        public IEnumerable<BinanceFiatWithdrawDeposit> BinanceFiatDepositList { get; set; } = new List<BinanceFiatWithdrawDeposit>();
        public IEnumerable<BinanceFiatWithdrawDeposit> BinanceFiatWithdrawList { get; set; } = new List<BinanceFiatWithdrawDeposit>();
    }
}
