using Microsoft.EntityFrameworkCore;
using MultiLaunch.DbContexts;
using MultiLaunch.Models;
using Wpf.Ui.Abstractions.Controls;

namespace MultiLaunch.ViewModels.Pages
{
    public partial class AppsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private SQLiteDbContext _dbContext;

        public AppsViewModel(SQLiteDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();
            return Task.CompletedTask;
        }

        private void InitializeViewModel()
        {
            Apps = _dbContext.Apps
                .Include(a => a.Credential)
                .OrderBy(a => a.Name)
                .ToList();
        }

        [ObservableProperty]
        private IEnumerable<AppEntry> _apps;
    }
}
