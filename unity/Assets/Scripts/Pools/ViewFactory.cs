using Match3.Unity.Views;
using UnityEngine;

namespace Match3.Unity.Pools
{
    /// <summary>
    /// Factory for creating view GameObjects at runtime.
    /// No prefabs - all objects created dynamically.
    /// </summary>
    public static class ViewFactory
    {
        /// <summary>
        /// Create a TileView with SpriteRenderer.
        /// </summary>
        public static TileView CreateTileView(Transform parent)
        {
            var go = new GameObject("Tile");
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Tiles";
            renderer.sortingOrder = 10;

            var tileView = go.AddComponent<TileView>();
            return tileView;
        }

        /// <summary>
        /// Create a ProjectileView with SpriteRenderer.
        /// </summary>
        public static ProjectileView CreateProjectileView(Transform parent)
        {
            var go = new GameObject("Projectile");
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Projectiles";
            renderer.sortingOrder = 40;

            var projectileView = go.AddComponent<ProjectileView>();
            return projectileView;
        }

        /// <summary>
        /// Create a ParticleSystem for effects.
        /// </summary>
        public static ParticleSystem CreateEffect(Transform parent, string effectType)
        {
            var go = new GameObject($"Effect_{effectType}");
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            ConfigureParticle(ps, effectType);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = 30;

            return ps;
        }

        private static void ConfigureParticle(ParticleSystem ps, string effectType)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 2f;
            main.startSize = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Disable; // Auto-disable when done

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // Effect-specific configuration
            switch (effectType)
            {
                case "match_pop":
                case "pop":
                    main.startColor = new Color(1f, 0.9f, 0.3f);
                    main.startSize = 0.15f;
                    break;

                case "explosion":
                case "bomb_explosion":
                    main.startColor = new Color(1f, 0.5f, 0.1f);
                    main.startSpeed = 4f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
                    break;

                case "bomb_created":
                    main.startColor = new Color(0.8f, 0.8f, 1f);
                    main.startSpeed = 1f;
                    break;

                case "projectile_hit":
                case "projectile_explosion":
                    main.startColor = new Color(0.5f, 0.8f, 1f);
                    main.startSpeed = 3f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });
                    break;

                case "match_highlight":
                    main.startColor = new Color(1f, 1f, 0.5f, 0.5f);
                    main.startSpeed = 0f;
                    main.startLifetime = 0.2f;
                    break;
            }
        }
    }
}
