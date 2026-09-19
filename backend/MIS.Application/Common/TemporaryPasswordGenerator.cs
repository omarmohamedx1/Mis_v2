namespace MIS.Application.Common;

public static class TemporaryPasswordGenerator
{
    private static readonly string[] Words =
    [
        "Nile", "Cairo", "Cedar", "Falcon", "Amber", "Oasis", "Pearl", "Maple",
        "Quartz", "Lotus", "Copper", "Orchid", "Velvet", "Canyon", "Meadow",
        "Coral", "Dune", "Harbor", "Pine", "Atlas"
    ];

    public static string Create()
    {
        var first = Words[Random.Shared.Next(Words.Length)];
        string second;
        do
        {
            second = Words[Random.Shared.Next(Words.Length)];
        } while (second == first);

        return $"{first}-{second}-{Random.Shared.Next(10, 100)}!";
    }
}
