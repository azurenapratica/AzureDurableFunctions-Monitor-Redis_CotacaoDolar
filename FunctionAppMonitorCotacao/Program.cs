using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services => {
        services.AddSingleton<ConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(
                Environment.GetEnvironmentVariable("RedisConnections")!));
    })
    .Build();

host.Run();
