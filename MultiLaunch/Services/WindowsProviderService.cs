using Microsoft.Extensions.DependencyInjection;
using MultiLaunch.Models;
using MultiLaunch.Services.Contracts;

namespace MultiLaunch.Services
{
    public class WindowsProviderService
    {
        private readonly IServiceProvider _serviceProvider;

        public WindowsProviderService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Show<T>()
            where T : class
        {
            if (!typeof(Window).IsAssignableFrom(typeof(T)))
            {
                throw new InvalidOperationException($"The window class should be derived from {typeof(Window)}.");
            }

            Window windowInstance =
                _serviceProvider.GetService<T>() as Window
                ?? throw new InvalidOperationException("Window is not registered as service.");
            windowInstance.Owner = Application.Current.MainWindow;
            windowInstance.Show();
        }

        public void ShowDialog<T>()
            where T : class
        {
            if (!typeof(Window).IsAssignableFrom(typeof(T)))
            {
                throw new InvalidOperationException($"The window class should be derived from {typeof(Window)}.");
            }

            Window windowInstance =
                _serviceProvider.GetService<T>() as Window
                ?? throw new InvalidOperationException("Window is not registered as service.");
            windowInstance.Owner = Application.Current.MainWindow;
            windowInstance.ShowDialog();
        }

        public AppEntry? ShowEditor<AppEditorWindow>(AppEntry app)
        {
            if (!typeof(Window).IsAssignableFrom(typeof(AppEditorWindow)))
            {
                throw new InvalidOperationException($"The window class should be derived from {typeof(Window)}.");
            }

            Window windowInstance =
                _serviceProvider.GetService<AppEditorWindow>() as Window
                ?? throw new InvalidOperationException("Window is not registered as service.");
            windowInstance.Owner = Application.Current.MainWindow;

            // Pass the app to the ViewModel if possible
            var viewModelProperty = windowInstance.GetType().GetProperty("ViewModel");
            var viewModel = viewModelProperty?.GetValue(windowInstance);
            var appProperty = viewModel?.GetType().GetProperty("Application");
            if (appProperty != null && appProperty.CanWrite)
            {
                appProperty.SetValue(viewModel, app);
            }

            bool? result = windowInstance.ShowDialog();

            if (windowInstance is IAppEntryResultProvider provider)
                return provider.Result;

            return null;
        }
    }
}
