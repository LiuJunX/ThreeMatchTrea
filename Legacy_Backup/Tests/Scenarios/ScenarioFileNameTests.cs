using Xunit;
using Match3.Core.Scenarios;

namespace Match3.Tests.Scenarios
{
    public class ScenarioFileNameTests
    {
        [Theory]
        [InlineData("ValidName", "ValidName")]
        [InlineData("Space Name", "SpaceName")]
        [InlineData("Name!@#", "Name")]
        [InlineData("   Trim   ", "Trim")]
        [InlineData("", "scenario")]
        [InlineData(null, "scenario")]
        public void SanitizeFileStem_ReturnsSafeName(string input, string expected)
        {
            var result = ScenarioFileName.SanitizeFileStem(input);
            Assert.Equal(expected, result);
        }
    }
}
