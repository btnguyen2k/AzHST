namespace AzHST.Application.Abstractions;

public interface IExternalFileLauncher
{
    void Open(Uri uri);

    void OpenContainingFolder(string filePath);
}
