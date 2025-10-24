using System.Text.Json.Serialization;
using CloneExtensions;
using QuantConnect;

namespace FinanceMaker.Common.Models.Finance
{
    public class FinanceCandleStick
    {
        public static FinanceCandleStick Empty => new FinanceCandleStick(default, 0f, 0f, 0f, 0f, 0L);
        #region Boring Data

        public DateTime Time => Candlestick?.Time ?? DateTime.MaxValue;
        public float Open => (float?)(Candlestick?.Open) ?? 0f;
        public float Close => (float?)(Candlestick?.Close) ?? 0f;
        public float High => (float?)Candlestick?.High ?? 0;
        public float Low => (float?)Candlestick?.Low ?? 0;
        public long Volume { get; set; }

        #endregion

        public bool IsGreen => Close > Open;
        public bool IsRed => Open > Close;

        #region Why Do I do stuff Data

        [JsonIgnore]
        public Candlestick Candlestick { get; set; }

        #endregion

        public FinanceCandleStick(
            DateTime dateTime,
            float open,
            float close,
            float high,
            float low,
            long volume)
        {
            Candlestick = new Candlestick(
                dateTime,
                Convert.ToDecimal(open),
                Convert.ToDecimal(high),
                Convert.ToDecimal(low),
                Convert.ToDecimal(close));
            Volume = volume;
            // EMASignal = TrendTypes.NoChange;
            // BreakThrough = TrendTypes.NoChange;
            // Pivot = Pivot.Unchanged;
        }

        public FinanceCandleStick(
            DateTime dateTime,
            decimal open,
            decimal close,
            decimal high,
            decimal low,
            long volume)
        {
            Candlestick = new Candlestick(dateTime, open, high, low, close);
            Volume = volume;
        }
        public FinanceCandleStick(
            FinanceCandleStick candleStick)
        {
            Candlestick = candleStick.Candlestick.GetClone();
            Volume = candleStick.Volume;
        }

        public FinanceCandleStick(Candlestick candlestick)
        {
            Candlestick = candlestick;
        }

        public virtual FinanceCandleStick Clone()
        {
            return new FinanceCandleStick(this);
        }

        public bool IsItHammer()
        {
            var body = Math.Abs(Close - Open);
            var lowerShadow = Open > Close ? Close - Low : Open - Low;
            var upperShadow = High - Math.Max(Close, Open);

            // Hammer criteria: small body, long lower shadow, little or no upper shadow
            if (body == 0) return false; // Avoid doji
            bool isHammer = lowerShadow >= 2 * body && upperShadow <= body;
            return isHammer;
        }

        public bool HasFatBody(float thresholdRatio = 0.6f)
        {
            if (thresholdRatio < 0 || thresholdRatio > 1)
            {
                throw new ArgumentException($"{thresholdRatio} is not in range 0-1");
            }
            var body = Math.Abs(Close - Open);
            var range = High - Low;

            if (range == 0) return false;
            var ratio = body / range;
            return ratio >= thresholdRatio;
        }
    }
}

