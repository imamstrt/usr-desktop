using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SmiWifiProvisioning.ViewModels;

namespace SmiWifiProvisioning.Views
{
    public partial class FunctionTestView : UserControl
    {
        public FunctionTestView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        // Saat view pertama kali tampil → langsung fokus ke barcode
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            FocusBarcodeBox();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;

            if (e.NewValue is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += OnVmPropertyChanged;
        }

        // Setiap kali IsBusy berubah true → false, fokus kembali ke barcode
        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FunctionTestViewModel.IsBusy) &&
                sender is FunctionTestViewModel vm && !vm.IsBusy)
            {
                Dispatcher.BeginInvoke(new Action(FocusBarcodeBox),
                                       DispatcherPriority.Background);
            }
        }

        private void FocusBarcodeBox()
        {
            TxtBarcodeInput.Focus();
            Keyboard.Focus(TxtBarcodeInput);
            TxtBarcodeInput.SelectAll();
        }
    }
}