using System.Collections.Generic;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.View;
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
            new HorizontalRocketEffect(),
            new VerticalRocketEffect(),
            new SquareBombEffect(),
            new ColorBombEffect(),
            new UfoEffect()
        };
        return new BombEffectRegistry(effects);
    }
}
