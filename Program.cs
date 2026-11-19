using Raylib_cs;

namespace DustysGanjaria;

// Opens the window and runs the game loop around the shop.
static class Program
{
    public const string Version = "0.4.0";
    public const int Width = 1280;
    public const int Height = 720;

    // Normal start opens the game. Screenshot flags set up a scene, save one frame and quit (for checking the layout):
    // "--shot <file.png>" a mid-order weigh station, "--shot-throw <file.png>" a bag sealed and thrown at the bin
    // (should end on the result panel), "--shot-counter <file.png>" the counter with a line and a waiting customer.
    static void Main(string[] args)
    {
        string[] shotModes = { "--shot", "--shot-throw", "--shot-counter" };
        string? mode = args.Length == 2 && shotModes.Contains(args[0]) ? args[0] : null;
        string? shotPath = mode != null ? args[1] : null;

        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        Raylib.InitWindow(Width, Height, "Dusty's Ganjaria");
        Raylib.SetTargetFPS(60);
        Art.LoadFont();

        var shop = new Shop(new Random(mode == null ? Environment.TickCount : 7));
        if (mode != null) shop.StageForScreenshot(mode);
        int frames = 0;

        while (!Raylib.WindowShouldClose())
        {
            if (shotPath != null && ++frames == (mode == "--shot-throw" ? 180 : 60))
            {
                var image = Raylib.LoadImageFromScreen();
                Raylib.ExportImage(image, shotPath);
                Raylib.UnloadImage(image);
                break;
            }

            shop.Update(Raylib.GetFrameTime());

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Art.Background);
            shop.Draw();
            Art.Label($"Dusty's Ganjaria {Version}", 12, Height - 26, 18, Art.Faint);
            Raylib.EndDrawing();
        }

        Art.UnloadFont();
        Raylib.CloseWindow();
    }
}
