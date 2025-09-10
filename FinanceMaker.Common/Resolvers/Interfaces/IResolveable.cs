namespace FinanceMaker.Common.Resolvers.Interfaces
{
    public interface IResolveable<TArgs>
	{
		bool IsRelevant(TArgs args);
	}
}

