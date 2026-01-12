using System.Collections.Generic;
using Match3.Core.Interfaces;
using Match3.Core.Models.Enums;
using Match3.Core.Systems.PowerUps.Effects;

namespace Match3.Core.Systems.PowerUps;

public class BombEffectRegistry
{
    private readonly Dictionary<BombType, IBombEffect> _effects = new();

    public BombEffectRegistry(IEnumerable<IBombEffect> effects)
    {
        foreach (var effect in effects)
        {
            Register(effect);
        }
    }

    public void Register(IBombEffect effect)
    {
        _effects[effect.Type] = effect;
    }

    public bool TryGetEffect(BombType type, out IBombEffect? effect)
    {
        return _effects.TryGetValue(type, out effect);
    }

    public static BombEffectRegistry CreateDefault()
    {
        var effects = new List<IBombEffect>
        {
            new AreaBombEffect(), // Added Area bomb
            new HorizontalRocketEffect(),
            new VerticalRocketEffect(),
            new SquareBombEffect(), // Was Square5x5
            new ColorBombEffect(),
            new UfoEffect()
        };
        return new BombEffectRegistry(effects);
    }
}
