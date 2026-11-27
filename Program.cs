using Raylib_cs;
using Raykit;

namespace DustysGanjaria;

// Opens the window and runs the game loop around the shop.
static class Program
{
    public const string Version = "0.21.1";
    public const int Width = 1280;
    public const int Height = 720;

    // Runs the game inside Raykit's crash net: a crash is saved to crash.txt next to the exe and shown in a message box.
    static void Main(string[] args) => App.Run("Dusty's Ganjaria", Version, () => RunGame(args));

    // Normal start opens the game on the saved day. Screenshot flags set up a scene, save one frame and quit (for
    // checking the layout): "--shot <file.png>" a mid-order weigh station (half the order in the bag, half on the tray), "--shot-pour" the tray tipping into the bag,
    // "--shot-throw <file.png>" a bag sealed and
    // thrown at the bin (ends as the hand-over's bag slides over), "--shot-counter <file.png>" the shop view with
    // a line and a waiting customer, "--shot-order <file.png>" the order shot at its last line (a no-shake request), "--shot-handover"
    // the end of a hand-over, "--shot-check" the customer's bag check half way, "--shot-results" and "--shot-tipjar" the two day-end screens,
    // "--shot-pack" and "--shot-pack-jar" a weighed order half closed on the pack station (sealer heating, lid notch dial),
    // "--shot-drop" the bag sliding on the pack station's rail over the open container,
    // "--shot-label" a packed jar on the label station with its peeled label sliding toward the guide marks,
    // "--shot-peel" its backing half pulled off, "--shot-slap" the label hitting the jar, "--shot-smooth" its bubbles being rubbed out, "--shot-intro"
    // the day card with a menu picked from all five strains, "--shot-pause" the pause menu over the shop view, "--shot-id" the order shot's ID check (an under-21 card).
    // A third argument "--tuning" shows the F1 tuning panel in the shot.
    // Screenshot runs make no sound, never read or write the save and never write the play log.
    static void RunGame(string[] args)
    {
        string[] shotModes =
        {
            "--shot", "--shot-throw", "--shot-counter", "--shot-order", "--shot-handover", "--shot-check", "--shot-results",
            "--shot-tipjar", "--shot-pack", "--shot-pack-jar", "--shot-label", "--shot-intro", "--shot-pause", "--shot-id",
            "--shot-pour", "--shot-drop", "--shot-peel", "--shot-slap", "--shot-smooth",
        };
        string? mode = args.Length >= 2 && shotModes.Contains(args[0]) ? args[0] : null;
        string? shotPath = mode != null ? args[1] : null;

        // A fixed 1280 x 720 window (F11 for full screen) with the game icon. Raykit leaves Esc to the pause menu.
        AppWindow.Open(new WindowOptions
        {
            Title = "Dusty's Ganjaria", Width = Width, Height = Height, Resizable = false, IconPath = Paths.Resource("icon.png"),
        });
        Art.LoadFont();
        Art.LoadSprites();
        // Screenshot runs load the sounds too (so a broken one shows up) but play them muted.
        Audio.Load();
        var settings = mode == null ? Settings.Load() : new Settings();
        Raylib.SetMasterVolume(mode == null ? settings.Volume : 0f);
        PlayLog.Enabled = mode == null;
        if (mode == null && settings.Fullscreen) AppWindow.SetFullscreen(true);
        var pause = new PauseMenu(settings);

        // "--body <character>" makes every customer that cast member (shots only).
        int bodyArg = Array.IndexOf(args, "--body");
        if (mode != null && bodyArg >= 0 && bodyArg + 1 < args.Length) Art.ForceBody = args[bodyArg + 1];
        var shop = mode == null
            ? new Shop(new Random(Environment.TickCount), Progress.Load(), persistent: true)
            : new Shop(new Random(7), new Progress(), persistent: false);
        if (mode == "--shot-pause") { shop.StageForScreenshot("--shot-counter"); pause.Open(true); }
        else if (mode != null) shop.StageForScreenshot(mode);
        if (mode != null && args.Contains("--tuning")) shop.Tuning = true;
        int frames = 0;

        while (!Raylib.WindowShouldClose())
        {
            if (mode == null && View.TogglePressed()) View.ToggleFullscreen(settings);
            View.Update();

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

            // The game draws in 1280 x 720 units; the camera scales them to the window, with black bars around.
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.BeginMode2D(View.Camera);
            Raylib.DrawRectangle(0, 0, Width, Height, Art.Background);
            shop.Draw();
            pause.Draw();
            Art.Label($"Dusty's Ganjaria {Version}", 12, Height - 26, 18, Art.Faint);
            Raylib.EndMode2D();
            View.DrawBars();

            // Screenshot runs save frame 59 (179 for the throw) before it is shown, then quit.
            bool shotNow = shotPath != null && ++frames == (mode == "--shot-throw" ? 179 : 59);
            if (shotNow) Shot.Save(shotPath!);
            Raylib.EndDrawing();
            if (shotNow) break;
        }

        Art.UnloadFont();
        Art.UnloadSprites();
        Audio.Unload();
        Raylib.CloseWindow();
    }
}
