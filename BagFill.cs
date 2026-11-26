using System.Numerics;

namespace DustysGanjaria;

// The nugs sealed in a bag, kept exactly as they lay when the zip closed, so Pack, Label and the hand-over draw the
// same pile the player made. Size is the sealed bag's size; each Offset is a nug's centre from the bag's top-left
// corner at that size. The buds are the weigh station's own (it lets go of them when it clears, and never changes
// them once sealed).
sealed class BagFill
{
    public Vector2 Size { get; }
    public IReadOnlyList<(Bud Bud, Vector2 Offset)> Nugs { get; }

    public BagFill(Vector2 size, IReadOnlyList<(Bud Bud, Vector2 Offset)> nugs)
    {
        Size = size;
        Nugs = nugs;
    }
}
