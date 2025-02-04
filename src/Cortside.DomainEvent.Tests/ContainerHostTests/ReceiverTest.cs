using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    public partial class ContainerHostTest : BaseHostTest {
        [Fact]
        public async Task ShouldReceiveMessage_Accept() {
            receiverSettings.Queue = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            host.RegisterMessageProcessor(receiverSettings.Queue + "TestEvent", new TestMessageProcessor(50, messages));
            linkProcessor = new TestLinkProcessor();
            host.RegisterLinkProcessor(linkProcessor);

            int count = 1;
            publisterSettings.Topic = receiverSettings.Queue;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventPublisher>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.PublishAsync(@event).ConfigureAwait(false);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            host.RegisterMessageSource(receiverSettings.Queue, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventReceiver>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = receiver.Receive(TimeSpan.FromSeconds(1));
                    Assert.NotNull(message.GetData<TestEvent>());
                    message.Accept();
                }
            }

            Assert.Equal(0, source.DeadLetterCount);
            Assert.Equal(0, source.Count);
        }

        [Fact]
        public async Task ShouldReceiveMessage_Reject() {
            receiverSettings.Queue = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            host.RegisterMessageProcessor(receiverSettings.Queue + "TestEvent", new TestMessageProcessor(50, messages));
            linkProcessor = new TestLinkProcessor();
            host.RegisterLinkProcessor(linkProcessor);

            int count = 1;
            publisterSettings.Topic = receiverSettings.Queue;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventPublisher>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.PublishAsync(@event).ConfigureAwait(false);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            host.RegisterMessageSource(receiverSettings.Queue, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventReceiver>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = receiver.Receive(TimeSpan.FromSeconds(1));
                    message.Reject();
                }
            }

            Assert.Equal(count, source.DeadLetterCount);
            Assert.Equal(0, source.Count);
        }

        [Fact(Skip = "concurrency issue")]
        public async Task ShouldReceiveMessage_Release() {
            receiverSettings.Queue = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            host.RegisterMessageProcessor(receiverSettings.Queue + "TestEvent", new TestMessageProcessor(50, messages));
            linkProcessor = new TestLinkProcessor();
            host.RegisterLinkProcessor(linkProcessor);

            int count = 1;
            publisterSettings.Topic = receiverSettings.Queue;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventPublisher>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.PublishAsync(@event).ConfigureAwait(false);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            host.RegisterMessageSource(receiverSettings.Queue, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventReceiver>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = receiver.Receive(TimeSpan.FromSeconds(1));
                    message.Release();
                }
            }

            Assert.Equal(0, source.DeadLetterCount);
            Assert.Equal(count, source.Count);
        }
    }
}
