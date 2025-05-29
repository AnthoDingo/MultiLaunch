using Microsoft.Win32;
using MultiLaunch.DbContexts;
using MultiLaunch.Enums;
using MultiLaunch.Models;
using MultiLaunch.Services;
using MultiLaunch.Services.Contracts;

namespace MultiLaunch.ViewModels.Windows
{
    public partial class AppEditorViewModel : ObservableObject
    {
        private SQLiteDbContext _dbContext;
        private MainPasswordService _mainPasswordService;

        public AppEditorViewModel(SQLiteDbContext dbContext, MainPasswordService mainPasswordService)
        {
            _dbContext = dbContext;
            _mainPasswordService = mainPasswordService;

            Application.Credential = _dbContext.Credentials.First(c => c.Type == Enums.CredentialType.Standard);
        }

        public string WindowTitle => Application.Id == 0 ? "Add Application" : "Edit Application";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        private AppEntry _application = new AppEntry();

        #region App Informations

        [RelayCommand]
        private void BrowseForExecutable()
        {
            OpenFileDialog dialog = new()
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select Application Executable",
                Multiselect = false,
            };

            if(dialog.ShowDialog() != true)
                return;

            if (dialog.FileNames.Length == 0)
                return;

            Application.Path = string.Join("\n", dialog.FileNames);
            OnPropertyChanged(nameof(Application));
            OnPropertyChanged(nameof(Application.Path));
        }

        [RelayCommand]
        private void BrowseForDirectory()
        {
            OpenFolderDialog dialog = new()
            {
                Multiselect = false,
            };

            if (dialog.ShowDialog() != true)
                return;

            if (dialog.FolderNames.Length == 0)
                return;

            Application.WorkingDirectory = string.Join("\n", dialog.FolderNames);
            OnPropertyChanged(nameof(Application));
            OnPropertyChanged(nameof(Application.WorkingDirectory));
        }

        #endregion

        #region Cred Informations

        [ObservableProperty]
        private IEnumerable<CredentialType> _credentialTypes = new[] { CredentialType.Standard, CredentialType.Privilege, CredentialType.Custom };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnable))]
        private CredentialType _selectedCredentialType;

        public bool IsEnable
        {
            get
            {
                switch (SelectedCredentialType)
                {
                    case CredentialType.Standard:
                    case CredentialType.Privilege:
                        return false;
                    case CredentialType.Custom:
                        return true;
                    default:
                        return false;
                }
            }
        }

        partial void OnSelectedCredentialTypeChanged(CredentialType value)
        {
            if (value == CredentialType.Custom)
            {
                Application.Credential = new AppCred
                {
                    Type = CredentialType.Custom,
                    Username = string.Empty,
                    Password = string.Empty,
                    Domain = string.Empty
                };
            }
            else
            {
                Application.Credential = _dbContext.Credentials.First(c => c.Type == value);
            }
            OnPropertyChanged(nameof(Application));
            OnPropertyChanged(nameof(Application.Credential));
            OnPropertyChanged(nameof(Application.Credential.Username));
            OnPropertyChanged(nameof(Application.Credential.Password));
            OnPropertyChanged(nameof(Application.Credential.Domain));

        }

        #endregion

        #region Close Editor

        public event EventHandler<bool>? RequestClose;

        [RelayCommand]
        private async Task Save()
        {
            if(Application.Id == 0)
            {
                _dbContext.Apps.Add(Application);
            }

            await _dbContext.SaveChangesAsync();

            RequestClose?.Invoke(this, true);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(this, false);
        }

        #endregion
    }
}
