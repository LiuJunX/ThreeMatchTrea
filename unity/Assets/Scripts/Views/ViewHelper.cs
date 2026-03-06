using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Safe setters for View rendering properties.
    /// Compares against the renderer's actual state before writing,
    /// avoiding unnecessary updates and per-frame allocations.
    /// </summary>
    public static class ViewHelper
    {
        public static void SetSprite(SpriteRenderer renderer, Sprite target)
        {
            if (renderer.sprite != target)
                renderer.sprite = target;
        }

        public static void SetMesh(MeshFilter filter, Mesh target)
        {
            if (filter.sharedMesh != target)
                filter.sharedMesh = target;
        }

        /// <summary>
        /// Set shared materials from a pre-cached array (e.g. MeshFactory.GetTileMaterialArray).
        /// Compares first element + length to avoid the GC cost of sharedMaterials getter/setter.
        /// </summary>
        public static void SetMaterials(MeshRenderer renderer, Material[] target)
        {
            var current = renderer.sharedMaterials;
            if (current.Length == target.Length && current[0] == target[0])
                return;
            renderer.sharedMaterials = target;
        }

        public static void SetBombOverlay(SpriteRenderer overlay, GameObject overlayGo, Sprite target)
        {
            if (target == null)
            {
                if (overlayGo.activeSelf)
                    overlayGo.SetActive(false);
                return;
            }

            if (overlay.sprite != target)
                overlay.sprite = target;
            if (!overlayGo.activeSelf)
                overlayGo.SetActive(true);
        }
    }
}
