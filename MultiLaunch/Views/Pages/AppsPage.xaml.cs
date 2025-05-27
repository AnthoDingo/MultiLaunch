using MultiLaunch.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace MultiLaunch.Views.Pages
{
    /// <summary>
    /// Interaction logic for AppsPage.xaml
    /// </summary>
    public partial class AppsPage : INavigableView<AppsViewModel>
    {

        public AppsViewModel ViewModel { get; }

        public AppsPage(AppsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}
