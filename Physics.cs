using System.Numerics;

namespace DustysGanjaria;

// A fixed wall the buds bump into: a straight line from A to B.
readonly record struct Segment(Vector2 A, Vector2 B);

// Small 2D physics for buds: gravity, buds pushing each other, and fixed walls.
// The held bud follows the mouse and counts as immovable, so it shoves the others.
sealed class Physics
{
    public const float Gravity = 1800f;
    const int Substeps = 6;
    const int Iterations = 2;
    const float Restitution = 0.1f;
    const float WallFriction = 0.9f;
    const float MaxSpeed = 2200f;

    // Advances every loose bud by dt seconds, in small steps so fast buds do not pass through walls.
    public void Step(List<Bud> buds, List<Segment> walls, Bud? held, float dt)
    {
        float h = dt / Substeps;
        for (int s = 0; s < Substeps; s++)
        {
            foreach (var b in buds)
            {
                if (b == held || b.Sealed) continue;
                b.Vel.Y += Gravity * h;
                if (b.Vel.LengthSquared() > MaxSpeed * MaxSpeed)
                    b.Vel = Vector2.Normalize(b.Vel) * MaxSpeed;
                b.Pos += b.Vel * h;
                // Spin with sideways motion so buds look like they roll and tumble.
                b.Angle += b.Vel.X * h / b.Radius;
            }

            for (int it = 0; it < Iterations; it++)
            {
                for (int i = 0; i < buds.Count; i++)
                    for (int j = i + 1; j < buds.Count; j++)
                        Collide(buds[i], buds[j], held);

                foreach (var b in buds)
                {
                    if (b == held || b.Sealed) continue;
                    foreach (var w in walls)
                        CollideWall(b, w);
                }
            }
        }
    }

    const float BoxBounce = 0.25f;
    const float BoxFriction = 0.85f;

    // Moves a thrown box (the sealed bag) by dt seconds: gravity, then bump off the walls.
    // Walls here are all straight up-down or side-to-side lines, so a simple box test is enough.
    public static void StepBox(ref Vector2 pos, ref Vector2 vel, Vector2 size, List<Segment> walls, float dt)
    {
        float h = dt / Substeps;
        for (int s = 0; s < Substeps; s++)
        {
            vel.Y += Gravity * h;
            if (vel.LengthSquared() > MaxSpeed * MaxSpeed)
                vel = Vector2.Normalize(vel) * MaxSpeed;
            pos += vel * h;
            foreach (var w in walls)
                CollideBox(ref pos, ref vel, size, w);
        }
    }

    // Pushes a box out of a wall the short way and bounces it a little.
    static void CollideBox(ref Vector2 pos, ref Vector2 vel, Vector2 size, Segment w)
    {
        float sl = MathF.Min(w.A.X, w.B.X), sr = MathF.Max(w.A.X, w.B.X);
        float st = MathF.Min(w.A.Y, w.B.Y), sb = MathF.Max(w.A.Y, w.B.Y);
        float bl = pos.X, br = pos.X + size.X, bt = pos.Y, bb = pos.Y + size.Y;
        if (br <= sl || bl >= sr || bb <= st || bt >= sb) return;

        float pushLeft = br - sl, pushRight = sr - bl, pushUp = bb - st, pushDown = sb - bt;
        if (MathF.Min(pushUp, pushDown) <= MathF.Min(pushLeft, pushRight))
        {
            if (pushUp < pushDown)
            {
                pos.Y -= pushUp;
                if (vel.Y > 0) { vel.Y = -vel.Y * BoxBounce; vel.X *= BoxFriction; }
            }
            else
            {
                pos.Y += pushDown;
                if (vel.Y < 0) vel.Y = -vel.Y * BoxBounce;
            }
        }
        else if (pushLeft < pushRight)
        {
            pos.X -= pushLeft;
            if (vel.X > 0) vel.X = -vel.X * BoxBounce;
        }
        else
        {
            pos.X += pushRight;
            if (vel.X < 0) vel.X = -vel.X * BoxBounce;
        }
    }

    // Pushes two overlapping buds apart and bounces them off each other.
    static void Collide(Bud a, Bud b, Bud? held)
    {
        if (a.Sealed || b.Sealed) return;
        float ia = a == held ? 0f : a.InvMass;
        float ib = b == held ? 0f : b.InvMass;
        if (ia + ib == 0f) return;

        var d = b.Pos - a.Pos;
        float min = a.Radius + b.Radius;
        float distSq = d.LengthSquared();
        if (distSq >= min * min) return;

        float dist = MathF.Sqrt(distSq);
        var n = dist > 1e-4f ? d / dist : new Vector2(0, -1);
        float push = (min - dist) / (ia + ib);
        a.Pos -= n * push * ia;
        b.Pos += n * push * ib;

        float vn = Vector2.Dot(b.Vel - a.Vel, n);
        if (vn < 0)
        {
            float j = -(1 + Restitution) * vn / (ia + ib);
            a.Vel -= n * j * ia;
            b.Vel += n * j * ib;
        }
    }

    // Pushes a bud out of a wall, kills its speed into the wall and adds a little friction along it.
    static void CollideWall(Bud b, Segment w)
    {
        var ab = w.B - w.A;
        float t = Math.Clamp(Vector2.Dot(b.Pos - w.A, ab) / ab.LengthSquared(), 0f, 1f);
        var closest = w.A + ab * t;
        var d = b.Pos - closest;
        float distSq = d.LengthSquared();
        if (distSq >= b.Radius * b.Radius) return;

        float dist = MathF.Sqrt(distSq);
        var n = dist > 1e-4f ? d / dist : Vector2.Normalize(new Vector2(-ab.Y, ab.X));
        b.Pos = closest + n * b.Radius;

        float vn = Vector2.Dot(b.Vel, n);
        if (vn < 0)
        {
            var tangent = b.Vel - n * vn;
            b.Vel = -n * vn * Restitution + tangent * WallFriction;
        }
    }
}
