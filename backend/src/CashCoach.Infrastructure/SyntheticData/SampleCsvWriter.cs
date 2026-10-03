using CashCoach.Core.Domain;

namespace CashCoach.Infrastructure.SyntheticData;

/// <summary>Writes the synthetic persona CSVs into the repo's <c>data/samples</c> folder.</summary>
public static class SampleCsvWriter
{
    public static string FileName(Persona persona) => $"synthetic_{SnakeCaseEnum<Persona>.ToName(persona)}.csv";

    /// <summary>Walks up from <paramref name="startDirectory"/> to the first folder that contains <c>data/samples</c>.</summary>
    public static string FindSamplesDirectory(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "data", "samples");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"No data/samples folder above '{startDirectory}'.");
    }

    public static IReadOnlyList<string> WriteAll(string samplesDirectory)
    {
        var paths = new List<string>();
        foreach (var persona in Enum.GetValues<Persona>())
        {
            var path = Path.Combine(samplesDirectory, FileName(persona));
            File.WriteAllText(path, SyntheticDataGenerator.GenerateCsv(persona));
            paths.Add(path);
        }

        return paths;
    }
}
