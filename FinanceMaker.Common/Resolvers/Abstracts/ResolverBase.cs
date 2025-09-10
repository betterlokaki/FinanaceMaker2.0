using FinanceMaker.Common.Resolvers.Interfaces;

namespace FinanceMaker.Common.Resolvers.Abstracts
{
    public abstract class ResolverBase<TInterface, TArgs> where TInterface : IResolveable<TArgs>
    {
        protected readonly IEnumerable<TInterface> m_LogicsToResolve;

        public ResolverBase(IEnumerable<TInterface> logicsToResolve)
        {
            m_LogicsToResolve = logicsToResolve;
        }
        public virtual TInterface Resolve(TArgs args)
        {
            foreach (var logic in m_LogicsToResolve)
            {
                if (logic.IsRelevant(args))
                {
                    return logic;
                }
            }

            throw new ArgumentException("Input wasn't found relevant to any of the resolvers," +
                                        $"\n{m_LogicsToResolve.Select(logic => logic.GetType().Name).Aggregate((_, __) => $"{_}, {__}")}");
        }
    }
}

