using Microsoft.EntityFrameworkCore;
using Console_Alpaca.Models;

namespace Console_Alpaca.DataContext
{
    public class StockDataContext : DbContext
    {
        public DbSet<StockBars> StockBars { get; set; }
        public DbSet<StockTickerLists> StockTickerLists { get; set; }
        public StockDataContext(DbContextOptions<StockDataContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<StockTickerLists>()
                .Property(a => a.Id)
                .ValueGeneratedOnAdd();
        }
    }
}
