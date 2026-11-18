namespace Duty.Services;

/// <summary>Единые пути локального хранилища приложения.</summary>
public static class LocalStoragePaths
{
    static LocalStoragePaths()
    {
#if WINDOWS
        // Unpackaged-приложение на Windows не имеет Package identity,
        // поэтому FileSystem.AppDataDirectory ведёт себя непредсказуемо.
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Duty");
#else
        var root = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
#endif
        DataDirectory = root;
    }

    public static string DataDirectory { get; }

    public static string DataFile => Path.Combine(DataDirectory, "duty-data.json");

    public static string ConnectionsFile => Path.Combine(DataDirectory, "duty-connections.json");
}
