namespace FinanceMaker.Common.Models.Pullers.YahooFinance
{
	#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public class NewsRequestModel
	{
		public Service serviceConfig { get; set; }

		public static NewsRequestModel CreateCloneToYahoo()
		{
			return new NewsRequestModel
			{
				serviceConfig = new Service
				{
					count = 40
				}
			};
		}
    }

	public class Service
	{
		public int count { get; set; }
	}
	#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}

