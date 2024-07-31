using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Console_Alpaca.Models
{
    public class StockBars
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Ticker { get; set; }
        [JsonProperty("t")]
        public DateTime Timestamp { get; set; }
        [JsonProperty("o")]
        public decimal Open { get; set; }
        [JsonProperty("h")]
        public decimal High { get; set; }
        [JsonProperty("l")]
        public decimal Low { get; set; }
        [JsonProperty("c")]
        public decimal Close { get; set; }
        [JsonProperty("v")]
        public long Volume { get; set; }
    }
}
