namespace Perfx.Tests.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    using AutoFixture;

    using Perfx;

    using Xunit;

    // https://github.com/AutoFixture/AutoFixture/wiki/Cheat-Sheet
    public class BenchmarkServiceTests
    {
        [Fact]
        public async Task Execute_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var mocker = new Fixture();
            var service = mocker.Create<BenchmarkService>();
            int? iterations = null;
            var stopToken = default(CancellationToken);

            // Act
            var result = await service.Execute(
                iterations,
                stopToken);

            // Assert
            Assert.True(false);
        }

        [Fact]
        public void Dispose_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var mocker = new Fixture();
            var service = mocker.Create<BenchmarkService>();

            // Act
            service.Dispose();

            // Assert
            Assert.True(false);
        }
    }
}
