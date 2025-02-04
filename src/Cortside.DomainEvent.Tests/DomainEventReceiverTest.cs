using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Amqp;
using Amqp.Framing;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Cortside.DomainEvent.Tests {
    public class DomainEventReceiverTest {

        private readonly IServiceProvider serviceProvider;
        private readonly DomainEventReceiverSettings settings;
        private readonly MockLogger<DomainEventReceiver> logger;
        private readonly MockReceiver receiver;
        private readonly Mock<IReceiverLink> receiverLink;

        public DomainEventReceiverTest() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            serviceProvider = services.BuildServiceProvider();

            settings = new DomainEventReceiverSettings();

            logger = new MockLogger<DomainEventReceiver>();
            receiver = new MockReceiver(settings, serviceProvider, logger);
            receiver.Setup(new Dictionary<string, Type> {
                { typeof(TestEvent).FullName, typeof(TestEvent) }
            });

            receiverLink = new Mock<IReceiverLink>();
        }

        [Fact]
        public async Task ShouldHandleWelformedJson() {
            // arrange
            var @event = new TestEvent() { IntValue = 1 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            receiverLink.VerifyAll();
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
        }

        [Theory]
        [InlineData("{")]
        [InlineData("{ \"contractorId\": \"6677\", \"contractorNumber\": \"1037\" \"sponsorNumber\": \"2910\" }")]
        public async Task ShouldHandleMalformedJson(string body) {
            // arrange
            var @event = new TestEvent();
            var eventType = @event.GetType().FullName;
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("errors deserializing messsage body"));
        }

        [Fact]
        public async Task ShouldHandleInvalidType() {
            // arrange
            var @event = new TestEvent();
            var eventType = @event.GetType().FullName;
            var body = 1;
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("invalid type"));
        }

        [Fact]
        public async Task ShouldHandleByteArray() {
            // arrange
            var @event = new TestEvent() { IntValue = 1 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, GetByteArray(body));

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
            receiverLink.VerifyAll();
        }

        [Fact]
        public async Task ShouldHandleMessageTypeNotFound() {
            // arrange
            var @event = new TestEvent();
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));
            receiver.Setup(new Dictionary<string, Type>());

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("message type was not registered for type"));
        }

        [Fact]
        public async Task ShouldHandleHandlerNotFound() {
            // arrange
            var @event = new TestEvent();
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));
            var provider = new ServiceCollection().BuildServiceProvider();
            receiver.SetProvider(provider);

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("handler was not found for type"));
        }

        [Fact]
        public async Task ShouldHandleSuccessResult() {
            // arrange
            var @event = new TestEvent() { IntValue = 2 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            receiverLink.VerifyAll();
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
        }

        [Fact]
        public async Task ShouldHandleFailedResult() {
            // arrange
            var @event = new TestEvent() { IntValue = -1 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            receiverLink.VerifyAll();
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
        }

        [Fact(Skip = "no tx handling with containerhost")]
        public async Task ShouldHandleRetryResult() {
            // arrange
            var @event = new TestEvent() { IntValue = 0 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            receiverLink.VerifyAll();
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
        }

        [Fact]
        public async Task ShouldHandleUnhandledException() {
            // arrange
            var @event = new TestEvent() { IntValue = int.MinValue };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));

            // act
            await receiver.MessageCallback(receiverLink.Object, message).ConfigureAwait(false);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("caught unhandled exception"));
        }

        private Message CreateMessage(string eventType, object body) {
            var message = new Message(body) {
                ApplicationProperties = new ApplicationProperties(),
                Properties = new Properties() {
                    CorrelationId = Guid.NewGuid().ToString(),
                    MessageId = Guid.NewGuid().ToString()
                },
                Header = new Header() {
                    DeliveryCount = 1
                }
            };
            message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY] = eventType;
            return message;
        }

        public byte[] GetByteArray(string body) {
            MemoryStream stream = new MemoryStream();
            DataContractSerializer s = new DataContractSerializer(typeof(string));
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter(stream);
            writer.WriteStartDocument();
            s.WriteStartObject(writer, body);
            s.WriteObjectContent(writer, body);
            s.WriteEndObject(writer);
            writer.Flush();
            stream.Position = 0;

            return stream.ToArray();
        }
    }
}
