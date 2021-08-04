using AntMeehan.NServiceBusMissingMessage.Commands;
using AntMeehan.NServiceBusMissingMessage.Events;
using Autofac;
using log4net.Config;
using NServiceBus;
using Raven.Client.Documents;
using System;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace AntMeehan.NServiceBusMissingMessage
{
    class Program
    {
        static async Task Main(string[] args)
        {
            NServiceBus.Logging.LogManager.Use<Log4NetFactory>();
            XmlConfigurator.Configure();

            var container = CreateContainer();
            var endpointConfiguration = CreateEndpointConfiguration(container);

            var endpointInstance = await Endpoint.Start(endpointConfiguration);

            Console.WriteLine("Running....");
            Console.WriteLine("E to raise event, Q to quit");
            ConsoleKeyInfo command;
            do
            {
                command = Console.ReadKey();
                Console.WriteLine();

                if (command.Key == ConsoleKey.E)
                {
                    Console.WriteLine("Raising AccountEvent");
                    await endpointInstance.Publish(new AccountEvent());
                }
            } while (command.Key != ConsoleKey.Q);

            await endpointInstance.Stop();
        }

        public static IContainer CreateContainer()
        {
            var builder = new ContainerBuilder();

            var assembly = Assembly.GetExecutingAssembly();
            builder.RegisterAssemblyModules(assembly);

            builder.RegisterAssemblyTypes(assembly)
                .AsImplementedInterfaces()
                .AsSelf();

            builder.Register(context =>
            {
                var documentStore = new DocumentStore
                {
                    Database = "AntMeehan.NServiceBusMissingMessage",
                    Urls = new[] { "https://localhost" },
                    Certificate = GetRavenDbClientCertificate()
                };
                documentStore.Initialize();

                return documentStore;
            })
                .As<IDocumentStore>()
                .SingleInstance();

            var container = builder.Build();

            return container;
        }
        private static readonly Type _concurrencyExceptionType = typeof(Raven.Client.Exceptions.ConcurrencyException);

        public static EndpointConfiguration CreateEndpointConfiguration(IContainer container)
        {
            var endpointConfiguration = new EndpointConfiguration("AntMeehan.NServiceBusMissingMessage");
            endpointConfiguration.EnableOutbox();

            var routing = endpointConfiguration.UseTransport<MsmqTransport>().Routing();

            routing.RouteToEndpoint(typeof(PropertyCommand), "AntMeehan.NServiceBusMissingMessage");
            routing.RouteToEndpoint(typeof(AddPropertyToBatchCommand), "AntMeehan.NServiceBusMissingMessage");
          
            endpointConfiguration.UseContainer<AutofacBuilder>(x => x.ExistingLifetimeScope(container));


            endpointConfiguration.UnitOfWork().WrapHandlersInATransactionScope();

            routing.RegisterPublisher(typeof(AccountEvent), "AntMeehan.NServiceBusMissingMessage");
            routing.RegisterPublisher(typeof(PropertyAssignedBatchEvent), "AntMeehan.NServiceBusMissingMessage");
            routing.RegisterPublisher(typeof(PropertyAddedToBatchEvent), "AntMeehan.NServiceBusMissingMessage");
            
            endpointConfiguration.SendFailedMessagesTo("error");
            endpointConfiguration.AuditProcessedMessagesTo("audit");
            endpointConfiguration.SendHeartbeatTo("particular.servicecontrol");

            // NOTE: LimitMessageProcessingConcurrencyTo set to 1 to since batching updates the same document) 
            endpointConfiguration.LimitMessageProcessingConcurrencyTo(1);

            endpointConfiguration.Recoverability()
              .CustomPolicy((config, context) => {

                    // Custom configuration for concurrency exceptions, to try harder than the default to get them through
                    if (_concurrencyExceptionType.IsInstanceOfType(context.Exception))
                  {
                      if (context.DelayedDeliveriesPerformed > 1000)
                      {
                          return RecoverabilityAction.MoveToError(config.Failed.ErrorQueue);
                      }

                      return RecoverabilityAction.DelayedRetry(TimeSpan.FromSeconds(5));
                  }

                  return DefaultRecoverabilityPolicy.Invoke(config, context);
              });

            var documentStore = new DocumentStore
            {
                Database = "AntMeehan.NServiceBusMissingMessage",
                Urls = new[] { "https://localhost" },
                Certificate = GetRavenDbClientCertificate(),
            };
            documentStore.Initialize();

            var configDocumentStore = new DocumentStore
            {
                Database = "AntMeehan.NServiceBusMissingMessage.Config",
                Urls = new[] { "https://localhost" },
                Certificate = GetRavenDbClientCertificate(),
            };
            configDocumentStore.Initialize();

            endpointConfiguration.UsePersistence<RavenDBPersistence>()
                .SetDefaultDocumentStore(documentStore)
                .UseDocumentStoreForSagas(documentStore)
                .UseDocumentStoreForTimeouts(documentStore)
                .UseDocumentStoreForGatewayDeduplication(configDocumentStore)
                .UseDocumentStoreForSubscriptions(configDocumentStore);

#if DEBUG
            endpointConfiguration.EnableInstallers();
#endif           

            return endpointConfiguration;
        }



        private static X509Certificate2 GetRavenDbClientCertificate()
        {
            var friendlyName = "CN=online-services";

            using (var store = new X509Store(StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                foreach (var certificate in store.Certificates)
                {
                    if (certificate.FriendlyName == friendlyName)
                    {
                        return certificate;
                    }
                }

            }
            throw new Exception($"Unable to find RavenDB certificate with FriendlyName '{friendlyName}'");
        }
    }
}
