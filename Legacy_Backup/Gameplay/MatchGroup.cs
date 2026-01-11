using System.Collections.Generic;

using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Models.Gameplay;

public enum MatchShape
{
    Simple3,
    Line4Horizontal,
    Line4Vertical,
    Line5, // Can be L-shape 5, T-shape 5, or Straight 5. Usually Straight 5 = Color Bomb.
    Cross, // T or L shape
    Square // 2x2 or similar
}

public class MatchGroup
{
    public TileType Type;
    public HashSet<Position> Positions = new HashSet<Position>();
    public MatchShape Shape;
    public Position? BombOrigin; // Where to spawn the bomb (usually the swap position)
    public BombType SpawnBombType = BombType.None;
}
