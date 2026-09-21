namespace Flicked.Api.Config;

public static class DotEnv
{
    public static void Load(IConfigurationBuilder config, string contentRoot)
    {
        var directory = new DirectoryInfo(contentRoot);
        for (var i = 0; i < 3 && directory is not null; i++, directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, ".env");
            if (!File.Exists(path)) continue;

            config.AddInMemoryCollection(Parse(File.ReadAllLines(path)));
            return;
        }
    }

    private static Dictionary<string, string?> Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var split = line.IndexOf('=');
            if (split <= 0) continue;

            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim().Trim('"', '\'');

            if (Environment.GetEnvironmentVariable(key) is not null) continue;

            values[key] = value;
        }

        return values;
    }
}
