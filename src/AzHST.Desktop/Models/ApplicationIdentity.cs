using System.Reflection;

namespace AzHST.Desktop.Models;

public sealed record ApplicationIdentity(
    string Name,
    string Version)
{
    public string DisplayText => $"{Name} {Version}";

    public static ApplicationIdentity FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var name = assembly
            .GetCustomAttribute<AssemblyProductAttribute>()
            ?.Product
            .Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = assembly
                .GetCustomAttribute<AssemblyTitleAttribute>()
                ?.Title
                .Trim();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = assembly.GetName().Name;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "The application assembly does not provide a product name.");
        }

        var fullVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            .Trim();
        if (string.IsNullOrWhiteSpace(fullVersion))
        {
            fullVersion = assembly.GetName().Version?.ToString();
        }

        if (string.IsNullOrWhiteSpace(fullVersion))
        {
            throw new InvalidOperationException(
                "The application assembly does not provide version metadata.");
        }

        var metadataSeparator = fullVersion.IndexOf('+');
        var displayVersion = metadataSeparator >= 0
            ? fullVersion[..metadataSeparator]
            : fullVersion;

        return new ApplicationIdentity(name, displayVersion);
    }
}
