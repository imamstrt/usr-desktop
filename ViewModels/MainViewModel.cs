using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmiWifiProvisioning.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object _currentViewModel;

    public ConfigProduksiViewModel ConfigProduksiVm { get; } = new();
    public ConfigUsrViewModel ConfigUsrVm { get; } = new();
    public ConfigAdminViewModel ConfigAdminVm { get; } = new();
    public FunctionTestViewModel FunctionTestVm { get; } = new(); // Tambahan Halaman Baru

    public MainViewModel()
    {
        _currentViewModel = ConfigProduksiVm;
    }

    [RelayCommand]
    private void NavigateToConfigProduksi() => CurrentViewModel = ConfigProduksiVm;

    [RelayCommand]
    private void NavigateToConfigUsr() => CurrentViewModel = ConfigUsrVm;

    [RelayCommand]
    private void NavigateToConfigAdmin() => CurrentViewModel = ConfigAdminVm;

    [RelayCommand]
    private void NavigateToFunctionTest() => CurrentViewModel = FunctionTestVm; // Command Navigasi Baru
}