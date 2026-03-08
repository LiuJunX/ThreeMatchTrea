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
        private static Material _particleMaterial;

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
            renderer.sharedMaterial = GetOrCreateParticleMaterial();

            return ps;
        }

        // Cached gradient keys to avoid allocation per particle system
        private static readonly GradientColorKey[] FadeColorKeys =
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        };
        private static readonly GradientAlphaKey[] FadeAlphaKeys =
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(0f, 1f)
        };

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
            gradient.SetKeys(FadeColorKeys, FadeAlphaKeys);
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // Effect-specific configuration
            switch (effectType)
            {
                case "match_pop":
                case "pop":
                    // Colorful outward burst: 8-12 small particles, short lifetime
                    main.startLifetime = 0.3f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.9f, 0.3f),
                        new Color(1f, 0.6f, 0.2f));
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 12) });
                    shape.radius = 0.1f;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    break;

                case "explosion":
                case "bomb_explosion":
                    // Large radial explosion: 16-24 particles with size decay
                    main.startLifetime = 0.5f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.5f, 0.1f),
                        new Color(1f, 0.8f, 0.2f));
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16, 24) });
                    shape.radius = 0.15f;
                    // Add velocity damping
                    var velocityOverLifetime = ps.velocityOverLifetime;
                    velocityOverLifetime.enabled = true;
                    velocityOverLifetime.speedModifier = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));
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

                case "bomb_shockwave":
                    // Ring-shaped outward burst for shockwave
                    main.startLifetime = 0.4f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 0.9f, 0.5f, 0.8f),
                        new Color(1f, 0.7f, 0.3f, 0.6f));
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 40) });
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.1f;
                    shape.radiusThickness = 0f; // Edge only (ring shape)
                    var shockVel = ps.velocityOverLifetime;
                    shockVel.enabled = true;
                    shockVel.speedModifier = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));
                    break;

                case "color_bomb_wave":
                    // Rainbow expanding ring burst at color bomb origin
                    main.startLifetime = 0.5f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
                    // Rainbow gradient: cycle through hues
                    var waveGrad = new Gradient();
                    waveGrad.SetKeys(
                        new[]
                        {
                            new GradientColorKey(new Color(1f, 0.2f, 0.2f), 0f),
                            new GradientColorKey(new Color(1f, 1f, 0.2f), 0.25f),
                            new GradientColorKey(new Color(0.2f, 1f, 0.4f), 0.5f),
                            new GradientColorKey(new Color(0.3f, 0.5f, 1f), 0.75f),
                            new GradientColorKey(new Color(0.8f, 0.3f, 1f), 1f)
                        },
                        new[]
                        {
                            new GradientAlphaKey(1f, 0f),
                            new GradientAlphaKey(0f, 1f)
                        });
                    main.startColor = new ParticleSystem.MinMaxGradient(waveGrad);
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 35, 45) });
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.15f;
                    shape.radiusThickness = 0f; // Edge only (ring)
                    var waveVel = ps.velocityOverLifetime;
                    waveVel.enabled = true;
                    waveVel.speedModifier = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
                    break;

                case "color_bomb_hit":
                    // Bright sparkle impact burst at target tile
                    main.startLifetime = 0.25f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(1f, 1f, 0.5f),
                        new Color(1f, 0.8f, 0.3f));
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 14) });
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.1f;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    var hitVel = ps.velocityOverLifetime;
                    hitVel.enabled = true;
                    hitVel.speedModifier = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));
                    break;

                case "bomb_flash":
                    // Brief bright flash at explosion center
                    main.startLifetime = 0.15f;
                    main.startSpeed = 0f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                    main.startColor = new Color(1f, 1f, 0.8f, 1f);
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.01f;
                    sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                        new AnimationCurve(
                            new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
                    break;
            }
        }

        /// <summary>
        /// Get or create a URP-compatible particle material (Unlit, vertex color).
        /// Avoids magenta when using URP.
        /// </summary>
        private static Material GetOrCreateParticleMaterial()
        {
            if (_particleMaterial != null)
                return _particleMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _particleMaterial = new Material(shader);
            return _particleMaterial;
        }
    }
}
