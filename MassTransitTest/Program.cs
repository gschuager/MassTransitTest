using MassTransit;
using MassTransit.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace MassTransitTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
              .MinimumLevel.Debug()
              .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
              .Enrich.FromLogContext()
              .WriteTo.Console()
              .CreateLogger();

            var provider = ConfigureServiceProvider();
            LogContext.ConfigureCurrentLogContext(provider.GetRequiredService<ILoggerFactory>());

            var bus = provider.GetRequiredService<IBusControl>();

            try
            {
                await bus.StartAsync();

                await bus.Send<DoWork>(new { });

                Console.ReadKey();
            }
            finally
            {
                await bus.StopAsync();
            }
        }

        private static ServiceProvider ConfigureServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddSerilog());

            EndpointConvention.Map<DoWork>(new Uri("queue:test-queue"));

            services.AddMassTransit(x =>
            {
                x.AddConsumers(Assembly.GetExecutingAssembly());

                x.AddBus(provider => Bus.Factory.CreateUsingInMemory(cfg =>
                {
                    cfg.ReceiveEndpoint("test-queue", x =>
                    {
                        x.UseInMemoryOutbox();
                        x.ConfigureConsumers(provider);
                    });
                }));
            });

            var provider = services.BuildServiceProvider();
            return provider;
        }
    }


    public class DoWorkConsumer : IConsumer<DoWork>
    {
        private readonly ILogger<DoWorkConsumer> logger;
        private readonly IPublishEndpoint publishEndpoint;

        public DoWorkConsumer(ILogger<DoWorkConsumer> logger, IPublishEndpoint publishEndpoint)
        {
            this.logger = logger;
            this.publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<DoWork> context)
        {
            logger.LogInformation(".... Do something 1");

            await context.Publish<SomeEvent2>(new { });

            logger.LogInformation(".... Do something 2");

            await publishEndpoint.Publish<SomeEvent1>(new { }); // <----- this is published right away

            logger.LogInformation(".... Do something 3");
        }
    }

    public interface DoWork
    {
    }

    public interface SomeEvent1
    {
    }

    public interface SomeEvent2
    {
    }

    public interface SomeEvent3
    {
    }
}
