using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cortside.DomainEvent.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    public partial class ContainerHostTest : BaseHostTest {
        [Fact(Skip = "hangs build")]
        public async Task ReceiverHostedService() {
            IServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddHostedService<ReceiverHostedService>();

            var receiver = new Mock<IDomainEventReceiver>();
            receiver.Setup(x => x.StartAndListen(It.IsAny<IDictionary<string, Type>>()));
            services.AddSingleton<IDomainEventReceiver>(receiver.Object);
            services.AddSingleton(new ReceiverHostedServiceSettings() { Enabled = true, MessageTypes = new Dictionary<string, Type>() });

            var serviceProvider = services.BuildServiceProvider();

            var service = serviceProvider.GetService<IHostedService>() as ReceiverHostedService;

            CancellationTokenSource source = new CancellationTokenSource();
            await service.StartAsync(source.Token).ConfigureAwait(false);

            await Task.Delay(1000).ConfigureAwait(false);
            receiver.VerifyAll();

            source.Cancel();
            await service.StopAsync(source.Token).ConfigureAwait(false);
        }
    }
}
