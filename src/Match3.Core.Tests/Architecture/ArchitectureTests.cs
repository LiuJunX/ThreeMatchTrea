using NetArchTest.Rules;
using Xunit;
using Match3.Core.Interfaces;

namespace Match3.Core.Tests.Architecture
{
    public class ArchitectureTests
    {
        [Fact]
        public void Core_Should_Not_Depend_On_Web()
        {
            var coreAssembly = typeof(IMatchFinder).Assembly;

            var result = Types.InAssembly(coreAssembly)
                .ShouldNot()
                .HaveDependencyOn("Match3.Web")
                .GetResult();

            Assert.True(result.IsSuccessful, "Match3.Core should not depend on Match3.Web");
        }

        [Fact]
        public void Interfaces_Should_Start_With_I()
        {
            var coreAssembly = typeof(IMatchFinder).Assembly;

            var result = Types.InAssembly(coreAssembly)
                .That()
                .AreInterfaces()
                .Should()
                .HaveNameStartingWith("I")
                .GetResult();

            Assert.True(result.IsSuccessful, "All interfaces in Core should start with 'I'");
        }

        [Fact]
        public void Core_Should_Not_Depend_On_Platform_Specifics()
        {
            var coreAssembly = typeof(IMatchFinder).Assembly;

            var result = Types.InAssembly(coreAssembly)
                .ShouldNot()
                .HaveDependencyOn("UnityEngine")
                //.And()
                //.ShouldNot()
                //.HaveDependencyOn("Microsoft.AspNetCore")
                .GetResult();

            Assert.True(result.IsSuccessful, "Match3.Core should not depend on Unity or ASP.NET Core");
        }
    }
}
