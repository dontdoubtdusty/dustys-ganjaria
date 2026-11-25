using System.Numerics;
using Raylib_cs;

namespace DustysGanjaria;

// The pause menu, opened with Esc: the game freezes behind it. Resume (or Esc again) carries on; the volume slider
// sets and saves the master volume; Restart day and Quit ask first while a day is being played, since the day is
// not saved until it ends.
sealed class PauseMenu
{
    public enum Choice { None, Resume, Restart, Quit }
    enum View { Closed, Main, ConfirmRestart, ConfirmQuit }

    static readonly Rectangle Panel = new(440, 120, 400, 490);
    static readonly Rectangle VolumeTrack = new(480, 290, 320, 14);
    static readonly Rectangle ResumeButton = new(520, 345, 240, 56);
    static readonly Rectangle RestartButton = new(520, 425, 240, 56);
    static readonly Rectangle QuitButton = new(520, 505, 240, 56);
    // The confirm view's two buttons.
    static readonly Rectangle YesButton = new(520, 360, 240, 56);
    static readonly Rectangle NoButton = new(520, 440, 240, 56);

    readonly Settings settings;
    View view = View.Closed;
    bool dayInProgress, draggingVolume;

    public PauseMenu(Settings settings) => this.settings = settings;

    public bool IsOpen => view != View.Closed;

    // Opens the menu. dayInProgress says whether restarting or quitting would lose a day (and so needs a confirm).
    public void Open(bool dayInProgress)
    {
        this.dayInProgress = dayInProgress;
        view = View.Main;
        draggingVolume = false;
    }

    // Handles one frame of input and returns what the player chose. Esc backs out of a confirm, or resumes.
    public Choice Update()
    {
        var mouse = Raylib.GetMousePosition();
        bool press = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool Clicked(Rectangle r) => press && Raylib.CheckCollisionPointRec(mouse, r);

        if (view is View.ConfirmRestart or View.ConfirmQuit)
        {
            bool restart = view == View.ConfirmRestart;
            if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(NoButton)) { view = View.Main; return Choice.None; }
            if (!Clicked(YesButton)) return Choice.None;
            view = View.Closed;
            return restart ? Choice.Restart : Choice.Quit;
        }

        UpdateVolume(mouse, press);
        if (draggingVolume) return Choice.None;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(ResumeButton)) { view = View.Closed; return Choice.Resume; }
        if (Clicked(RestartButton) && dayInProgress) view = View.ConfirmRestart;
        if (Clicked(QuitButton))
        {
            if (dayInProgress) view = View.ConfirmQuit;
            else { view = View.Closed; return Choice.Quit; }
        }
        return Choice.None;
    }

    // Drags the volume knob (a press anywhere on or near the track grabs it); the new level is saved on release,
    // with a tick so the player hears it.
    void UpdateVolume(Vector2 mouse, bool press)
    {
        var grab = new Rectangle(VolumeTrack.X - 14, VolumeTrack.Y - 14, VolumeTrack.Width + 28, VolumeTrack.Height + 28);
        if (press && Raylib.CheckCollisionPointRec(mouse, grab)) draggingVolume = true;
        if (!draggingVolume) return;
        settings.Volume = Math.Clamp((mouse.X - VolumeTrack.X) / VolumeTrack.Width, 0f, 1f);
        Raylib.SetMasterVolume(settings.Volume);
        if (Raylib.IsMouseButtonDown(MouseButton.Left)) return;
        draggingVolume = false;
        settings.Save();
        Audio.Play(Audio.Tick);
    }

    // Draws the menu over the frozen game.
    public void Draw()
    {
        if (view == View.Closed) return;
        var mouse = Raylib.GetMousePosition();
        bool Hover(Rectangle r) => Raylib.CheckCollisionPointRec(mouse, r);

        if (view is View.ConfirmRestart or View.ConfirmQuit)
        {
            bool restart = view == View.ConfirmRestart;
            Art.DrawPausePanel(Panel, restart ? "Restart day?" : "Quit?", "Today's orders and tips will be lost.");
            Art.DrawButton(YesButton, restart ? "Restart" : "Quit", Hover(YesButton), true);
            Art.DrawButton(NoButton, "Back", Hover(NoButton), true);
            return;
        }

        Art.DrawPausePanel(Panel, "Paused", dayInProgress ? "The shop waits while you are away." : "");
        var grab = new Rectangle(VolumeTrack.X - 14, VolumeTrack.Y - 14, VolumeTrack.Width + 28, VolumeTrack.Height + 28);
        Art.DrawSlider(VolumeTrack, "Volume", settings.Volume, draggingVolume || Hover(grab));
        Art.DrawButton(ResumeButton, "Resume", Hover(ResumeButton), true);
        Art.DrawButton(RestartButton, "Restart day", dayInProgress && Hover(RestartButton), dayInProgress);
        Art.DrawButton(QuitButton, "Quit", Hover(QuitButton), true);
    }
}
