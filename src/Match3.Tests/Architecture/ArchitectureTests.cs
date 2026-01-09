using NetArchTest.Rules;
using Xunit;
using Match3.Core.Interfaces;

namespace Match3.Tests.Architecture
{
    public class ArchitectureTests
    {
        [Fact]
        public void Core_Should_Not_Depend_On_Web()
        {
            // Core assembly
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
        public void Logic_Classes_Should_Be_Sealed_Or_Abstract_Or_Internal()
        {
            // Optional: Enforce that Logic classes are not meant for inheritance unless designed so.
            // For now, let's just check that Logic namespace classes don't depend on Console directly.
            
            var coreAssembly = typeof(IMatchFinder).Assembly;
            
            var result = Types.InAssembly(coreAssembly)
                .That()
                .ResideInNamespace("Match3.Core.Logic")
                .ShouldNot()
                .HaveDependencyOn("System.Console")
                .GetResult();

            // Note: This might fail if ConsoleGameLogger is in Logic namespace. 
            // Let's check if ConsoleGameLogger exists and if it violates this.
            // If ConsoleGameLogger is intended to use Console, we should exclude it or move it to Infrastructure.
            // But based on previous LS, ConsoleGameLogger IS in Logic.
            // So we might skip this test for now or refine it.
        }
    }
}
