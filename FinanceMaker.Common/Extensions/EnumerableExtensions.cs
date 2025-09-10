namespace FinanceMaker.Common.Extensions
{
	public static class EnumerableExtensions
	{
		public static int GetNonEnumeratedCount<T>(this IEnumerable<T> enumerable)
		{
			return enumerable.TryGetNonEnumeratedCount(out int count) ? count : enumerable.Count();
		}

		public static bool NullOrEmpty<T>(this IEnumerable<T> enumerable)
		{
			return enumerable is null || !enumerable.Any();
		}
	}
}

