using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Objectives;

namespace Match3.Core.Systems.Generation;

public class BoardInitializer : IBoardInitializer
{
    private readonly ITileGenerator _tileGenerator;
    private readonly ILevelObjectiveSystem? _objectiveSystem;

    public BoardInitializer(ITileGenerator tileGenerator, ILevelObjectiveSystem? objectiveSystem = null)
    {
        _tileGenerator = tileGenerator;
        _objectiveSystem = objectiveSystem;
    }

    public void Initialize(ref GameState state, LevelConfig? levelConfig)
    {
        if (levelConfig != null)
        {
            // Initialize difficulty settings from level config
            state.MoveLimit = levelConfig.MoveLimit;
            state.TargetDifficulty = levelConfig.TargetDifficulty;

            // Initialize objectives
            _objectiveSystem?.Initialize(ref state, levelConfig);

            for (int i = 0; i < levelConfig.Grid.Length; i++)
            {
                int x = i % levelConfig.Width;
                int y = i / levelConfig.Width;

                if (x < state.Width && y < state.Height)
                {
                    // Initialize Tile layer
                    var type = levelConfig.Grid[i];
                    var bomb = BombType.None;
                    if (levelConfig.Bombs != null && i < levelConfig.Bombs.Length)
                    {
                        bomb = levelConfig.Bombs[i];
                    }
                    state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y, bomb));

                    // Initialize Ground layer
                    if (levelConfig.Grounds != null && i < levelConfig.Grounds.Length)
                    {
                        var groundType = levelConfig.Grounds[i];
                        if (groundType != GroundType.None)
                        {
                            byte health = GroundRules.GetDefaultHealth(groundType);
                            if (levelConfig.GroundHealths != null && i < levelConfig.GroundHealths.Length && levelConfig.GroundHealths[i] > 0)
                            {
                                health = levelConfig.GroundHealths[i];
                            }
                            state.SetGround(x, y, new Ground(groundType, health));
                        }
                    }

                    // Initialize Cover layer
                    if (levelConfig.Covers != null && i < levelConfig.Covers.Length)
                    {
                        var coverType = levelConfig.Covers[i];
                        if (coverType != CoverType.None)
                        {
                            byte health = CoverRules.GetDefaultHealth(coverType);
                            if (levelConfig.CoverHealths != null && i < levelConfig.CoverHealths.Length && levelConfig.CoverHealths[i] > 0)
                            {
                                health = levelConfig.CoverHealths[i];
                            }
                            bool isDynamic = CoverRules.IsDynamicType(coverType);
                            state.SetCover(x, y, new Cover(coverType, health, isDynamic));
                        }
                    }
                }
            }
        }
        else
        {
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var type = _tileGenerator.GenerateNonMatchingTile(ref state, x, y);
                    state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
                    // Ground and Cover layers are already initialized to empty in GameState constructor
                }
            }
        }
    }
}
