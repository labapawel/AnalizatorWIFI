using CommunityToolkit.Mvvm.ComponentModel;
using AnalizatorWiFi.UI.Services;

namespace AnalizatorWiFi.UI.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected static LocalizationService L => LocalizationService.Instance;
}
