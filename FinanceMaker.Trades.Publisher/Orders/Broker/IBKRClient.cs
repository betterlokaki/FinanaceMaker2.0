using System.Collections.Generic;
using System.Threading;
using FinanceMaker.Common.Models.Interactive;
using IBApi;
using NetMQ.Sockets;

namespace FinanceMaker.Publisher.Orders.Broker;

public class IBKRClient : EWrapper
{
    private readonly EClientSocket _clientSocket;
    private readonly EReaderSignal _signal;
    private readonly List<IBKRPosition> _positions = new List<IBKRPosition>();
    private decimal _buyingPower;
    private int _nextOrderId;
    private long _connectionTimeoutSeconds = 10;
    private readonly List<IBKROrderResponse> _openOrders = new List<IBKROrderResponse>();

    public IBKRClient()
    {
        _signal = new EReaderMonitorSignal();
        _clientSocket = new EClientSocket(this, _signal);
    }

    public void Connect(string host, int port, int clientId)
    {
        if (_clientSocket.IsConnected()) return;
        _clientSocket.eConnect(host, port, clientId);
        var reader = new EReader(_clientSocket, _signal);
        reader.Start();
        new Thread(() =>
        {
            while (_clientSocket.IsConnected())
            {
                _signal.waitForSignal();
                var processTask = Task.Run(() => reader.processMsgs());

                if (!processTask.Wait(TimeSpan.FromSeconds(_connectionTimeoutSeconds)))
                {
                    Console.WriteLine($"Timeout in processMsgs after {_connectionTimeoutSeconds}s, disconnecting...");
                    _clientSocket.eDisconnect();
                    break;
                }
            }
        })
        { IsBackground = true }.Start();
        Console.WriteLine("Connected to IB Gateway");
    }
    public void RequestAccountSummary()
    {
        _clientSocket.reqAccountSummary(1, "All", "AccountType,BuyingPower, AvailableFunds, NetLiquidation, TotalCashValue");
        Console.WriteLine("Requested account summary");
    }

    public void PlaceBracketOrder(int orderId, Contract contract, Order entryOrder, Order takeProfitOrder, Order stopLossOrder)
    {
        // Set parent order
        entryOrder.OrderId = orderId;
        entryOrder.Transmit = false;
        entryOrder.OutsideRth = true;

        // Set take profit order
        takeProfitOrder.OrderId = orderId + 1;
        takeProfitOrder.ParentId = orderId;
        takeProfitOrder.Transmit = false;
        takeProfitOrder.OutsideRth = true;

        // Set stop loss order
        stopLossOrder.OrderId = orderId + 2;
        stopLossOrder.ParentId = orderId;
        stopLossOrder.Transmit = true;
        stopLossOrder.OutsideRth = true;
        // Place orders
        _clientSocket.placeOrder(entryOrder.OrderId, contract, entryOrder);
        Console.WriteLine($"Entry order {entryOrder.OrderId} placed for {contract.Symbol}");

        _clientSocket.placeOrder(takeProfitOrder.OrderId, contract, takeProfitOrder);
        Console.WriteLine($"Take profit order {takeProfitOrder.OrderId} placed for {contract.Symbol}");

        _clientSocket.placeOrder(stopLossOrder.OrderId, contract, stopLossOrder);
        Console.WriteLine($"Stop loss order {stopLossOrder.OrderId} placed for {contract.Symbol}");
    }

    public void RequestCurrentPositions()
    {
        _clientSocket.reqPositions();
        Console.WriteLine("Requested current positions");
    }
    public void RequestOpenOrders()
    {
        _clientSocket.reqOpenOrders();
        Console.WriteLine("Requested open orders");
    }
    public void Disconnect()
    {
        _clientSocket.eDisconnect();
        Console.WriteLine("Disconnected from IB Gateway");
    }

    // Implement other EWrapper methods as needed
    public void error(Exception e) { Console.WriteLine($"Error: {e.Message}"); }
    public void error(string str) { Console.WriteLine($"Error: {str}"); }
    public void error(int id, int errorCode, string errorMsg) { Console.WriteLine($"Error: {id}, {errorCode}, {errorMsg}"); }
    public void nextValidId(int orderId)
    {
        Console.WriteLine($"Next Valid Id: {orderId}");
        _nextOrderId = orderId;
    }
    public void position(string account, Contract contract, double pos, double avgCost)
    {
        Console.WriteLine($"Position: {account}, {contract.Symbol}, {pos}, {avgCost}");
        _positions.Add(new IBKRPosition { Symbol = contract.Symbol, Position = (decimal)pos, AvgPrice = (decimal)avgCost });
    }
    public void positionEnd() { Console.WriteLine("Position End"); }
    public void currentTime(long time) { Console.WriteLine($"Current Time: {time}"); }
    public void tickPrice(int tickerId, int field, double price, TickAttrib attribs) { Console.WriteLine($"Tick Price: {tickerId}, {field}, {price}"); }
    public void tickSize(int tickerId, int field, int size) { Console.WriteLine($"Tick Size: {tickerId}, {field}, {size}"); }
    public void tickString(int tickerId, int field, string value) { Console.WriteLine($"Tick String: {tickerId}, {field}, {value}"); }
    public void tickGeneric(int tickerId, int field, double value) { Console.WriteLine($"Tick Generic: {tickerId}, {field}, {value}"); }
    public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureExpiry, double dividendImpact, double dividendsToExpiry) { Console.WriteLine($"Tick EFP: {tickerId}, {tickType}, {basisPoints}"); }
    public void deltaNeutralValidation(int reqId, UnderComp underComp) { Console.WriteLine($"Delta Neutral Validation: {reqId}"); }
    public void tickOptionComputation(int tickerId, int field, double impliedVol, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice) { Console.WriteLine($"Tick Option Computation: {tickerId}, {field}"); }
    public void tickSnapshotEnd(int tickerId) { Console.WriteLine($"Tick Snapshot End: {tickerId}"); }
    public void managedAccounts(string accountsList) { Console.WriteLine($"Managed Accounts: {accountsList}"); }
    public void connectionClosed() { Console.WriteLine("Connection Closed"); }
    public void accountSummary(int reqId, string account, string tag, string value, string currency)
    {
        Console.WriteLine($"Account Summary: {reqId}, {account}, {tag}, {value}, {currency}");
        if (tag == "BuyingPower")
        {
            _buyingPower = decimal.Parse(value);
        }
    }
    public void accountSummaryEnd(int reqId) { Console.WriteLine($"Account Summary End: {reqId}"); }
    public void bondContractDetails(int reqId, ContractDetails contract) { Console.WriteLine($"Bond Contract Details: {reqId}, {contract.Summary.Symbol}"); }
    public void updateAccountValue(string key, string value, string currency, string accountName) { Console.WriteLine($"Update Account Value: {key}, {value}, {currency}, {accountName}"); }
    public void updatePortfolio(Contract contract, double position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName) { Console.WriteLine($"Update Portfolio: {contract.Symbol}, {position}"); }
    public void updateAccountTime(string timeStamp) { Console.WriteLine($"Update Account Time: {timeStamp}"); }
    public void accountDownloadEnd(string account) { Console.WriteLine($"Account Download End: {account}"); }
    public void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice) { Console.WriteLine($"Order Status: {orderId}, {status}"); }
    public void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
    {
        Console.WriteLine($"Open Order: {orderId}, {contract.Symbol}");
        _openOrders.Add(new IBKROrderResponse { OrderId = orderId.ToString(), Symbol = contract.Symbol, Status = orderState.Status, });
    }
    public void openOrderEnd() { Console.WriteLine("Open Order End"); }
    public void contractDetails(int reqId, ContractDetails contractDetails) { Console.WriteLine($"Contract Details: {reqId}, {contractDetails.Summary.Symbol}"); }
    public void contractDetailsEnd(int reqId) { Console.WriteLine($"Contract Details End: {reqId}"); }
    public void execDetails(int reqId, Contract contract, Execution execution) { Console.WriteLine($"Exec Details: {reqId}, {contract.Symbol}"); }
    public void execDetailsEnd(int reqId) { Console.WriteLine($"Exec Details End: {reqId}"); }
    public void commissionReport(CommissionReport commissionReport) { Console.WriteLine($"Commission Report: {commissionReport.ExecId}"); }
    public void fundamentalData(int reqId, string data) { Console.WriteLine($"Fundamental Data: {reqId}"); }
    public void historicalData(int reqId, Bar bar) { Console.WriteLine($"Historical Data: {reqId}, {bar.Time}"); }
    public void historicalDataUpdate(int reqId, Bar bar) { Console.WriteLine($"Historical Data Update: {reqId}, {bar.Time}"); }
    public void historicalDataEnd(int reqId, string startDateStr, string endDateStr) { Console.WriteLine($"Historical Data End: {reqId}"); }
    public void marketDataType(int reqId, int marketDataType) { Console.WriteLine($"Market Data Type: {reqId}, {marketDataType}"); }
    public void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size) { Console.WriteLine($"Update Mkt Depth: {tickerId}"); }
    public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size) { Console.WriteLine($"Update Mkt Depth L2: {tickerId}"); }
    public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) { Console.WriteLine($"Update News Bulletin: {msgId}"); }
    public void realtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double wap, int count) { Console.WriteLine($"Realtime Bar: {reqId}"); }
    public void scannerParameters(string xml) { Console.WriteLine("Scanner Parameters"); }
    public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr) { Console.WriteLine($"Scanner Data: {reqId}"); }
    public void scannerDataEnd(int reqId) { Console.WriteLine($"Scanner Data End: {reqId}"); }
    public void receiveFA(int faDataType, string faXmlData) { Console.WriteLine($"Receive FA: {faDataType}"); }
    public void verifyMessageAPI(string apiData) { Console.WriteLine($"Verify Message API: {apiData}"); }
    public void verifyCompleted(bool isSuccessful, string errorText) { Console.WriteLine($"Verify Completed: {isSuccessful}"); }
    public void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) { Console.WriteLine($"Verify And Auth Message API: {apiData}"); }
    public void verifyAndAuthCompleted(bool isSuccessful, string errorText) { Console.WriteLine($"Verify And Auth Completed: {isSuccessful}"); }
    public void displayGroupList(int reqId, string groups) { Console.WriteLine($"Display Group List: {reqId}"); }
    public void displayGroupUpdated(int reqId, string contractInfo) { Console.WriteLine($"Display Group Updated: {reqId}"); }
    public void connectAck() { Console.WriteLine("Connect Ack"); }
    public void positionMulti(int reqId, string account, string modelCode, Contract contract, double pos, double avgCost) { Console.WriteLine($"Position Multi: {reqId}"); }
    public void positionMultiEnd(int reqId) { Console.WriteLine($"Position Multi End: {reqId}"); }
    public void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency) { Console.WriteLine($"Account Update Multi: {reqId}"); }
    public void accountUpdateMultiEnd(int reqId) { Console.WriteLine($"Account Update Multi End: {reqId}"); }
    public void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes) { Console.WriteLine($"Security Definition Option Parameter: {reqId}"); }
    public void securityDefinitionOptionParameterEnd(int reqId) { Console.WriteLine($"Security Definition Option Parameter End: {reqId}"); }
    public void softDollarTiers(int reqId, SoftDollarTier[] tiers) { Console.WriteLine($"Soft Dollar Tiers: {reqId}"); }
    public void familyCodes(FamilyCode[] familyCodes) { Console.WriteLine("Family Codes"); }
    public void symbolSamples(int reqId, ContractDescription[] contractDescriptions) { Console.WriteLine($"Symbol Samples: {reqId}"); }
    public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) { Console.WriteLine("Mkt Depth Exchanges"); }
    public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { Console.WriteLine($"Tick News: {tickerId}"); }
    public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) { Console.WriteLine($"Smart Components: {reqId}"); }
    public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) { Console.WriteLine($"Tick Req Params: {tickerId}"); }
    public void newsProviders(NewsProvider[] newsProviders) { Console.WriteLine("News Providers"); }
    public void newsArticle(int requestId, int articleType, string articleText) { Console.WriteLine($"News Article: {requestId}"); }
    public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) { Console.WriteLine($"Historical News: {requestId}"); }
    public void historicalNewsEnd(int requestId, bool hasMore) { Console.WriteLine($"Historical News End: {requestId}"); }
    public void headTimestamp(int reqId, string headTimestamp) { Console.WriteLine($"Head Timestamp: {reqId}"); }
    public void histogramData(int reqId, HistogramEntry[] data) { Console.WriteLine($"Histogram Data: {reqId}"); }
    public void rerouteMktDataReq(int reqId, int conid, string exchange) { Console.WriteLine($"Reroute Mkt Data Req: {reqId}"); }
    public void rerouteMktDepthReq(int reqId, int conid, string exchange) { Console.WriteLine($"Reroute Mkt Depth Req: {reqId}"); }
    public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) { Console.WriteLine($"Market Rule: {marketRuleId}"); }
    public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) { Console.WriteLine($"PnL: {reqId}"); }
    public void pnlSingle(int reqId, int pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) { Console.WriteLine($"PnL Single: {reqId}"); }
    public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) { Console.WriteLine($"Historical Ticks: {reqId}"); }
    public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) { Console.WriteLine($"Historical Ticks Bid Ask: {reqId}"); }
    public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) { Console.WriteLine($"Historical Ticks Last: {reqId}"); }
    public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttrib attribs, string exchange, string specialConditions) { Console.WriteLine($"Tick By Tick All Last: {reqId}"); }
    public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttrib attribs) { Console.WriteLine($"Tick By Tick Bid Ask: {reqId}"); }
    public void tickByTickMidPoint(int reqId, long time, double midPoint) { Console.WriteLine($"Tick By Tick Mid Point: {reqId}"); }

    public List<IBKRPosition> GetCurrentPositions()
    {
        return new List<IBKRPosition>(_positions);
    }

    public decimal GetBuyingPower()
    {
        return _buyingPower;
    }

    public List<IBKROrderResponse> GetOpenOrders()
    {
        return new List<IBKROrderResponse>(_openOrders);
    }
    public int GetNextOrderId()
    {
        _clientSocket.reqIds(1);
        return _nextOrderId;
    }
}
