using System;
using System.Threading.Tasks;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Database;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Xunit;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests {
    public class OutboxPublisherTest {
        private readonly IServiceProvider provider;

        public OutboxPublisherTest() {
            var services = new ServiceCollection();
            services.AddLogging();

            var options = new DbContextOptionsBuilder<EntityContext>()
                    .UseInMemoryDatabase($"DomainEventOutbox-{Guid.NewGuid()}")
                    .Options;
            var context = new EntityContext(options);
            Seed(context);
            services.AddSingleton(context);

            services.AddSingleton(new DomainEventPublisherSettings() { Topic = "topic." });
            services.AddTransient<IDomainEventOutboxPublisher, DomainEventOutboxPublisher<EntityContext>>();

            provider = services.BuildServiceProvider();
        }

        private void Seed(EntityContext context) {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var one = new Widget() { Text = "one", Height = 1, Width = 1 };
            var two = new Widget() { Text = "two", Height = 2, Width = 2 };

            context.AddRange(one, two);
            context.SaveChanges();
        }

        [Fact]
        public async Task CanGetWidgets() {
            // arrange
            var context = provider.GetService<EntityContext>();

            // act
            var widgets = await context.Widgets.ToListAsync().ConfigureAwait(false);

            // assert
            Assert.Equal(2, widgets.Count);
            Assert.Equal("one", widgets[0].Text);
            Assert.Equal("two", widgets[1].Text);
        }

        [Fact]
        public async Task ShouldPublishEvent1() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(@event).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
        }

        [Fact]
        public async Task ShouldPublishEvent2() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(@event, correlationId).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
        }

        [Fact]
        public async Task ShouldPublishEvent3() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(@event, new EventProperties() { CorrelationId = correlationId, MessageId = messageId }).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
        }

        [Fact]
        public async Task ShouldPublishEvent4() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(@event, new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId }).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Topic);
            Assert.Equal("baz", messages[0].RoutingKey);
        }

        [Fact]
        public async Task ShouldPublishEvent5() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(JsonConvert.SerializeObject(@event), new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId, MessageId = messageId }).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Topic);
            Assert.Equal("baz", messages[0].RoutingKey);
        }

        [Fact]
        public async Task ShouldScheduleEvent1() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(@event, scheduleDate).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            messages[0].ScheduledDate.Should().BeCloseTo(scheduleDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent2() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(@event, scheduleDate, new EventProperties() { CorrelationId = correlationId }).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            messages[0].ScheduledDate.Should().BeCloseTo(scheduleDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent3() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(@event, scheduleDate, new EventProperties() { CorrelationId = correlationId, MessageId = messageId }).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
            Assert.Equal(scheduleDate, messages[0].ScheduledDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent4() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(@event, scheduleDate, new EventProperties() { EventType = "foo", Topic = "bar", CorrelationId = correlationId }).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Topic);
            Assert.Equal("WidgetStateChangedEvent", messages[0].RoutingKey);
            Assert.Equal(scheduleDate, messages[0].ScheduledDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent5() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(JsonConvert.SerializeObject(@event), scheduleDate, new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId, MessageId = messageId }).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // assert
            var messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Topic);
            Assert.Equal("baz", messages[0].RoutingKey);
            Assert.Equal(scheduleDate, messages[0].ScheduledDate);
        }

    }
}
