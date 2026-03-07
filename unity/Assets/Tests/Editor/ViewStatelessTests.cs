using System;
using System.Collections.Generic;
using System.Reflection;
using Match3.Core.Models.Enums;
using NUnit.Framework;

namespace Match3.Unity.Tests
{
    /// <summary>
    /// Structural tests that enforce View layer stateless conventions.
    /// View types must not cache upstream data (TileType, BombType, etc.) as instance fields.
    /// See: docs/01-architecture/core-patterns.md §14 "Tile Data Flow"
    /// </summary>
    public class ViewStatelessTests
    {
        // Types that represent upstream data — View should never store these as instance fields.
        private static readonly HashSet<Type> ForbiddenFieldTypes = new()
        {
            typeof(TileType),
            typeof(BombType),
        };

        // View types to scan. Add new View classes here as they are created.
        private static readonly string[] ViewTypeNames =
        {
            "Match3.Unity.Views.TileView",
            "Match3.Unity.Views.Tile3DView",
            "Match3.Unity.Views.ProjectileView",
            "Match3.Unity.Views.Projectile3DView",
            "Match3.Unity.Views.OutlineEffect",
        };

        [Test]
        public void ViewTypes_ShouldNotCacheUpstreamData()
        {
            var assembly = typeof(Match3.Unity.Views.ViewHelper).Assembly;
            var violations = new List<string>();

            foreach (var typeName in ViewTypeNames)
            {
                var viewType = assembly.GetType(typeName);
                if (viewType == null) continue;

                var fields = viewType.GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (var field in fields)
                {
                    if (ForbiddenFieldTypes.Contains(field.FieldType))
                    {
                        violations.Add(
                            $"{viewType.Name}.{field.Name} ({field.FieldType.Name}) " +
                            "— View must not cache upstream data. Use ViewHelper to compare renderer state.");
                    }
                }

                // Also check auto-property backing fields via properties
                var props = viewType.GetProperties(
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (var prop in props)
                {
                    if (ForbiddenFieldTypes.Contains(prop.PropertyType))
                    {
                        violations.Add(
                            $"{viewType.Name}.{prop.Name} ({prop.PropertyType.Name}) " +
                            "— View must not cache upstream data. Use ViewHelper to compare renderer state.");
                    }
                }
            }

            Assert.IsEmpty(violations,
                "View types must not cache upstream data as fields/properties:\n" +
                string.Join("\n", violations));
        }
    }
}
