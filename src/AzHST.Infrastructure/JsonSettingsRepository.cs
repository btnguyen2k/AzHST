using System.Text.Json;
using AzHST.Application.Abstractions;
using AzHST.Application.Models;

namespace AzHST.Infrastructure;

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _settingsFile;
    private readonly string _defaultOutputDirectory;

    public JsonSettingsRepository(ApplicationPaths paths)
    {
        _settingsFile = paths.SettingsFile;
        _defaultOutputDirectory = paths.GeneratedPagesDirectory;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFile))
        {
            return CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsFile);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                SerializerOptions,
                cancellationToken);

            return settings is null
                ? throw new InvalidDataException("The settings file does not contain an object.")
                : Normalize(settings);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The settings file '{_settingsFile}' contains invalid JSON.",
                exception);
        }
    }

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(_settingsFile)
            ?? throw new InvalidOperationException("The settings path does not have a parent directory.");

        Directory.CreateDirectory(directory);
        var temporaryFile = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = File.Create(temporaryFile))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    normalized,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryFile, _settingsFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    private AppSettings CreateDefault()
    {
        return new AppSettings
        {
            OutputDirectory = _defaultOutputDirectory,
        };
    }

    private AppSettings Normalize(AppSettings settings)
    {
        return settings with
        {
            Model = string.IsNullOrWhiteSpace(settings.Model)
                ? AppSettings.DefaultModel
                : settings.Model.Trim(),
            OutputDirectory = string.IsNullOrWhiteSpace(settings.OutputDirectory)
                ? _defaultOutputDirectory
                : Path.GetFullPath(settings.OutputDirectory.Trim()),
        };
    }
}
