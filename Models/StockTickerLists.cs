using System.ComponentModel.DataAnnotations;

namespace Console_Alpaca.Models
{
    public class StockTickerLists
    {
        [Key]
        public int Id { get; set; }
        public string Symbol { get; set; }
        public decimal Volume { get; set; }
    }
}
