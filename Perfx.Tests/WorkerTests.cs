namespace Perfx.Tests
{
    using AutoFixture;

    using Perfx;

    using Xunit;

    public class WorkerTests
    {
        [Fact]
        public void TestMethod1()
        {
            // Arrange
            var mocker = new Fixture();
            var worker = mocker.Create<Worker>();

            // Act


            // Assert
            Assert.True(false);
        }
    }
}
