using MultiLaunch.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace MultiLaunch.Views.Pages
{
    /// <summary>
    /// Interaction logic for CredentialsPage.xaml
    /// </summary>
    public partial class CredentialsPage : INavigableView<CredentialsViewModel>
    {
        public CredentialsViewModel ViewModel { get; }

        public CredentialsPage(CredentialsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}
