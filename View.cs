using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// Fits the game's fixed 1280 x 720 layout to the window: windowed at that size, or full screen (borderless, the
// monitor's size) scaled up evenly with black bars where the shapes differ. All game code keeps working in
// 1280 x 720 units: a Camera2D scales the drawing and Raylib's mouse scale maps the mouse back.
static class View
{
    public static float Scale { get; private set; } = 1f;
    public static Vector2 Offset { get; private set; }

    // Works out the scale and bars for the window's current size and points the mouse at game units. Call once a
    // frame before anything reads the mouse.
    public static void Update()
    {
        float w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        Scale = MathF.Min(w / Program.Width, h / Program.Height);
        Offset = new Vector2(MathF.Floor((w - Program.Width * Scale) / 2), MathF.Floor((h - Program.Height * Scale) / 2));
        Raylib.SetMouseOffset(-(int)Offset.X, -(int)Offset.Y);
        Raylib.SetMouseScale(1f / Scale, 1f / Scale);
    }

    // The camera that draws game units onto the window.
    public static Camera2D Camera => new() { Offset = Offset, Target = Vector2.Zero, Rotation = 0, Zoom = Scale };

    // Switches between windowed and full screen, and remembers the choice in the settings. Raykit's borderless
    // window covers the monitor without raylib's true fullscreen, which stays on top and makes alt-tab slow.
    public static void ToggleFullscreen(Settings settings)
    {
        Raykit.AppWindow.ToggleFullscreen();
        settings.Fullscreen = Raykit.AppWindow.IsFullscreen;
        settings.Save();
    }

    // True on the frame F11 is pressed. (Not Alt+Enter: Enter already takes orders.)
    public static bool TogglePressed() => Raylib.IsKeyPressed(KeyboardKey.F11);

    // Paints the bars around the game black, over anything the game drew past its own edges. Call after EndMode2D.
    public static void DrawBars()
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        int x0 = (int)Offset.X, y0 = (int)Offset.Y;
        int x1 = (int)MathF.Ceiling(Offset.X + Program.Width * Scale), y1 = (int)MathF.Ceiling(Offset.Y + Program.Height * Scale);
        Raylib.DrawRectangle(0, 0, w, y0, Color.Black);
        Raylib.DrawRectangle(0, y1, w, h - y1, Color.Black);
        Raylib.DrawRectangle(0, 0, x0, h, Color.Black);
        Raylib.DrawRectangle(x1, 0, w - x1, h, Color.Black);
    }

    // Starts a scissor (clip) rectangle given in game units.
    public static void BeginScissor(Rectangle r) =>
        Raylib.BeginScissorMode((int)(Offset.X + r.X * Scale), (int)(Offset.Y + r.Y * Scale),
                                (int)MathF.Ceiling(r.Width * Scale), (int)MathF.Ceiling(r.Height * Scale));
}
