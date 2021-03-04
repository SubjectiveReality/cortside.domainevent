using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Tests {
    public class TestReceiver : DomainEventReceiver {

        public TestReceiver(MessageBrokerReceiverSettings settings, IServiceProvider provider, ILogger<DomainEventReceiver> logger) : base(settings, provider, logger) {
        }

        public void Setup(IDictionary<string, Type> eventTypeLookup) {
            EventTypeLookup = eventTypeLookup;
        }

        public async Task MessageCallback(IReceiverLink receiver, Message message) {
            await OnMessageCallback(receiver, message);
        }

        internal void SetProvider(ServiceProvider provider) {
            base.Provider = provider;
        }
    }
}
