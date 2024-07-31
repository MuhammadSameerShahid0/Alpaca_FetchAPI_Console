using Console_Alpaca.DataContext;
using Console_Alpaca.Interfaces;
using Console_Alpaca.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Console_Alpaca;

public class StockDataRepository : IStockDataRepository
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlpacaApiSettings _apiSettings;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public StockDataRepository(HttpClient httpClient, IServiceScopeFactory scopeFactory, IOptions<AlpacaApiSettings> apiSettings)
    {
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
        _apiSettings = apiSettings.Value;

        _httpClient.BaseAddress = new Uri("https://data.alpaca.markets");
        _httpClient.DefaultRequestHeaders.Add("APCA-API-KEY-ID", _apiSettings.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", _apiSettings.ApiSecretKey);
    }

    public async Task<List<StockTickerLists>> FetchMostActivities()
    {
        var symbols = new List<StockTickerLists>();
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1beta1/screener/stocks/most-actives?by=volume&top=10");

        try
        {
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonString);
                var mostActivesArray = json["most_actives"] as JArray;
                if (mostActivesArray != null)
                {
                    foreach (var item in mostActivesArray)
                    {
                        var symbol = item["symbol"].ToString();
                        var volume = Convert.ToDecimal(item["volume"].ToString());
                        var stockTicker = new StockTickerLists
                        {
                            Symbol = symbol,
                            Volume = volume
                        };
                        if (!await CheckingExistingTicker(stockTicker.Symbol))
                        {
                            symbols.Add(stockTicker);
                        }
                    }
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<StockDataContext>();
                        await context.StockTickerLists.AddRangeAsync(symbols);
                        await context.SaveChangesAsync();
                    }
                }
                else
                {
                    Console.WriteLine("Data not found in the response.");
                }
            }
            else
            {
                Console.WriteLine($"Failed to fetch symbol data. Status code: {response.StatusCode}");
            }
            ThreadTickers();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching symbols data: {ex.Message}");
        }

        return symbols;
    }

    public void ThreadTickers()
    {
        var tickers = FetchTickerListFromDatabase().Result;

        Parallel.ForEach(tickers, ticker =>
        {
            Console.WriteLine($"Fetching data for {ticker.Symbol}");
            GetStockDataForMonth(ticker.Symbol);
        });
    }

    private async Task<List<StockTickerLists>> FetchTickerListFromDatabase()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StockDataContext>();
            return await context.StockTickerLists.ToListAsync();
        }
    }

    public List<StockBars> GetStockDataForMonth(string ticker)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StockDataContext>();

            var startDate = DateTimeOffset.UtcNow.Date.AddDays(-10);
            var endDate = DateTimeOffset.UtcNow.Date;

            if (CheckExistingBars(context, ticker, startDate, endDate).Result)
            {
                Console.WriteLine($"Data for ticker '{ticker}' already exists in the database.");
                return new List<StockBars>();
            }
        }

        var CombinedResults = new ConcurrentDictionary<int, List<StockBars>>();

        Parallel.For(0, 10, i =>
        {
            var result = FetchStockDataForDay(ticker, i).Result;
            CombinedResults[i] = result.ToList();
        });

        var ResultsList = CombinedResults.Values.SelectMany(list => list).ToList();

        _semaphore.Wait();
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<StockDataContext>();
                context.StockBars.AddRange(ResultsList);
                context.SaveChanges();
                Console.WriteLine($"Data for ticker '{ticker}' has been saved to the database.");
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return ResultsList;
    }

    private async Task<List<StockBars>> FetchStockDataForDay(string ticker, int i)
    {
        var results = new List<StockBars>();
        DateTimeOffset startTimeUtc = DateTimeOffset.UtcNow.Date.AddDays(-(i + 1)).AddHours(0).AddMinutes(0);
        DateTimeOffset endTimeUtc = DateTimeOffset.UtcNow.Date.AddDays(-(i + 1)).AddHours(23).AddMinutes(59);
        string startUtcString = startTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        string endUtcString = endTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/v2/stocks/{ticker}/bars?timeframe=1Min&start={startUtcString}&end={endUtcString}&limit=10000&adjustment=raw&feed=sip&sort=asc");
        try
        {
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonString);
                var barsArray = json["bars"].ToString();
                if (!string.IsNullOrEmpty(barsArray))
                {
                    var stockBars = JsonConvert.DeserializeObject<List<StockBars>>(barsArray);
                    stockBars.AsParallel().ForAll(sb => sb.Ticker = ticker);
                    results.AddRange(stockBars);
                }
            }
            else
            {
                Console.WriteLine($"Failed to retrieve '{ticker}' data for day {i + 1}. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error for day {startTimeUtc.Date}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        return results;
    }

    private async Task<bool> CheckingExistingTicker(string symbol)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StockDataContext>();
            return await context.StockTickerLists.AnyAsync(s => s.Symbol == symbol);
        }
    }

    private async Task<bool> CheckExistingBars(StockDataContext context, string ticker, DateTimeOffset startTimeUtc, DateTimeOffset endTimeUtc)
    {
        return await context.StockBars.AnyAsync(sb =>
            sb.Ticker == ticker &&
            sb.Timestamp >= startTimeUtc &&
            sb.Timestamp <= endTimeUtc
        );
    }
}
