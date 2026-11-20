using Raylib_cs;

namespace DustysGanjaria;

// Opens the window and runs the game loop around the shop.
static class Program
{
    public const string Version = "0.6.0";
    public const int Width = 1280;
    public const int Height = 720;

    // Gives the window and its taskbar button the game icon (the .exe icon alone does not reach a Raylib window).
    static void SetWindowIcon()
    {
        const string path = "resources/icon.png";
        if (!File.Exists(path)) return;
        var icon = Raylib.LoadImage(path);
        Raylib.SetWindowIcon(icon);
        Raylib.UnloadImage(icon);
    }

    // Normal start opens the game. Screenshot flags set up a scene, save one frame and quit (for checking the layout):
    // "--shot <file.png>" a mid-order weigh station, "--shot-throw <file.png>" a bag sealed and thrown at the bin
    // (should end on the result panel), "--shot-counter <file.png>" the shop view with a line and a waiting customer,
    // "--shot-order <file.png>" the order shot half way through. Screenshot runs make no sound.
    static void Main(string[] args)
    {
        string[] shotModes = { "--shot", "--shot-throw", "--shot-counter", "--shot-order" };
        string? mode = args.Length == 2 && shotModes.Contains(args[0]) ? args[0] : null;
        string? shotPath = mode != null ? args[1] : null;

        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        Raylib.InitWindow(Width, Height, "Dusty's Ganjaria");
        SetWindowIcon();
        Raylib.SetTargetFPS(60);
        Art.LoadFont();
        // Screenshot runs load the sounds too (so a broken one shows up) but play them muted.
        Audio.Load();
        if (mode != null) Raylib.SetMasterVolume(0f);

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
        Audio.Unload();
        Raylib.CloseWindow();
    }
}
