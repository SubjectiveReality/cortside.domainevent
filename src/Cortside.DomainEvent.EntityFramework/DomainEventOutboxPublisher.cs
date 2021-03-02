#pragma warning disable CS0067

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cortside.DomainEvent.EntityFramework {
    public class DomainEventOutboxPublisher<TDbContext> : IDomainEventOutboxPublisher where TDbContext : DbContext {

        public const string MESSAGE_TYPE_KEY = "Message.Type.FullName";
        public const string SCHEDULED_ENQUEUE_TIME_UTC = "x-opt-scheduled-enqueue-time";

        protected ServiceBusSettings Settings { get; }

        private readonly TDbContext context;

        protected ILogger<DomainEventOutboxPublisher<TDbContext>> Logger { get; }

        public DomainEventOutboxPublisher(ServiceBusPublisherSettings settings, TDbContext context, ILogger<DomainEventOutboxPublisher<TDbContext>> logger) {
            this.Settings = settings;
            this.context = context;
            Logger = logger;
        }

        public DomainEventError Error { get; set; }

        public event PublisherClosedCallback Closed;

        public async Task SendAsync<T>(T @event) where T : class {
            var data = JsonConvert.SerializeObject(@event);
            var eventType = @event.GetType().FullName;
            var address = Settings.Address + @event.GetType().Name;
            await InnerSendAsync(eventType, address, data, null, null);
        }

        public async Task SendAsync<T>(T @event, string correlationId) where T : class {
            var data = JsonConvert.SerializeObject(@event);
            var eventType = @event.GetType().FullName;
            var address = Settings.Address + @event.GetType().Name;
            await InnerSendAsync(eventType, address, data, correlationId, null);
        }

        public async Task SendAsync<T>(T @event, string correlationId, string messageId) where T : class {
            var data = JsonConvert.SerializeObject(@event);
            var eventType = @event.GetType().FullName;
            var address = Settings.Address + @event.GetType().Name;
            await InnerSendAsync(eventType, address, data, correlationId, messageId);
        }

        public async Task SendAsync<T>(T @event, string eventType, string address, string correlationId) where T : class {
            var data = JsonConvert.SerializeObject(@event);
            await InnerSendAsync(eventType, address, data, correlationId, null);
        }

        public async Task SendAsync(string eventType, string address, string data, string correlationId, string messageId) {
            await InnerSendAsync(eventType, address, data, correlationId, messageId);
        }

        public async Task ScheduleMessageAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc) where T : class {
            var data = JsonConvert.SerializeObject(@event);
            var eventType = @event.GetType().FullName;
            var address = Settings.Address + @event.GetType().Name;
            await InnerSendAsync(eventType, address, data, null, null, scheduledEnqueueTimeUtc);

        }

        public async Task ScheduleMessageAsync<T>(T @event, string correlationId, DateTime scheduledEnqueueTimeUtc) where T : class {
            var data = JsonConvert.SerializeObject(@event);
            var eventType = @event.GetType().FullName;
            var address = Settings.Address + @event.GetType().Name;
            await InnerSendAsync(eventType, address, data, correlationId, null, scheduledEnqueueTimeUtc);
        }

        public async Task ScheduleMessageAsync<T>(T @event, string correlationId, string messageId, DateTime scheduledEnqueueTimeUtc) where T : class {
            var data = JsonConvert.SerializeObject(@event);
            var eventType = @event.GetType().FullName;
            var address = Settings.Address + @event.GetType().Name;
            await InnerSendAsync(eventType, address, data, correlationId, messageId, scheduledEnqueueTimeUtc);
        }

        public async Task ScheduleMessageAsync<T>(T @event, string eventType, string address, string correlationId, DateTime scheduledEnqueueTimeUtc) where T : class {
            var data = JsonConvert.SerializeObject(@event);
            await InnerSendAsync(eventType, address, data, correlationId, null, scheduledEnqueueTimeUtc);

        }

        public async Task ScheduleMessageAsync(string data, string eventType, string address, string correlationId, string messageId, DateTime scheduledEnqueueTimeUtc) {
            await InnerSendAsync(eventType, address, data, correlationId, messageId, scheduledEnqueueTimeUtc);
        }

        private async Task InnerSendAsync(string eventType, string address, string data, string correlationId, string messageId, DateTime? scheduledEnqueueTimeUtc = null) {
            var date = DateTime.UtcNow;
            var messageIdentifier = messageId ?? Guid.NewGuid().ToString();

            Logger.LogDebug($"Queueing message {messageIdentifier} with body: {data}");
            await context.Set<Outbox>().AddAsync(new Outbox() {
                MessageId = messageIdentifier,
                CorrelationId = correlationId,
                EventType = eventType,
                Address = address,
                Body = data,
                CreatedDate = date,
                ScheduledDate = scheduledEnqueueTimeUtc ?? date,
                Status = OutboxStatus.Queued
            });
        }
    }
}
