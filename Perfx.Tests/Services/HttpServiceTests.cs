namespace Perfx.Tests.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    using AutoFixture;

    using Perfx;

    using Xunit;

    public class HttpServiceTests
    {
        [Fact]
        public async Task ProcessRequest_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var mocker = new Fixture();
            var service = mocker.Create<HttpService>();
            Result record = null;
            CancellationToken stopToken = default(CancellationToken);

            // Act
            var result = await service.ProcessRequest(
                record,
                stopToken);

            // Assert
            Assert.True(false);
        }

        [Fact]
        public void Dispose_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var mocker = new Fixture();
            var service = mocker.Create<HttpService>();

            // Act
            service.Dispose();

            // Assert
            Assert.True(false);
        }
    }
}
