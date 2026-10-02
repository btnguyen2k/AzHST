namespace AzHST.Desktop.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
