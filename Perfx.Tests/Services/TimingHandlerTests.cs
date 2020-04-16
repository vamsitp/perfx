namespace Perfx.Tests.Services
{
    using AutoFixture;

    using Perfx;

    using Xunit;

    public class TimingHandlerTests
    {
        [Fact]
        public void SendAsync_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var mocker = new Fixture();
            var timingHandler = mocker.Create<TimingHandler>();

            // Act


            // Assert
            Assert.True(false);
        }
    }
}
