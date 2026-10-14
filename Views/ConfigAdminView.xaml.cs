using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SmiWifiProvisioning.Views
{
    /// <summary>
    /// Interaction logic for ConfigAdminView.xaml
    /// </summary>
    public partial class ConfigAdminView : UserControl
    {
        public ConfigAdminView()
        {
            InitializeComponent();
        }

        private void TxtLogAdmin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
            }
        }
    }
}
