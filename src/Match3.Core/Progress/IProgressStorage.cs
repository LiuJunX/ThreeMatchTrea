namespace Match3.Core.Progress;

/// <summary>
/// Persistence interface for player progress.
/// </summary>
public interface IProgressStorage
{
    PlayerProgress Load();
    void Save(PlayerProgress progress);
}
