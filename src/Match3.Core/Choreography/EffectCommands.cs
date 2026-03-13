using System;
using System.Numerics;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Show a visual effect at a position.
/// </summary>
public sealed record ShowEffectCommand : RenderCommand
{
    /// <summary>Type of effect to show.</summary>
    public string EffectType { get; init; } = string.Empty;

    /// <summary>Position to show the effect.</summary>
    public Vector2 Position { get; init; }
}

/// <summary>
/// Show match highlight effect on matched tiles.
/// </summary>
public sealed record ShowMatchHighlightCommand : RenderCommand
{
    /// <summary>Positions of matched tiles.</summary>
    public Position[] Positions { get; init; } = Array.Empty<Position>();
}
