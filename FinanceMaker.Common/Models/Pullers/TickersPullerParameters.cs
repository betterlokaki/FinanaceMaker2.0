namespace FinanceMaker.Common.Models.Pullers
{
    public record TickersPullerParameters
    {

        public static TickersPullerParameters BestBuyer => new TickersPullerParameters
        {
            MinPrice = 2,
            MaxPrice = 100,
            MaxAvarageVolume = 1_000_000_000,
            MinAvarageVolume = 1_000_000,
            MinVolume = 1_000_000,
            MaxVolume = 3_000_000_000,
            MinPresentageOfChange = 5,
            MaxPresentageOfChange = 120
        };

        public static TickersPullerParameters BestSellers =>  new TickersPullerParameters
        {
            MinPrice = 5,
            MaxPrice = 40,
            MaxAvarageVolume = 1_000_000_000,
            MinAvarageVolume = 1_000_000,
            MinVolume = 3_000_000,
            MaxVolume = 3_000_000_000,
            MinPresentageOfChange = -5,
            MaxPresentageOfChange = -40
        };
        // TODO: add volume not only average
        public double MinVolume { get; set; }
        public double MaxVolume { get; set; }
        public double MinPrice { get; set; }
        public double MaxPrice { get; set; }
        public int MinAvarageVolume { get; set; }
        public int MaxAvarageVolume { get; set; }
        public float MinPresentageOfChange { get; set; }
        public float MaxPresentageOfChange { get; set; }
    }
}

