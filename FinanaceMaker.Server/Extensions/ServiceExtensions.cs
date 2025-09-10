namespace FinanaceMaker.Server;

public static class ServiceExtensions
{

    public static TService AddAndGetService<TService>(this IServiceProvider serviceProvider,
                                                           IServiceCollection serviceCollection
                                                           ) where TService : class
    {
        serviceCollection.AddSingleton<TService>();
        var serviceAdded = serviceProvider.GetService<TService>();

        if (serviceAdded is null)
        {
            throw new Exception($"Couldn\'t resolve service from type {typeof(TService).FullName}");
        }

        return serviceAdded;
    }

    // public static TService AddAndGetService<TService>(
    //     this IServiceCollection serviceCollection) 
    //     where TService : class
    // {
    //     serviceCollection.AddSingleton<TService>();

    //     var servicesAdded = serviceCollection.Create
    // }

}
