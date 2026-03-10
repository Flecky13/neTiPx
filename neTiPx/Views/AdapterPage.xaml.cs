using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using neTiPx.ViewModels;

namespace neTiPx.Views
{
    public partial class AdapterPage : Page
    {
        public AdapterPage()
        {
            InitializeComponent();
            Loaded += AdapterPage_Loaded;
            Unloaded += AdapterPage_Unloaded;
        }

        private void AdapterPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AdapterViewModel viewModel)
            {
                viewModel.StartConnectionMonitoring();
            }
        }

        private void AdapterPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AdapterViewModel viewModel)
            {
                viewModel.StopConnectionMonitoring();
            }
        }
    }
}
