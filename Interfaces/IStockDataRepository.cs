using Console_Alpaca.Models;

namespace Console_Alpaca.Interfaces
{
    public interface IStockDataRepository
    {
        List<StockBars> GetStockDataForMonth(string ticker);
        Task<List<StockTickerLists>> FetchMostActivities();
    }
}
