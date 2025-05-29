using Microsoft.Extensions.DependencyInjection;
using MultiLaunch.Models;
using MultiLaunch.Services.Contracts;
using MultiLaunch.ViewModels.Windows;
using Wpf.Ui.Controls;

namespace MultiLaunch.Views.Windows
{
    /// <summary>
    /// Interaction logic for AppEditor.xaml
    /// </summary>
    public partial class AppEditorWindow : FluentWindow, IAppEntryResultProvider
    {
        public AppEditorViewModel ViewModel { get; }

        public AppEntry? Result { get; private set; }

        public AppEditorWindow(AppEditorViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            ViewModel.RequestClose += OnRequestClose;

            InitializeComponent();
        }

        private void OnRequestClose(object? sender, bool result)
        {
            DialogResult = result;
            if(result)
            {
                Result = ViewModel.Application;
            }
            else
            {
                Result = null;
            }
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe to prevent memory leaks
            ViewModel.RequestClose -= OnRequestClose;
            if(DialogResult == null)
                DialogResult = false;

            base.OnClosed(e);
        }
    }
}
