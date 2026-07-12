namespace Trading.Worker.Extensions;

internal static class EnvLoader
{
    internal static void Load()
    {
        var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(envFile)) return;
        foreach (var line in File.ReadAllLines(envFile)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#')))
        {
            var idx = line.IndexOf('=');
            if (idx > 0)
                Environment.SetEnvironmentVariable(line[..idx].Trim(), line[(idx + 1)..].Trim());
        }
    }
}
