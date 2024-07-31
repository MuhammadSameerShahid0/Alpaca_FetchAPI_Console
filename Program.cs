using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Console_Alpaca;
using Console_Alpaca.DataContext;
using Console_Alpaca.Interfaces;
using System.Threading.Tasks;

namespace Alpaca_Console
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            var services = new ServiceCollection();

            services.Configure<AlpacaApiSettings>(config.GetSection("AlpacaApi"));
            services.AddHttpClient();
            services.AddDbContext<StockDataContext>(options =>
                options.UseSqlServer(config.GetConnectionString("DefaultConnection")), ServiceLifetime.Transient);
            services.AddScoped<IStockDataRepository, StockDataRepository>();

            var serviceProvider = services.BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IStockDataRepository>();

                try
                {
                    var symbols = await repository.FetchMostActivities();
                    var options = new ParallelOptions { MaxDegreeOfParallelism = 8 };

                    Parallel.ForEach(symbols, options, symbol =>
                    {
                        Console.WriteLine($"Fetching data for {symbol.Symbol}");
                        repository.GetStockDataForMonth(symbol.Symbol);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine("Total time consumed for this => " + elapsedTime);
        }
    }
}
