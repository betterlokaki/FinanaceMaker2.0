using System;
using System.Collections.Generic;
using System.Net.Http;
using FinanceMaker.Algorithms;
using FinanceMaker.Algorithms.News.Analyziers;
using FinanceMaker.Algorithms.News.Analyziers.Interfaces;
using FinanceMaker.Brokers.InteractiveBrokers;
using FinanceMaker.Common;
using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Ideas.Ideas;
using FinanceMaker.Ideas.Ideas.Abstracts;
using FinanceMaker.Publisher.Orders.Broker;
using FinanceMaker.Publisher.Orders.Trader.Interfaces;
using FinanceMaker.Publisher.Traders;
using FinanceMaker.Publisher.Traders.Interfaces;
using FinanceMaker.Pullers;
using FinanceMaker.Pullers.NewsPullers;
using FinanceMaker.Pullers.NewsPullers.Interfaces;
using FinanceMaker.Pullers.PricesPullers;
using FinanceMaker.Pullers.PricesPullers.Interfaces;
using FinanceMaker.Pullers.TickerPullers;
using FinanceMaker.Pullers.TickerPullers.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var app = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(provider =>
                {
                    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();
                    return configuration;
                });
                services.AddHttpClient().ConfigureHttpClientDefaults((a) =>
                {
                    a.ConfigurePrimaryHttpMessageHandler(() =>
                    {
                        var handler = new HttpClientHandler
                        {
                            CookieContainer = new System.Net.CookieContainer(),
                            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        };
                        return handler;
                    });

                });
                // Can't use the extension (the service collection becomes read only after the build)
                services.AddSingleton<MarketStatus>();
                services.AddSingleton<FinvizTickersPuller>();
                services.AddSingleton<TradingViewTickersPuller>();
                services.AddSingleton<MostVolatiltyTickers>();
                services.AddSingleton(sp => new IParamtizedTickersPuller[]
                {
                    sp.GetService<MostVolatiltyTickers>()
                });
                services.AddSingleton(sp => Array.Empty<IRelatedTickersPuller>());
                services.AddSingleton(sp => Array.Empty<ITickerPuller>());
                services.AddSingleton<MainTickersPuller>();

                services.AddSingleton<YahooPricesPuller>();
                services.AddSingleton<YahooInterdayPricesPuller>();

                services.AddSingleton(sp => new IPricesPuller[]
                {
                    // sp.GetService<YahooPricesPuller>(),
                    sp.GetService<YahooInterdayPricesPuller>(),

                });
                services.AddSingleton<IPricesPuller, MainPricesPuller>();
                services.AddSingleton<GoogleNewsPuller>();
                services.AddSingleton<YahooFinanceNewsPuller>();
                services.AddSingleton(sp => new INewsPuller[]
                {
                    sp.GetService<GoogleNewsPuller>(),
                    sp.GetService<YahooFinanceNewsPuller>(),
                    sp.GetService<FinvizNewPuller>(),

                });

                services.AddSingleton<KeyLevelsRunner>();
                services.AddSingleton<EMARunner>();
                services.AddSingleton<BreakOutDetectionRunner>();

                services.AddSingleton<IEnumerable<IAlgorithmRunner<RangeAlgorithmInput>>>(
                    sp =>
                    {
                        var runner3 = sp.GetService<KeyLevelsRunner>();
                        var runner1 = sp.GetService<EMARunner>();
                        var runner2 = sp.GetService<BreakOutDetectionRunner>();


                        return [runner1, runner2, runner3];
                    }
                );

                services.AddSingleton<RangeAlgorithmsRunner>();
                services.AddSingleton<INewsPuller, MainNewsPuller>();
                services.AddSingleton<KeywordsDetectorAnalysed>();
                services.AddSingleton<INewsAnalyzer[]>(sp => [
                    sp.GetService<KeywordsDetectorAnalysed>()
                ]);
                services.AddSingleton<INewsAnalyzer, NewsAnalyzer>();
                services.AddSingleton<IdeaBase<TechnicalIdeaInput, EntryExitOutputIdea>, OverNightBreakout>();
                services.AddSingleton<OverNightBreakout>();
                services.AddSingleton<IBKRClient>();
                var consumerKey = Environment.GetEnvironmentVariable("IBKR_CONSUMER_KEY") ?? "SHROZOSHR";
                var tokenAccess = Environment.GetEnvironmentVariable("IBKR_TOKEN") ?? "d583aac6335668f1f6b9";
                var tokenSecret = Environment.GetEnvironmentVariable("IBKR_TOKEN_SECRET") ?? "ni3WiVII+DhvPAav6OHnVGU87x/wAcc6qkp/l5gyVmfDfPnp7umsq8DWxjQ79UHE0TxdlgfeKzF1lHxCFIcRltCtPeQCl9uaHEG3cxkFMiziKmqrUNDnGAtPgvSwAeNJ0xDLCujvPh/HnK37av+7bRRI+3hDvty4C/XX4M/J3dWd8tH7ei/G6x6kiDfmWxGBERHw0DfrhqncUJ2ujWTkRy8UjGLgnH4oJrcPfnyBgMSY/OsFvMN2WbxRD8+TBPTu6DeMsZJc83m9nJC3HiAMzesfXae65iJgM0m366jSavQnrKPWOjp9EeuJZceui2sUsxUR3pVJJHQzK6LcG0hfKA==";

                var dhParamPath = Environment.GetEnvironmentVariable("IBKR_DH_PARAM") ?? "/Users/shaharrozolio/dhparam.pem";
                var dhPrivateEncryption = Environment.GetEnvironmentVariable("IBKR_DH_PRIVATE_ENCRYPTION") ?? "/Users/shaharrozolio/private_encryption.pem";
                var dhPrivateSignature = Environment.GetEnvironmentVariable("IBKR_DH_PRIVATE_SIGNATURE") ?? "/Users/shaharrozolio/private_signature.pem";

                var config = new IBKRConfig
                {
                    ConsumerKey = consumerKey,
                    TokenAccess = tokenAccess,
                    TokenSecret = tokenSecret,
                    DiffieHellmanParamPath = dhParamPath,
                    DiffieHellmanPrivateEncryptionPath = dhPrivateEncryption,
                    DiffieHellmanPrivateSignaturePath = dhPrivateSignature,
                };
                services.AddSingleton<IBKRConfig>(config);
                services.AddSingleton<IBroker, InteracrtiveBroker>();
                services.AddSingleton<ITrader, QCTrader>();
                services.AddSingleton<Worker>();


            })
            .Build();

var aa = app.Services.GetRequiredService<Worker>();
await aa.ExecuteAsync(new System.Threading.CancellationToken());
