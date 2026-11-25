using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The baggie: an open-topped box that sits on the scale, with a zip along the top.
sealed class Bag
{
    public Vector2 Size;         // shrinks when the bag is sealed and pressed flat
    public Vector2 Pos;          // top-left corner
    public Vector2 Vel;          // only used once sealed, when the bag can be thrown
    public bool Placed;          // on the scale
    public bool Sealed;
    public float Zip;            // 0 = open, 1 = fully zipped

    // Sealing presses the bag flat to this share of its size (see WeighStation).
    public const float SealedScale = 0.6f;

    public Bag(Vector2 size) => Size = size;

    // An empty bag's size for an order: bigger orders get bigger bags.
    public static Vector2 SizeFor(float grams) => grams switch
    {
        <= 1f => new Vector2(120, 120),
        <= 3.5f => new Vector2(165, 160),
        _ => new Vector2(215, 200),
    };

    public Rectangle Rect => new(Pos.X, Pos.Y, Size.X, Size.Y);
    public Vector2 Center => Pos + Size / 2;
    public float ZipY => Pos.Y;

    // Where the zip tab is now.
    public Vector2 ZipPoint => new(Pos.X + Size.X * Zip, Pos.Y);

    // True if a point is inside the bag (a little slack at the mouth).
    public bool Contains(Vector2 p) =>
        p.X > Pos.X && p.X < Pos.X + Size.X && p.Y > Pos.Y - 4 && p.Y < Pos.Y + Size.Y;

    // The bag's sides and bottom, as physics walls.
    public IEnumerable<Segment> Walls
    {
        get
        {
            var bottomLeft = new Vector2(Pos.X, Pos.Y + Size.Y);
            var bottomRight = Pos + Size;
            yield return new Segment(Pos, bottomLeft);
            yield return new Segment(bottomLeft, bottomRight);
            yield return new Segment(new Vector2(bottomRight.X, Pos.Y), bottomRight);
        }
    }
}
