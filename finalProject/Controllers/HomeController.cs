using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using IEXTrading.Infrastructure.IEXTradingHandler;
using IEXTrading.Models;
using IEXTrading.Models.ViewModel;
using IEXTrading.DataAccess;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;

namespace MVCTemplate.Controllers
{
    public class HomeController : Controller
    {
        public ApplicationDbContext dbContext;

        public HomeController(ApplicationDbContext context)
        {
            dbContext = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        /****
         * The Symbols action calls the GetSymbols method that returns a list of Companies.
         * This list of Companies is passed to the Symbols View.
        ****/
        public IActionResult Symbols()
        {
            //Set ViewBag variable first
            ViewBag.dbSucessComp = 0;
            IEXHandler webHandler = new IEXHandler();
            List<Company> companies = webHandler.GetSymbols();

            String companiesData = JsonConvert.SerializeObject(companies);
            //int size = System.Text.ASCIIEncoding.ASCII.GetByteCount(companiesData);
            String sessionKeyName = "getAllSymbols";
            HttpContext.Session.SetString(sessionKeyName, companiesData);

            return View(companies);
        }
        /****
         * The Chart action calls the GetChart method that returns 1 year's equities for the passed symbol.
         * A ViewModel CompaniesEquities containing the list of companies, prices, volumes, avg price and volume.
         * This ViewModel is passed to the Chart view.
        ****/
        public IActionResult Chart(string symbol)
        {
            //Set ViewBag variable first
            ViewBag.dbSuccessChart = 0;
            List<Equity> equities = new List<Equity>();
            if (symbol != null)
            {
                IEXHandler webHandler = new IEXHandler();
                equities = webHandler.GetChart(symbol);
                equities = equities.OrderBy(c => c.date).ToList(); //Make sure the data is in ascending order of date.
            }

            CompaniesEquities companiesEquities = getCompaniesEquitiesModel(equities);

            return View(companiesEquities);
        }

        /****
         * The Refresh action calls the ClearTables method to delete records from a or all tables.
         * Count of current records for each table is passed to the Refresh View.
        ****/
        public IActionResult Refresh(string tableToDel)
        {
            ClearTables(tableToDel);
            Dictionary<string, int> tableCount = new Dictionary<string, int>();
            tableCount.Add("Companies", dbContext.Companies.Count());
            tableCount.Add("Charts", dbContext.Equities.Count());
            return View(tableCount);
        }

        /****
         * Saves the Symbols in database.
        ****/
        public IActionResult PopulateSymbols()
        {
            // reading JSON from the Session
            string companiesData = HttpContext.Session.GetString("getAllSymbols");
            List<Company> companies = null;
            if (companiesData != "")
            {
                companies = JsonConvert.DeserializeObject<List<Company>>(companiesData);
            }

            foreach (Company company in companies)
            {
                //Database will give PK constraint violation error when trying to insert a record with existing PK.
                //So add company only if it doesn't exist, check its existence using symbol (PK)


                if (dbContext.Companies.Where(c => c.symbol.Equals(company.symbol)).Count() == 0)
                {
                    
                    dbContext.Companies.Add(company);
                }
            }
            dbContext.SaveChanges();
            ViewBag.dbSuccessComp = 1;
            return View("Symbols", companies);
        }

        /****
         * Saves the equities in database.
        ****/
        public IActionResult SaveCharts(string symbol)
        {
            IEXHandler webHandler = new IEXHandler();
            List<Equity> equities = webHandler.GetChart(symbol);
            //List<Equity> equities = JsonConvert.DeserializeObject<List<Equity>>(TempData["Equities"].ToString());
            foreach (Equity equity in equities)
            {
                if (dbContext.Equities.Where(c => c.date.Equals(equity.date)).Count() == 0)
                {
                    dbContext.Equities.Add(equity);
                }
            }

            dbContext.SaveChanges();
            ViewBag.dbSuccessChart = 1;

            CompaniesEquities companiesEquities = getCompaniesEquitiesModel(equities);

            return View("Chart", companiesEquities);
        }

        /****
         * Deletes the records from tables.
        ****/
        public void ClearTables(string tableToDel)
        {
            if ("all".Equals(tableToDel))
            {
                //First remove equities and then the companies
                dbContext.Equities.RemoveRange(dbContext.Equities);
                dbContext.Companies.RemoveRange(dbContext.Companies);
            }
            else if ("Companies".Equals(tableToDel))
            {
                //Remove only those that don't have Equity stored in the Equitites table
                dbContext.Companies.RemoveRange(dbContext.Companies
                                                         .Where(c => c.Equities.Count == 0)
                                                                      );
            }
            else if ("Charts".Equals(tableToDel))
            {
                dbContext.Equities.RemoveRange(dbContext.Equities);
            }
            dbContext.SaveChanges();
        }

        /****
         * Returns the ViewModel CompaniesEquities based on the data provided.
         ****/
        public CompaniesEquities getCompaniesEquitiesModel(List<Equity> equities)
        {
            List<Company> companies = dbContext.Companies.ToList();

            if (equities.Count == 0)
            {
                return new CompaniesEquities(companies, null, "", "", "", 0, 0);
            }

            Equity current = equities.Last();
            string dates = string.Join(",", equities.Select(e => e.date));
            string prices = string.Join(",", equities.Select(e => e.high));
            string volumes = string.Join(",", equities.Select(e => e.volume / 1000000)); //Divide vol by million
            float avgprice = equities.Average(e => e.high);
            double avgvol = equities.Average(e => e.volume) / 1000000; //Divide volume by million
            return new CompaniesEquities(companies, equities.Last(), dates, prices, volumes, avgprice, avgvol);
        }
    
        public IActionResult recommendType()
        {
            return View();
        }
        /****
         * @author qing qi
         * Recommend stock by 52-Week Price Range Recommendation Strategy, which means you better to choose
         * the stock that the price change range is larger than 0.82(82%)
        ****/
        public IActionResult recommend(String stockType)
        {
            List<Company> companiesList = dbContext.Companies.ToList();
            
            List<Equity> equities = new List<Equity>();
            List<Double> pricreRangeRateList = new List<double>();//all stock price increase rate
            List<String> symbolList = new List<String>();//all stock symbol
            List<Company> stockChose = new List<Company>();//stock you better to choose
            List<double> stockChosePrice = new List<double>();// price increase rate of stock you better to choose
            string symbolString = "";
            string priceRangeRate = "";
            if (companiesList != null)
            {
                int n = companiesList.Count;
                if (companiesList.Count > 50) n = 50;
                companiesList = companiesList.GetRange(0, n);
                foreach (Company company in companiesList)
                {
                    if (company.type.Equals(stockType))//filter the companies, just get company with the type that user chose.
                    {
                        IEXHandler webHandler = new IEXHandler();
                        String symbol = company.symbol;
                        symbolList.Add(symbol);
                        equities = webHandler.GetChart(company.symbol);
                        if (equities != null && equities.Count != 0)
                        {
                            equities = equities.OrderBy(c => c.date).ToList(); //Make sure the data is in ascending order of date.
                            float maxprice = equities.Max(e => e.high);
                            float minprice = equities.Min(e => e.low);
                            Equity current = equities.Last();
                            float currentPrice = current.high;
                            double pricre_Range_Rate = 0.0;
                            if (maxprice != minprice)//in case messy data makes the denominator zero below
                            {
                                pricre_Range_Rate = Math.Round((currentPrice - minprice) / (maxprice - minprice), 2);
                            }
                            pricreRangeRateList.Add(pricre_Range_Rate);// for joinning to string below
                            if (pricre_Range_Rate >= 0.82)// the stocks you better to choose
                            {
                                stockChose.Add(company);
                                stockChosePrice.Add(pricre_Range_Rate);
                            }
                        }
                    }
                   
                }
                if(pricreRangeRateList!=null&& symbolList != null) {
                    priceRangeRate = string.Join(",", pricreRangeRateList);//list to string for js use in the page
                    symbolString = string.Join(",", symbolList);
                }
            }
            RecommendStock RecommendStock = new RecommendStock(stockChose, stockChosePrice, symbolString, priceRangeRate);
            return View("recommend", RecommendStock);
        }
        
        public IActionResult aboutUS()
        {

            return View();
        }

    }
}
