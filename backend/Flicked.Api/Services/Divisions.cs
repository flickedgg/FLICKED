namespace Flicked.Api.Services;

/* Turning a rating into a division name.

   The thresholds are placeholders: with no real matches played, nobody knows what
   a 2000 rating means yet. They live here, in one place, so they can be changed
   once there is data, rather than being spread through the launcher. */
public static class Divisions
{
    private static readonly (int Floor, string Name)[] Bands =
    [
        (2400, "Division I"),
        (2000, "Division II"),
        (1600, "Division III"),
        (1200, "Division IV"),
        (0,    "Division V"),
    ];

    public static string For(int rating) => Bands.First(b => rating >= b.Floor).Name;
}
