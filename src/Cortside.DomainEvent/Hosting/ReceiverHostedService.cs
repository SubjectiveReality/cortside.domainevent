using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Hosting {
    /// <summary>
    /// Message receiver hosted service
    /// </summary>
    public class ReceiverHostedService : BackgroundService {
        private readonly ILogger logger;
        private readonly IServiceProvider services;
        private readonly ReceiverHostedServiceSettings settings;
        private IDomainEventReceiver receiver;

        /// <summary>
        /// Message receiver hosted service
        /// </summary>
        public ReceiverHostedService(ILogger<ReceiverHostedService> logger, IServiceProvider services, ReceiverHostedServiceSettings settings) {
            this.logger = logger;
            this.services = services;
            this.settings = settings;
        }

        public override async Task StartAsync(CancellationToken cancellationToken) {
            logger.LogInformation($"ReceiverHostedService StartAsync() entered.");
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Interface method to start service
        /// </summary>
        protected async override Task ExecuteAsync(CancellationToken stoppingToken) {
            await Task.Yield();

            if (!settings.Enabled) {
                logger.LogInformation("ReceiverHostedService is not enabled");
            } else if (settings.MessageTypes == null) {
                logger.LogError("Configuration error:  No event types have been configured for the receiverhostedeservice");
            } else {
                while (!stoppingToken.IsCancellationRequested) {
                    if (receiver == null || receiver.Link == null || receiver.Link.IsClosed) {
                        DisposeReceiver();
                        receiver = services.GetService<IDomainEventReceiver>();
                        logger.LogInformation($"Starting receiver...");
                        try {
                            receiver.StartAndListen(settings.MessageTypes);
                            logger.LogInformation("Receiver started");
                        } catch (Exception e) {
                            logger.LogCritical($"Unable to start receiver. \n {e}");
                        }
                        receiver.Closed += OnReceiverClosed;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(settings.TimedInterval)).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Interface method to stop service
        /// </summary>
        public override Task StopAsync(CancellationToken cancellationToken) {
            logger.LogInformation("Receiver Hosted Service is stopping.");
            DisposeReceiver();
            return Task.CompletedTask;
        }

        private void OnReceiverClosed(IDomainEventReceiver receiver, DomainEventError error) {
            if (error == null) {
                logger.LogError("Handling OnReceiverClosed event with no error information");
            } else {
                logger.LogError($"Handling OnReceiverClosed event with error: {error.Condition} - {error.Description}");
            }
        }

        private void DisposeReceiver() {
            receiver?.Close();
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public override void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Finalizer.
        /// </summary>
        ~ReceiverHostedService() {
            Dispose(false);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing) {
            DisposeReceiver();
        }
    }
}
