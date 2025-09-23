using System;
using System.Collections.Generic;

namespace Minecraftonia.MarkovJunior;

/// <summary>
/// Represents a symbol used by the MarkovJunior-inspired rule system.
/// Symbols carry tags so rules can reason about semantics (e.g. street, wall, canopy).
/// </summary>
public sealed class MarkovSymbol
{
    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);

    public MarkovSymbol(string id, int paletteIndex)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Symbol id cannot be empty.", nameof(id));
        }

        Id = id;
        PaletteIndex = paletteIndex;
    }

    public string Id { get; }
    public int PaletteIndex { get; }

    public IReadOnlyCollection<string> Tags => _tags;

    public MarkovSymbol WithTags(params string[] tags)
    {
        if (tags is null)
        {
            return this;
        }

        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                _tags.Add(tag.ToLowerInvariant());
            }
        }

        return this;
    }

    public bool HasTag(string tag) => _tags.Contains(tag);
}
