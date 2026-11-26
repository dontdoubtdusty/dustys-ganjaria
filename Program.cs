using Raylib_cs;

namespace DustysGanjaria;

// Opens the window and runs the game loop around the shop.
static class Program
{
    public const string Version = "0.15.0";
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

    // Normal start opens the game on the saved day. Screenshot flags set up a scene, save one frame and quit (for
    // checking the layout): "--shot <file.png>" a mid-order weigh station, "--shot-throw <file.png>" a bag sealed and
    // thrown at the bin (ends as the hand-over's bag slides over), "--shot-counter <file.png>" the shop view with
    // a line and a waiting customer, "--shot-order <file.png>" the order shot half way through, "--shot-handover"
    // the end of a hand-over, "--shot-check" the customer's bag check half way, "--shot-results" and "--shot-tipjar" the two day-end screens,
    // "--shot-pack" and "--shot-pack-jar" a weighed order half closed on the pack station (pouch strip, jar lid),
    // "--shot-label" a packed jar on the label station with a label stuck on loosely, a little crooked, "--shot-intro"
    // the day card with a menu picked from all five strains, "--shot-pause" the pause menu over the shop view, "--shot-id" the order shot's ID check (an under-21 card).
    // A third argument "--tuning" shows the F1 tuning panel in the shot.
    // Screenshot runs make no sound, never read or write the save and never write the play log.
    static void Main(string[] args)
    {
        string[] shotModes =
        {
            "--shot", "--shot-throw", "--shot-counter", "--shot-order", "--shot-handover", "--shot-check", "--shot-results",
            "--shot-tipjar", "--shot-pack", "--shot-pack-jar", "--shot-label", "--shot-intro", "--shot-pause", "--shot-id",
        };
        string? mode = args.Length >= 2 && shotModes.Contains(args[0]) ? args[0] : null;
        string? shotPath = mode != null ? args[1] : null;

        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        Raylib.InitWindow(Width, Height, "Dusty's Ganjaria");
        SetWindowIcon();
        Raylib.SetTargetFPS(60);
        Art.LoadFont();
        // Esc opens the pause menu instead of closing the window (closing the window still quits).
        Raylib.SetExitKey(KeyboardKey.Null);
        // Screenshot runs load the sounds too (so a broken one shows up) but play them muted.
        Audio.Load();
        var settings = mode == null ? Settings.Load() : new Settings();
        Raylib.SetMasterVolume(mode == null ? settings.Volume : 0f);
        PlayLog.Enabled = mode == null;
        var pause = new PauseMenu(settings);

        var shop = mode == null
            ? new Shop(new Random(Environment.TickCount), Progress.Load(), persistent: true)
            : new Shop(new Random(7), new Progress(), persistent: false);
        if (mode == "--shot-pause") { shop.StageForScreenshot("--shot-counter"); pause.Open(true); }
        else if (mode != null) shop.StageForScreenshot(mode);
        if (mode != null && args.Contains("--tuning")) shop.Tuning = true;
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

            // While paused the shop does not update at all: clock, patience and physics all stand still.
            if (pause.IsOpen)
            {
                var choice = pause.Update();
                if (choice == PauseMenu.Choice.Restart) shop.RestartDay();
                if (choice == PauseMenu.Choice.Quit) break;
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                shop.Pause();
                pause.Open(shop.DayInProgress);
            }
            else
            {
                shop.Update(Raylib.GetFrameTime());
                if (shop.QuitRequested) break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Art.Background);
            shop.Draw();
            pause.Draw();
            Art.Label($"Dusty's Ganjaria {Version}", 12, Height - 26, 18, Art.Faint);
            Raylib.EndDrawing();
        }

        Art.UnloadFont();
        Audio.Unload();
        Raylib.CloseWindow();
    }
}
