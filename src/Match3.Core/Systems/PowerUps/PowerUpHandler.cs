using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.PowerUps.Effects;
using Match3.Core.Utility.Pools;

namespace Match3.Core.Systems.PowerUps;

public class PowerUpHandler : IPowerUpHandler
{
    private readonly IScoreSystem _scoreSystem;
    private readonly BombComboHandler _comboHandler;
    private readonly BombEffectRegistry _effectRegistry;

    public PowerUpHandler(IScoreSystem scoreSystem)
        : this(scoreSystem, new BombComboHandler(), BombEffectRegistry.CreateDefault())
    {
    }

    public PowerUpHandler(IScoreSystem scoreSystem, BombComboHandler comboHandler, BombEffectRegistry effectRegistry)
    {
        _scoreSystem = scoreSystem;
        _comboHandler = comboHandler;
        _effectRegistry = effectRegistry;
    }

    public void ProcessSpecialMove(ref GameState state, Position p1, Position p2, out int points)
    {
        points = 0;
        var t1 = state.GetTile(p1.X, p1.Y);
        var t2 = state.GetTile(p2.X, p2.Y);

        // Calculate score before modifying state (tiles might be cleared)
        points = _scoreSystem.CalculateSpecialMoveScore(t1.Type, t1.Bomb, t2.Type, t2.Bomb);

        // 使用 BombComboHandler 处理组合
        var affected = Pools.ObtainHashSet<Position>();
        try
        {
            if (_comboHandler.TryApplyCombo(ref state, p1, p2, affected))
            {
                // 清除所有受影响的方块
                ClearAffectedTiles(ref state, affected);
                return;
            }
        }
        finally
        {
            Pools.Release(affected);
        }

        // If no special move happened, reset points
        points = 0;
    }

    public void ActivateBomb(ref GameState state, Position p)
    {
        var t = state.GetTile(p.X, p.Y);
        if (t.Bomb == BombType.None) return;

        var affected = Pools.ObtainHashSet<Position>();
        try
        {
            // 使用 BombEffectRegistry 获取单个炸弹效果
            if (_effectRegistry.TryGetEffect(t.Bomb, out var effect))
            {
                effect!.Apply(in state, p, affected);
                ClearAffectedTiles(ref state, affected);
            }

            // 确保炸弹本身被清除
            var currentT = state.GetTile(p.X, p.Y);
            if (currentT.Type != TileType.None)
            {
                state.SetTile(p.X, p.Y, new Tile(0, TileType.None, p.X, p.Y));
            }
        }
        finally
        {
            Pools.Release(affected);
        }
    }

    /// <summary>
    /// 清除所有受影响的方块（支持递归连锁爆炸）
    /// </summary>
    private void ClearAffectedTiles(ref GameState state, HashSet<Position> affected)
    {
        foreach (var pos in affected)
        {
            ClearTileWithChain(ref state, pos);
        }
    }

    /// <summary>
    /// 清除单个方块，如果是炸弹则触发连锁爆炸
    /// </summary>
    private void ClearTileWithChain(ref GameState state, Position pos)
    {
        if (pos.X < 0 || pos.X >= state.Width || pos.Y < 0 || pos.Y >= state.Height)
            return;

        var tile = state.GetTile(pos.X, pos.Y);

        // 已经是空的，跳过
        if (tile.Type == TileType.None)
            return;

        // 如果是炸弹，触发连锁爆炸
        if (tile.Bomb != BombType.None)
        {
            // 先清除炸弹本身（防止重复触发）
            state.SetTile(pos.X, pos.Y, new Tile(0, TileType.None, pos.X, pos.Y));

            // 触发炸弹效果
            if (_effectRegistry.TryGetEffect(tile.Bomb, out var effect))
            {
                var chainAffected = Pools.ObtainHashSet<Position>();
                try
                {
                    effect!.Apply(in state, pos, chainAffected);
                    // 递归清除连锁受影响的方块
                    foreach (var chainPos in chainAffected)
                    {
                        ClearTileWithChain(ref state, chainPos);
                    }
                }
                finally
                {
                    Pools.Release(chainAffected);
                }
            }
        }
        else
        {
            // 普通方块，直接清除
            state.SetTile(pos.X, pos.Y, new Tile(0, TileType.None, pos.X, pos.Y));
        }
    }
}
