using Match3.Core.Models.Enums;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views.Obstacles
{
    /// <summary>
    /// Presenter for PotionBottle obstacle: 4 colored sub-bottles tracked via bitmask.
    /// Each sub-bottle corresponds to a bit in State (bit 0 = Red, bit 1 = Green, etc.).
    /// Broken sub-bottles darken; all broken → destroy.
    /// </summary>
    public sealed class PotionBottlePresenter : IObstaclePresenter
    {
        private byte _lastRenderedState;

        // Sub-bottle colors: bit index → color
        private static readonly Color[] SubBottleColors =
        {
            new(0.90f, 0.15f, 0.10f), // bit 0: Red   (Item1)
            new(0.10f, 0.75f, 0.30f), // bit 1: Green (Item2)
            new(0.10f, 0.40f, 0.90f), // bit 2: Blue  (Item3)
            new(0.95f, 0.75f, 0.10f), // bit 3: Yellow(Item4)
        };

        public void Setup(ObstacleView view, byte stage, byte state)
        {
            _lastRenderedState = state;

            var mesh = MeshFactory.GetObstacleMesh(ObstacleType.PotionBottle);
            view.SetMesh(mesh);

            var mats = MeshFactory.GetObstacleMaterials(ObstacleType.PotionBottle);
            if (mats != null)
                view.SetMaterials(mats);

            ApplyStateColor(view, state);
        }

        public void ApplyDamage(ObstacleView view, float progress, byte newStage)
        {
            byte currentState = view.CurrentState;

            // Update color if state changed
            if (_lastRenderedState != currentState)
            {
                _lastRenderedState = currentState;
                ApplyStateColor(view, currentState);
            }

            // Shake animation: oscillate + decay
            float shake = Mathf.Sin(progress * Mathf.PI * 6f) * 0.06f * (1f - progress);
            view.SetLocalScaleMultiplier(1f + shake);
        }

        public void ApplyDeath(ObstacleView view, float progress)
        {
            // Shrink + fade
            float scale = 1f - progress;
            view.SetLocalScaleMultiplier(scale);
            view.SetAlpha(1f - progress);
        }

        public void OnUpdate(ObstacleView view, float dt)
        {
            // No idle animation
        }

        /// <summary>
        /// Blend sub-bottle colors based on remaining bitmask.
        /// Present sub-bottles contribute their color; broken ones contribute dark gray.
        /// </summary>
        private static void ApplyStateColor(ObstacleView view, byte state)
        {
            float r = 0f, g = 0f, b = 0f;
            int count = 0;

            for (int i = 0; i < SubBottleColors.Length; i++)
            {
                if ((state & (1 << i)) != 0)
                {
                    r += SubBottleColors[i].r;
                    g += SubBottleColors[i].g;
                    b += SubBottleColors[i].b;
                    count++;
                }
            }

            Color blended;
            if (count > 0)
            {
                blended = new Color(r / count, g / count, b / count, 1f);
                // Brightness proportional to remaining sub-bottles
                float brightness = 0.5f + 0.5f * (count / (float)SubBottleColors.Length);
                blended *= brightness;
                blended.a = 1f;
            }
            else
            {
                blended = new Color(0.2f, 0.2f, 0.2f, 1f); // all broken — dark
            }

            view.SetBaseColor(blended);
        }
    }
}
