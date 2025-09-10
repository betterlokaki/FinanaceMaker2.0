using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Packets;

namespace FinanceMaker.BackTester
{
    class YahooFinanceLiveDataQueue : IDataQueueHandler
    {
        public bool IsConnected => throw new NotImplementedException();

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void SetJob(LiveNodePacket job)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            throw new NotImplementedException();
        }
    }
}
