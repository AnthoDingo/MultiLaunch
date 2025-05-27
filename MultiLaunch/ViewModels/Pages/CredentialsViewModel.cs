using MultiLaunch.DbContexts;
using MultiLaunch.Enums;
using MultiLaunch.Models;
using MultiLaunch.Services;
using Wpf.Ui.Abstractions.Controls;

namespace MultiLaunch.ViewModels.Pages
{
    public partial class CredentialsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private SQLiteDbContext _dbContext;
        private MainPasswordService _mainPasswordService;

        public CredentialsViewModel(SQLiteDbContext dbContext, MainPasswordService mainPasswordService)
        {
            _dbContext = dbContext;
            _mainPasswordService = mainPasswordService;

            //string test = _mainPasswordService.GetAsString();
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
            CustomCreds = _dbContext.Credentials.Where(c => c.Type == CredentialType.Custom).ToList();
            _isInitialized = true;
        }

        [ObservableProperty]
        private IEnumerable<CredentialType> _credentialTypes = new[] { CredentialType.Standard, CredentialType.Privilege };

        [ObservableProperty]
        private CredentialType _selectedCredentialType;

        [ObservableProperty]
        private IEnumerable<AppCred> _customCreds = new List<AppCred>();

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
            if(value == CredentialType.Custom)
            {
                SelectedCredential = new AppCred
                {
                    Type = CredentialType.Custom,
                    Username = string.Empty,
                    Password = string.Empty,
                    Domain = string.Empty
                };
            }
            else
            {
                SelectedCredential = _dbContext.Credentials.FirstOrDefault(c => c.Type == value);
            }                
        }

        [RelayCommand]
        private void Save()
        {
            string mainPassword = _mainPasswordService.GetPassword();
        }

        [RelayCommand]
        private async Task DeleteCredential(AppCred credential)
        {
            if (credential == null)
                return;

            
            //AppCred result = await _dbContext.Credentials.FirstOrDefaultAsync(c => c.Id == credential.Id);
            //if (result != null)
            //{
            //    _dbContext.Credentials.Remove(result);
            //    await _dbContext.SaveChangesAsync();
            //    CustomCreds = _dbContext.Credentials.Where(c => c.Type == CredentialType.Custom).ToList();
            //}
        }
    }
}
