using Match3.Core.Models.Enums;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// 纯 View 匀速掉落测试。
    /// 不经过任何逻辑层 / VisualState / 插值 —— 直接在 Update 里用 deltaTime 驱动位置。
    /// 用于对比：卡顿感是否来自逻辑-显示分离。
    ///
    /// 自动挂载：GameController 初始化后自动在棋盘右侧生成一列持续掉落的 tile。
    /// </summary>
    public sealed class FallingTestView : MonoBehaviour
    {
        private struct FallingTile
        {
            public GameObject Go;
            public float Y;
        }

        private FallingTile[] _tiles;
        private Match3Bridge _bridge;
        private float _cellSize;
        private float _originX;
        private float _originY;
        private int _rowCount;

        private const float FallSpeedCells = 4.0f; // cells per second
        private const float TileScaleMultiplier = 1.05f;
        private static readonly Quaternion BaseTilt = Quaternion.Euler(-10f, 0f, 0f);
        private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropFallback = Shader.PropertyToID("_Color");

        private static readonly ElementType[] TileTypes =
        {
            ElementType.Item1, ElementType.Item2, ElementType.Item3,
            ElementType.Item4, ElementType.Item5,
        };

        /// <summary>
        /// 由 GameController 在初始化后调用。
        /// </summary>
        public void Setup(Match3Bridge bridge)
        {
            _bridge = bridge;
            _cellSize = bridge.CellSize;
            _rowCount = bridge.Height;

            // 放在棋盘最右列位置（叠在棋盘上），直接跟左边列的逻辑掉落对比
            _originX = bridge.BoardOrigin.x + (bridge.Width - 1) * _cellSize;
            _originY = bridge.BoardOrigin.y + 5 * _cellSize; // 整体上移5行

            SpawnColumn();
        }

        private void Update()
        {
            if (_tiles == null || _bridge == null) return;

            float dt = _bridge.ScaledDeltaTime; // 受速度控制器控制
            float speed = FallSpeedCells * _cellSize;
            float totalHeight = (_rowCount + 2) * _cellSize;
            float bottomY = _originY - _cellSize;

            for (int i = 0; i < _tiles.Length; i++)
            {
                ref var tile = ref _tiles[i];
                tile.Y -= speed * dt;

                if (tile.Y < bottomY)
                    tile.Y += totalHeight;

                var pos = tile.Go.transform.localPosition;
                pos.y = tile.Y;
                tile.Go.transform.localPosition = pos;
            }
        }

        private void SpawnColumn()
        {
            var container = new GameObject("FallingTestColumn");
            container.transform.SetParent(transform, false);

            float scaleFactor = _cellSize * TileScaleMultiplier;
            _tiles = new FallingTile[_rowCount];

            for (int row = 0; row < _rowCount; row++)
            {
                var type = TileTypes[row % TileTypes.Length];

                var go = new GameObject($"FallTile_{row}");
                go.transform.SetParent(container.transform, false);

                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();

                mf.sharedMesh = MeshFactory.GetTileMesh(type);
                mr.sharedMaterials = MeshFactory.GetTileMaterialArray(type);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                var propBlock = new MaterialPropertyBlock();
                var color = MeshFactory.GetCeramicColor(type);
                propBlock.SetColor(ColorProp, color);
                propBlock.SetColor(ColorPropFallback, color);
                mr.SetPropertyBlock(propBlock);

                go.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
                go.transform.localRotation = BaseTilt;

                float worldY = _originY + (_rowCount - 1 - row) * _cellSize + _cellSize * 0.5f;
                go.transform.localPosition = new Vector3(_originX + _cellSize * 0.5f, worldY, -0.1f);

                _tiles[row] = new FallingTile { Go = go, Y = worldY };
            }

            Debug.Log($"[FallingTest] 纯View匀速掉落 — {_rowCount} tiles @ {FallSpeedCells} cells/s, 棋盘右侧");
        }

        private void OnDestroy()
        {
            var container = transform.Find("FallingTestColumn");
            if (container != null)
                Destroy(container.gameObject);
            _tiles = null;
        }
    }
}
