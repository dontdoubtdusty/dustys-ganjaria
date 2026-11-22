using System.Text.Json;

namespace DustysGanjaria;

// What carries over from day to day: which day is next, the tips saved up, and rank points. Saved as JSON in
// %APPDATA%\DustysGanjaria\save.json at the end of every day. Delete that file to start over from day 1.
sealed class Progress
{
    public int Day { get; set; } = 1;
    public int TipsCents { get; set; }
    public int RankPoints { get; set; }

    // Rank names, lowest first. Rank 1 is where everyone starts.
    static readonly string[] RankNames =
    {
        "Fresh Hire", "Shelf Stocker", "Scale Hand", "Budtender", "Senior Budtender",
        "Shift Lead", "Floor Manager", "Connoisseur", "Head Budtender", "Shop Legend",
    };

    // Points needed to reach rank r (rank 1 needs 0). Each step costs 150 more than the last: 250, 400, 550...
    public static int PointsFor(int rank) => rank <= 1 ? 0 : (rank - 1) * 250 + 75 * (rank - 1) * (rank - 2);

    // The rank a number of points reaches.
    public static int RankOf(int points)
    {
        int r = 1;
        while (PointsFor(r + 1) <= points) r++;
        return r;
    }

    // A rank's name; past the end of the list it is the last name with a number.
    public static string RankName(int rank) =>
        rank <= RankNames.Length ? RankNames[rank - 1] : $"{RankNames[^1]} {rank - RankNames.Length + 1}";

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DustysGanjaria", "save.json");

    // Loads the save, or a fresh start if there is none or it cannot be read.
    public static Progress Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Progress>(File.ReadAllText(FilePath)) ?? new Progress();
        }
        catch (Exception) { }
        return new Progress();
    }

    // Writes the save. A failure is ignored: the game goes on, it just will not remember this day.
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception) { }
    }
}
