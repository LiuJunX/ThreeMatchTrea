using System.Linq;
using Match3.Core.Events;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
using NetArchTest.Rules;
using Xunit;

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
                .And()
                .HaveDependencyOn("Microsoft.AspNetCore")
                .GetResult();

            Assert.True(result.IsSuccessful, "Match3.Core should not depend on Unity or ASP.NET Core");
        }

        [Fact]
        public void Systems_Should_Implement_Interfaces()
        {
            var coreAssembly = typeof(IMatchFinder).Assembly;

            var systemTypes = Types.InAssembly(coreAssembly)
                .That()
                .HaveNameEndingWith("System")
                .And()
                .AreClasses()
                .And()
                .ArePublic()
                .And()
                .ResideInNamespaceStartingWith("Match3.Core.Systems")
                .GetTypes();

            var violations = systemTypes
                .Where(t => !t.GetInterfaces().Any(i => i.Name.StartsWith("I")))
                .Select(t => t.FullName)
                .ToList();

            Assert.True(
                violations.Count == 0,
                $"System classes should implement an interface starting with 'I'. Violations: {string.Join(", ", violations)}");
        }

        [Fact]
        public void Core_Should_Not_Depend_On_Editor()
        {
            var coreAssembly = typeof(IMatchFinder).Assembly;

            var result = Types.InAssembly(coreAssembly)
                .ShouldNot()
                .HaveDependencyOn("Match3.Editor")
                .GetResult();

            Assert.True(result.IsSuccessful, "Match3.Core should not depend on Match3.Editor");
        }

        [Fact]
        public void Core_Should_Not_Depend_On_Presentation()
        {
            var coreAssembly = typeof(IMatchFinder).Assembly;

            var result = Types.InAssembly(coreAssembly)
                .ShouldNot()
                .HaveDependencyOn("Match3.Presentation")
                .GetResult();

            Assert.True(result.IsSuccessful, "Match3.Core should not depend on Match3.Presentation");
        }

        [Fact]
        public void Event_Types_Inheriting_GameEvent_Should_Be_Sealed()
        {
            var coreAssembly = typeof(IMatchFinder).Assembly;

            var result = Types.InAssembly(coreAssembly)
                .That()
                .Inherit(typeof(GameEvent))
                .And()
                .AreNotAbstract()
                .Should()
                .BeSealed()
                .GetResult();

            var failedNames = result.FailingTypeNames ?? Enumerable.Empty<string>();
            Assert.True(
                result.IsSuccessful,
                $"All concrete GameEvent subclasses should be sealed. Violations: {string.Join(", ", failedNames)}");
        }

        [Fact]
        public void Core_Should_Not_Use_System_Random()
        {
            var coreAssembly = typeof(IMatchFinder).Assembly;

            var result = Types.InAssembly(coreAssembly)
                .ShouldNot()
                .HaveDependencyOn("System.Random")
                .GetResult();

            Assert.True(result.IsSuccessful, "Match3.Core should not depend on System.Random — use IRandom instead");
        }
    }
}
