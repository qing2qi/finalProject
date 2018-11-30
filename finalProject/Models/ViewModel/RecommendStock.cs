using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IEXTrading.Models.ViewModel
{
    public class RecommendStock
    {

        public List<Company> Companies { get; set; }
        public List<double> StockChosePrice { get; set; }
        public string Symbol { get; set; }
        public string PriceRangeRate { get; set; }

        public RecommendStock(List<Company> companies, List<double> stockChosePrice, String symbol,string priceRangeRate)
        {
            Companies = companies;
            StockChosePrice = stockChosePrice;
            Symbol = symbol;
            PriceRangeRate = priceRangeRate;
        }
    }
}
