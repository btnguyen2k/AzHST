namespace AzHST.Desktop.Models;

internal sealed record ApplicationLaunchOptions(bool UsePortableStorage)
{
    public const string PortableArgument = "--portable";

    public static ApplicationLaunchOptions Parse(IEnumerable<string>? arguments)
    {
        var usePortableStorage = arguments?.Any(
            argument => string.Equals(
                argument,
                PortableArgument,
                StringComparison.Ordinal)) == true;

        return new ApplicationLaunchOptions(usePortableStorage);
    }
}
