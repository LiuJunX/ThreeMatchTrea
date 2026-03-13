using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Systems.PowerUps.Effects;

namespace Match3.Core.Systems.PowerUps;

/// <summary>
/// Registry mapping bomb types to their area-of-effect behaviors.
/// <para>To add a new bomb effect:</para>
/// <list type="number">
/// <item>Create a class implementing <see cref="IBombEffect"/></item>
/// <item>Add it to <see cref="CreateDefault"/></item>
/// <item>Register the <see cref="ElementType"/> in the Models layer</item>
/// </list>
/// </summary>
public class BombEffectRegistry
{
    private readonly Dictionary<ElementType, IBombEffect> _effects = new();

    /// <summary>
    /// Initializes a new registry with the given bomb effects.
    /// </summary>
    /// <param name="effects">The bomb effects to register.</param>
    public BombEffectRegistry(IEnumerable<IBombEffect> effects)
    {
        foreach (var effect in effects)
        {
            Register(effect);
        }
    }

    /// <summary>
    /// Registers a bomb effect, replacing any existing effect for the same <see cref="ElementType"/>.
    /// </summary>
    /// <param name="effect">The bomb effect to register.</param>
    public void Register(IBombEffect effect)
    {
        _effects[effect.Type] = effect;
    }

    /// <summary>
    /// Attempts to retrieve the bomb effect for the given element type.
    /// </summary>
    /// <param name="type">The element type to look up.</param>
    /// <param name="effect">The matching bomb effect, or <c>null</c> if not found.</param>
    /// <returns><c>true</c> if an effect was found; otherwise <c>false</c>.</returns>
    public bool TryGetEffect(ElementType type, out IBombEffect? effect)
    {
        return _effects.TryGetValue(type, out effect);
    }

    /// <summary>
    /// Creates a registry pre-populated with the standard bomb effects
    /// (horizontal/vertical rockets, square bomb, color bomb, UFO).
    /// </summary>
    /// <returns>A new <see cref="BombEffectRegistry"/> with default effects.</returns>
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
