using AnalizatorWiFi.Core.Models;

namespace AnalizatorWiFi.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Current { get; }
    Task LoadAsync();
    Task SaveAsync();
}
