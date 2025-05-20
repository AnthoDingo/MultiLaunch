using MultiLaunch.DbContexts;
using MultiLaunch.Enums;
using MultiLaunch.Models;
using Wpf.Ui.Abstractions.Controls;

namespace MultiLaunch.ViewModels.Pages
{
    public partial class CredentialsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private SQLiteDbContext _dbContext;

        public CredentialsViewModel(SQLiteDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            SelectedCredentialType = CredentialType.Standard;
            SelectedCredential = _dbContext.Credentials.FirstOrDefault(c => c.Type == SelectedCredentialType);
            _isInitialized = true;
        }

        [ObservableProperty]
        private IEnumerable<CredentialType> _credentialTypes = new[] { CredentialType.Standard, CredentialType.Privilege };

        [ObservableProperty]
        private CredentialType _selectedCredentialType;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaskedPassword))]
        private AppCred _selectedCredential;

        public string MaskedPassword
        {
            get
            {
                return !string.IsNullOrEmpty(SelectedCredential?.Password) ? "********" : string.Empty;
            }
            set
            {
                if (SelectedCredential != null)
                {
                    SelectedCredential.Password = value;
                }
            }
        }        
        partial void OnSelectedCredentialTypeChanged(CredentialType value)
        {
            SelectedCredential = _dbContext.Credentials.FirstOrDefault(c => c.Type == value);
        }

        [RelayCommand]
        private void Save()
        {

        }
    }
}
