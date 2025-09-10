using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using QuantConnect;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using Quotefeeder;

namespace FinanceMaker.BackTester.QCHelpers
{
    public class YahooLiveQuoteHandler : IDataQueueHandler
    {
        private readonly ConcurrentQueue<BaseData> m_DataQueue;
        private ClientWebSocket m_WebSocket;
        private CancellationTokenSource m_Cts;
        private Task m_ListenerTask;
        private readonly HashSet<string> m_Symbols;

        public YahooLiveQuoteHandler()
        {
            m_DataQueue = new ConcurrentQueue<BaseData>();
            m_Symbols = new HashSet<string>();
            m_WebSocket = new ClientWebSocket();
            m_ListenerTask = Task.CompletedTask;
            m_Cts = new CancellationTokenSource();
        }

        public bool IsConnected => m_WebSocket != null && m_WebSocket.State == WebSocketState.Open;
        public bool IsRunning => m_ListenerTask.Status == TaskStatus.Running;
        public void Dispose()
        {
            m_Cts?.Cancel();
            m_WebSocket?.Dispose();
        }

        public void SetJob(LiveNodePacket job)
        {
            // Can be used to access job parameters if needed
        }

        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            m_Symbols.Add(dataConfig.Symbol.Value);

            if (!IsConnected)
            {
                m_ListenerTask = Task.Run(() => StartListener(m_Cts.Token, newDataAvailableHandler));
            }
            else if (IsConnected)
            {
                var subscribeMessage = new { subscribe = m_Symbols }; // JSON: {"subscribe":["AAPL", "NIO"]}
                var json = JsonSerializer.Serialize(subscribeMessage);
                var bytes = Encoding.UTF8.GetBytes(json);
                m_WebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, m_Cts.Token);
            }

            return m_DataQueue.GetEnumerator();
        }

        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            m_Symbols.Remove(dataConfig.Symbol.Value);
        }

        private async Task StartListener(CancellationToken token, EventHandler newDataAvailableHandler)
        {
            try
            {
                await m_WebSocket.ConnectAsync(new Uri("wss://streamer.finance.yahoo.com/?version=2"), token);

                var subscribeMessage = new { subscribe = m_Symbols };
                var json = JsonSerializer.Serialize(subscribeMessage);
                var bytesToSend = Encoding.UTF8.GetBytes(json);
                await m_WebSocket.SendAsync(bytesToSend, WebSocketMessageType.Text, true, token);

                var buffer = new byte[4096];

                while (!token.IsCancellationRequested && m_WebSocket.State == WebSocketState.Open)
                {
                    var result = await m_WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var message1 = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var jsonMessage = JsonSerializer.Deserialize<JsonElement>(message1);
                    var actualMessage = jsonMessage.GetProperty("message").GetString();

                    if (string.IsNullOrEmpty(actualMessage)) continue;

                    byte[] data = Convert.FromBase64String(actualMessage);

                    var msg = PricingData.Parser.ParseFrom(data);

                    var tick = new Tick
                    {
                        Symbol = Symbol.Create(msg.Id, SecurityType.Equity, Market.USA),
                        Time = DateTimeOffset.FromUnixTimeMilliseconds((long)msg.Time).UtcDateTime,
                        Value = (decimal)msg.Price,
                        BidPrice = (decimal)msg.Bid,
                        AskPrice = (decimal)msg.Ask,
                        TickType = TickType.Quote
                    };

                    m_DataQueue.Enqueue(tick);
                    newDataAvailableHandler?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YahooLiveQuoteHandler] Error: {ex.Message}");
            }
        }
    }
}
