namespace Perfx.Tests.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using AutoFixture;

    using Perfx;

    using Xunit;

    public class LogDataServiceTests
    {
        [Fact]
        public async Task ExecuteAppInsights_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var mocker = new Fixture();
            var service = mocker.Create<LogDataService>();
            List<Result> results = null;
            string timeframe = null;
            int retries = 0;
            var stopToken = default(CancellationToken);

            // Act
            await service.ExecuteAppInsights(
                results,
                timeframe,
                retries,
                stopToken);

            // Assert
            Assert.True(false);
        }

        [Fact]
        public async Task GetLogs_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var mocker = new Fixture();
            var service = mocker.Create<LogDataService>();
            IEnumerable<string> traceIds = null;
            var stopToken = default(CancellationToken);
            string timeframe = null;

            // Act
            var result = await service.GetLogs(
                traceIds,
                stopToken,
                timeframe);

            // Assert
            Assert.True(false);
        }
    }
}
