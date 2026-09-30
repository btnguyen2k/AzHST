using AzHST.Application.Models;

namespace AzHST.Desktop.Services;

public interface ISettingsDialogService
{
    Task<AppSettings?> ShowAsync(AppSettings currentSettings);
}
