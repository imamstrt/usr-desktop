using SmiWifiProvisioning.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SmiWifiProvisioning.Views
{
    public partial class ConfigProduksiView : UserControl
    {
        public ConfigProduksiView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

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

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Setiap kali IsBusy berubah menjadi false → fokus kembali ke barcode
            if (e.PropertyName == nameof(ConfigProduksiViewModel.IsBusy))
            {
                if (sender is ConfigProduksiViewModel vm && !vm.IsBusy)
                {
                    Dispatcher.BeginInvoke(new Action(FocusBarcodeBox),
                                           DispatcherPriority.Background);
                }
            }
        }

        private void FocusBarcodeBox()
        {
            TxtMeterId.Focus();
            Keyboard.Focus(TxtMeterId);
            TxtMeterId.SelectAll();
        }

        // ============================================================
        //  HANDLER UNTUK LOG — auto-scroll ke baris terbaru
        // ============================================================
        private void TxtLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.ScrollToEnd();
            }
        }
    }
}