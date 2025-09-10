using System.Formats.Asn1;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using FinanceMaker.BackTester;
using FinanceMaker.BackTester.QCAlggorithms;
using FinanceMaker.Publisher.Orders.Broker;
Console.WriteLine("Hello, World!");

// Client Portal Web API usually uses self-signed certs, so bypass validation (for dev only!)

// BackTester.Runner(typeof(RangePlusAlgorithm));
// var data = StaticContainer.ServiceProvider.GetService<IPricesPuller>();
// var candles = await data!.GetTickerPrices(new PricesPullerParameters("MNDY", new DateTime(2025, 1, 1), DateTime.Now, Period.Daily), CancellationToken.None);
// var volumes = candles.Select(_ => (_.Volume, _.Time));
// foreach (var volume in volumes)
// {
//     System.Console.WriteLine($"{volume.Volume}, {volume.Time}");
// }
// RealTimeTester.Runner(typeof(RangeAlgoritm));

// IBKR example replication from get_ib_portfolio.py
// Load DH parameters from PEM file
await InteractiveWebAPI.RunAsync();
