using System;
using System.Collections.Generic;
using System.Diagnostics;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Utility.Pools;
using Xunit;
using Xunit.Abstractions;

namespace Match3.Core.Tests.Systems.Matching
{
    public class BombGeneratorPerformanceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly BombGenerator _generator;

        public BombGeneratorPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _generator = new BombGenerator();
        }

        [Fact]
        public void Generate_HugeBlock_ShouldRunFast()
        {
            // Create a 9x9 block of same-colored gems
            int width = 9;
            int height = 9;
            var component = new HashSet<Position>();
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    component.Add(new Position(x, y));
                }
            }

            _output.WriteLine($"Testing with {component.Count} cells.");

            // Warm up
            _generator.Generate(component);

            // Measure
            var sw = Stopwatch.StartNew();
            var results = _generator.Generate(component);
            sw.Stop();

            _output.WriteLine($"Time elapsed: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Results count: {results.Count}");
            foreach (var r in results)
            {
                _output.WriteLine($"Result: {r.SpawnBombType}, Size: {r.Positions.Count}");
            }

            // Assertion: Should be under 100ms (Greedy fallback should make this instant)
            // Without optimization, this might hang or take seconds.
            Assert.True(sw.ElapsedMilliseconds < 200, $"Performance check failed: {sw.ElapsedMilliseconds}ms");
        }
    }
}
