using System.Formats.Asn1;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Accord.Math;
using FinanceMaker.Algorithms.Runners;
using FinanceMaker.BackTester;
using FinanceMaker.BackTester.QCAlggorithms;
using FinanceMaker.BackTester.QCHelpers;
using FinanceMaker.Common;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Publisher.Orders.Broker;
using FinanceMaker.Pullers.TickerPullers;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using QuantConnect.Api;
// static async Task<string> GetGrokResponseAsync(string prompt)
// {
//     // Configure ChromeOptions for headless mode
//     var chromeOptions = new ChromeOptions();
//     chromeOptions.AddArguments("--headless"); // Run in headless mode
//     chromeOptions.AddArguments("--no-sandbox");
//     chromeOptions.AddArguments("--disable-dev-shm-usage");
//     chromeOptions.AddArguments("--disable-gpu");
//     chromeOptions.AddArguments("--window-size=1920,1080");

//     // Initialize WebDriver
//     using var driver = new ChromeDriver(chromeOptions);
//     try
//     {
//         // Navigate to Grok's web interface (adjust URL as needed)
//         driver.Navigate().GoToUrl("https://grok.com");

//         // Wait for the page to load (adjust selector and wait time as needed)
//         await Task.Delay(5000); // Simple delay; consider WebDriverWait for robustness

//         // Find the input field for the prompt (update selector based on actual site)
//         var inputField = driver.FindElement(By.XPath("//textarea[1]")); // Adjust selector
//         foreach (var chara in prompt)
//         {
//             inputField.SendKeys(chara.ToString());
//             await Task.Delay(new Random().Next(0, 100));
//         }
//         // inputField.SendKeys(prompt);
//         inputField.SendKeys(Keys.Enter);

//         // Wait for Grok's response (adjust selector and wait time as needed)
//         await Task.Delay(5000); // Wait for response to load

//         // Find the response element (update selector based on actual site)
//         var responseElement = driver.FindElement(By.CssSelector("div[class*='response-container']")); // Adjust selector
//         string responseText = responseElement.Text;

//         return string.IsNullOrEmpty(responseText) ? "No response found" : responseText;
//     }
//     catch (NoSuchElementException ex)
//     {
//         return $"Error: Element not found. Check selectors. {ex.Message}";
//     }
//     catch (Exception ex)
//     {
//         return $"Error: {ex.Message}";
//     }
//     finally
//     {
//         driver.Quit();
//     }
// }

// Console.WriteLine("Hello, World!");

// Client Portal Web API usually uses self-signed certs, so bypass validation (for dev only!)

var password = Environment.GetEnvironmentVariable("EMAIL_PASSWORD");
var allTickers = new HashSet<string>();
var scanner = StaticContainer.ServiceProvider.GetService<StockExplode>();
var fibonachiRunner = StaticContainer.ServiceProvider.GetService<FibonachiRunner>();
while (true)
{
    var ticker = await scanner!.ScanTickers(new FinanceMaker.Common.Models.Pullers.TickersPullerParameters(), CancellationToken.None);
    var p = allTickers.Count;

    var tickerToPrice = new Dictionary<string, decimal>();
    foreach (var t in ticker)
    {
        allTickers.Add(t);
        var fibResult = await fibonachiRunner!.Run(new RangeAlgorithmInput(t, DateTime.Now.Date.Subtract(TimeSpan.FromDays(3 * 365)), DateTime.Now, Period.Weekly, Algorithm.Fibonachi), CancellationToken.None);
        var fibLevels = fibResult.KeyLevels;

        // don't forget I've already have the prices in fibResult.Candles
        var latestPrice = fibResult.Last().Close;
        var closestLevel = fibLevels.OrderBy(level => Math.Abs(level - latestPrice)).First();
        Console.WriteLine($"Ticker: {t}, Latest Price: {latestPrice}, Closest Fibonacci Level: {closestLevel}");

        tickerToPrice[t] = (decimal)closestLevel;
    }

    if (p < allTickers.Count)
    {
        try
        {
            var tickerList = string.Join("\n", tickerToPrice.Select(kvp =>
                $"[\n    \"{kvp.Key}\": {{\n" +
                $"        \"Entry\": {kvp.Value:F2},\n" +
                $"        \"Stop Loss\": {kvp.Value * 0.95m:F2},\n" +
                $"        \"Take Profit\": {kvp.Value * 1.15m:F2}\n" +
                $"    }}\n]"
            ));
            var fromAddress = new MailAddress("betterlokaki@gmail.com", "FinanceMaker Bot");
            var toAddress = new MailAddress("shahartheking22@gmail.com", "Shahar Rozolio");
            var toAdress2 = new MailAddress("evyatar.kima@mail.huji.ac.il", "Evyatar Kima");
            var toAdress3 = new MailAddress("meir.rozolio@gmail.com", "Meir Rozolio");
            const string subject = "Ticker List";
            string body = $"Here are the tickers found:\n\n{tickerList}";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, password)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body
            })
            {
                smtp.Send(message);
            }
            using (var message = new MailMessage(fromAddress, toAdress2)
            {
                Subject = subject,
                Body = body
            })
            {
                smtp.Send(message);
            }
            using (var message = new MailMessage(fromAddress, toAdress3)
            {
                Subject = subject,
                Body = body
            })
            {
                smtp.Send(message);
            }

            Console.WriteLine("Ticker list sent to your email successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email: {ex.Message}");
        }
    }
    await Task.Delay(TimeSpan.FromMinutes(30));
}

// var candles = await data!.GetTickerPrices(new PricesPullerParameters("MNDY", new DateTime(2025, 1, 1), DateTime.Now, Period.Daily), CancellationToken.None);
// var volumes = candles.Select(_ => (_.Volume, _.Time));
// foreach (var volume in volumes)
// {
//     System.Console.WriteLine($"{volume.Volume}, {volume.Time}");
// }
// RealTimeTester.Runner(typeof(Four));
// await BackTester.Runner(typeof(FourHoursBreakoutAlgorithm));
// IBKR example replication from get_ib_portfolio.py
// Load DH parameters from PEM file
return;
// await InteractiveWebAPI.RunAsync();
// var xpath = "//tr[@class=\"styled-row is-hoverable is-bordered is-rounded is-border-top is-hover-borders has-color-text news_table-row\"]";

// // var htmlWeb = new H();
// using var aclient = new HttpClient();

// // 1) Send as plain text
// aclient.AddBrowserUserAgent();
// var htmlNod = await aclient.GetAsync("https://finviz.com/news.ashx?v=3");
// var c = await htmlNod.Content.ReadAsStringAsync();

// var document = new HtmlDocument();
// document.LoadHtml(c);
// var p = document.DocumentNode;
// var news = p.SelectNodes(xpath)
//                     .Select(node => (node.SelectInnerSingleNode($"//a[@class=\"nn-tab-link\"]").Attributes["href"].Value,
//                     node.SelectInnerNodes($"//span[@class=\"select-none font-semibold\"]").Select(p => p.InnerText).ToArray()))
//                     .ToArray();
// var result = news
//     .SelectMany(item => item.Item2.Select(key => new { key, link = item.Item1 }))
//     .GroupBy(x => x.key)
//     .ToDictionary(g => g.Key, g => g.Select(x => x.link).ToList());
// long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
// var initUrl = $"https://ssff.grok.com/v1/initialize?k=client-IaZnesJsihYxWIlzjCFWo0vUALsjfIVa9N289FA0tce&st=javascript-client-react&sv=3.8.1&t={timestamp}&sid=a46369f5-74ea-464f-95fa-16d303fecabc&se=1";
// string payload = "=0XfsxWdupjIsJXVrNWYixGbhZmIsIyYiF2YlZ2MwMDZ2ETLhZWN50iZ0YDNtEWZ0cTL1YWO2MjN0EmI6ICRJ52bpN3clNnIsICM3QWYiVDM4gTNjdTLiJmY50SZwQDNtIGNiFWLyImZiRTM3EmI6ICRJVGbiFGdzJCLiQ3YhVmctQnbllGbj1CdwlmcjNXY2FmaiojIlBXeUtGZzJCLiEjL44yMiojIu9WazJXZWtGZzJye6ISY0FGZhRXZNdWazRXY0NnIsU2csFmZ6ICZlR3clVXclJVZz52bwNXZSNXY0xWZkJCLiIjYqRmI6ICazFGaiwSf9JibvlGdjVHZvJHciojIyVWa0Jye6ICduVWbu9mcpZnbFdWazRXY0NnIs0nIuVmI6ISZsF2Yvxkb4ETaisnOi02b0NXdjJCL9JCM3QWYiVDM4gTNjdTLiJmY50SZwQDNtIGNiFWLyImZiRTM3EmI6ICRJVGbiFGdzJye6IycElUbvR3c1NmIsIiMwI2YhRmNkJ2N0ITLzEjZi1SZiBDNtYmZiBTL0YGO0IGM1QjI6ICRJJXZzVnI7pjIyV2c1Jye";

// using var client = new HttpClient();

// // 1) Send as plain text
// client.AddBrowserUserAgent();
// var textResponse = await client.PostAsync(
//    initUrl,
//     new StringContent(payload, Encoding.UTF8, "text/plain")

// );
// var textResult = await textResponse.Content.ReadAsStringAsync();
// System.Console.WriteLine(textResult);



// =0XfsxWdupjIsJXVrNWYixGbhZmIsIyYiF2YlZ2MwMDZ2ETLhZWN50iZ0YDNtEWZ0cTL1YWO2MjN0EmI6ICRJ52bpN3clNnIsICM3QWYiVDM4gTNjdTLiJmY50SZwQDNtIGNiFWLyImZiRTM3EmI6ICRJVGbiFGdzJCLiQ3YhVmctQnbllGbj1CdwlmcjNXY2FmaiojIlBXeUtGZzJCLiEjL44yMiojIu9WazJXZWtGZzJye6ISY0FGZhRXZNdWazRXY0NnIsU2csFmZ6ICZlR3clVXclJVZz52bwNXZSNXY0xWZkJCLiIjYqRmI6ICazFGaiwSf9JibvlGdjVHZvJHciojIyVWa0Jye6ICduVWbu9mcpZnbFdWazRXY0NnIs0nIuVmI6ISZsF2Yvxkb4ETaisnOi02b0NXdjJCL9JCM3QWYiVDM4gTNjdTLiJmY50SZwQDNtIGNiFWLyImZiRTM3EmI6ICRJVGbiFGdzJye6IycElUbvR3c1NmIsIiMwI2YhRmNkJ2N0ITLzEjZi1SZiBDNtYmZiBTL0YGO0IGM1QjI6ICRJJXZzVnI7pjIyV2c1Jy
// =0XfsxWdupjIsJXVrNWYixGbhZmIsIyYiF2YlZ2MwMDZ2ETLhZWN50iZ0YDNtEWZ0cTL1YWO2MjN0EmI6ICRJ52bpN3clNnIsICM3QWYiVDM4gTNjdTLiJmY50SZwQDNtIGNiFWLyImZiRTM3EmI6ICRJVGbiFGdzJCLiQ3YhVmctQnbllGbj1CdwlmcjNXY2FmaiojIlBXeUtGZzJCLiEjL44yMiojIu9WazJXZWtGZzJye6ISY0FGZhRXZNdWazRXY0NnIsU2csFmZ6ICZlR3clVXclJVZz52bwNXZSNXY0xWZkJCLiIjYqRmI6ICazFGaiwSf9JibvlGdjVHZvJHciojIyVWa0Jye6ICduVWbu9mcpZnbFdWazRXY0NnIs0nIuVmI6ISZsF2Yvxkb4ETaisnOi02b0NXdjJCL9JCM3QWYiVDM4gTNjdTLiJmY50SZwQDNtIGNiFWLyImZiRTM3EmI6ICRJVGbiFGdzJye6IycElUbvR3c1NmIsIiMwI2YhRmNkJ2N0ITLzEjZi1SZiBDNtYmZiBTL0YGO0IGM1QjI6ICRJJXZzVnI7pjIyV2c1Jy
// =0XfsxWdupjIsJXVrNWYixGbhZmIsIyYiF2YlZ2MwMDZ2ETLhZWN50iZ0YDNtEWZ0cTL1YWO2MjN0EmI6ICRJ52bpN3clNnIsICM3QWYiVDM4gTNjdTLiJmY50SZwQDNtIGNiFWLyImZiRTM3EmI6ICRJVGbiFGdzJCLiQ3YhVmctQnbllGbj1CdwlmcjNXY2FmaiojIlBXeUtGZzJCLiEjL44yMiojIu9WazJXZWtGZzJye6ISY0FGZhRXZNdWazRXY0NnIsU2csFmZ6ICZlR3clVXclJVZz52bwNXZSNXY0xWZkJCLiIjYqRmI6ICazFGaiwSf9JibvlGdjVHZvJHciojIyVWa0Jye6ICduVWbu9mcpZnbFdWazRXY0NnIs0nIuVmI6ISZsF2Yvxkb4ETaisnOi02b0NXdjJCL9JCM3QWYiVDM4gTNjdTLiJmY50SZwQDNtIGNiFWLyImZiRTM3EmI6ICRJVGbiFGdzJye6IycElUbvR3c1NmIsIiMwI2YhRmNkJ2N0ITLzEjZi1SZiBDNtYmZiBTL0YGO0IGM1QjI6ICRJJXZzVnI7pjIyV2c1Jye