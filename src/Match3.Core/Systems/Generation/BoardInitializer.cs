using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Obstacles;
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

            var gridSize = levelConfig.Width * levelConfig.Height;
            for (int i = 0; i < gridSize; i++)
            {
                int x = i % levelConfig.Width;
                int y = i / levelConfig.Width;

                if (x < state.Width && y < state.Height)
                {
                    // 1. Initialize Cell layer (Structure)
                    if (levelConfig.Cells != null && i < levelConfig.Cells.Length)
                    {
                        state.SetCell(x, y, levelConfig.Cells[i]);
                    }
                    else
                    {
                         // Fallback default
                         state.SetCell(x, y, CellKind.Slot);
                    }

                    // 2. Initialize Element layer (Content)
                    // Skip tile creation for Void/Wall cells
                    var cellKind = state.GetCell(x, y);
                    if (cellKind == CellKind.Void || cellKind == CellKind.Wall)
                        continue;

                    // Initialize Obstacle layer (before tile — obstacle occupies the cell)
                    if (levelConfig.Obstacles != null && i < levelConfig.Obstacles.Length)
                    {
                        var obstacleType = levelConfig.Obstacles[i];
                        if (obstacleType != ObstacleType.None)
                        {
                            byte stage = ObstacleRules.GetDefaultStage(obstacleType);
                            if (levelConfig.ObstacleStages != null && i < levelConfig.ObstacleStages.Length && levelConfig.ObstacleStages[i] > 0)
                            {
                                stage = (byte)levelConfig.ObstacleStages[i];
                            }
                            byte obstacleState = 0;
                            if (levelConfig.ObstacleStates != null && i < levelConfig.ObstacleStates.Length)
                            {
                                obstacleState = (byte)levelConfig.ObstacleStates[i];
                            }
                            // Fallback to default state when not configured (e.g., PotionBottle needs 0x0F)
                            if (obstacleState == 0)
                            {
                                obstacleState = ObstacleRules.GetDefaultState(obstacleType);
                            }
                            // PotionBottle: ensure Stage matches popcount(State) for bitmask consistency
                            if (obstacleType == ObstacleType.PotionBottle)
                            {
                                stage = 0;
                                for (uint bits = obstacleState; bits != 0; bits &= bits - 1)
                                    stage++;
                            }
                            state.SetObstacle(x, y, new Obstacle(obstacleType, stage, obstacleState));
                            continue; // Obstacle occupies the cell — no tile here
                        }
                    }

                    // ElementType.None in a CellKind.Slot means "Generate Random".
                    var type = (levelConfig.Grid != null && i < levelConfig.Grid.Length)
                        ? levelConfig.Grid[i]
                        : ElementType.None;

                    if (type == ElementType.None)
                    {
                        type = _tileGenerator.GenerateNonMatchingTile(ref state, x, y);
                    }

                    var tile = new Tile(state.NextTileId++, type, x, y);

                    // Apply custom tile stage from level config
                    if (levelConfig.TileStages != null && i < levelConfig.TileStages.Length && levelConfig.TileStages[i] > 0)
                    {
                        tile.Stage = levelConfig.TileStages[i];
                    }

                    state.SetTile(x, y, tile);

                    // Initialize Ground layer
                    if (levelConfig.Grounds != null && i < levelConfig.Grounds.Length)
                    {
                        var groundType = levelConfig.Grounds[i];
                        if (groundType != GroundType.None)
                        {
                            byte health = GroundRules.GetDefaultHealth(groundType);
                            if (levelConfig.GroundHealths != null && i < levelConfig.GroundHealths.Length && levelConfig.GroundHealths[i] > 0)
                            {
                                health = (byte)levelConfig.GroundHealths[i];
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
                                health = (byte)levelConfig.CoverHealths[i];
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
            // Default random initialization
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    // Default Cells are already Slots
                    var type = _tileGenerator.GenerateNonMatchingTile(ref state, x, y);
                    state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
                    // Ground and Cover layers are already initialized to empty in GameState constructor
                }
            }
        }
    }
}
