using Microsoft.EntityFrameworkCore;
using MultiLaunch.DbContexts;
using MultiLaunch.Enums;
using MultiLaunch.Models;
using MultiLaunch.Services;
using Wpf.Ui;
using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace MultiLaunch.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private SQLiteDbContext _dbContext;
        private VaultService _vaultService;
        private readonly ISnackbarService _snackbarService;

        /// <summary>Number of consecutive failed unlock attempts, used to slow down guessing.</summary>
        private int _failedUnlockAttempts;

        public MainWindowViewModel(SQLiteDbContext dbContext, VaultService vaultService, ISnackbarService snackbarService)
        {
            _dbContext = dbContext;
            _vaultService = vaultService;
            _snackbarService = snackbarService;
        }

        #region Menus

        [ObservableProperty]
        private string _applicationTitle = "MultiLaunch";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Apps",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Apps24 },
                TargetPageType = typeof(Views.Pages.AppsPage)
            },
            //new NavigationViewItem()
            //{
            //    Content = "Home",
            //    Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
            //    TargetPageType = typeof(Views.Pages.DashboardPage)
            //},
            //new NavigationViewItem()
            //{
            //    Content = "Data",
            //    Icon = new SymbolIcon { Symbol = SymbolRegular.DataHistogram24 },
            //    TargetPageType = typeof(Views.Pages.DataPage)
            //}
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Creds",
                Icon = new SymbolIcon { Symbol = SymbolRegular.InprivateAccount24 },
                TargetPageType = typeof(Views.Pages.CredentialsPage)
            },

            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" }
        };

        #endregion

        #region Splash Screen

        [ObservableProperty]
        private Visibility _rootGridVisibility = Visibility.Visible;

        [ObservableProperty]
        private Visibility _splashGridVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility _loaderStackVisibility = Visibility.Visible;
        
        [ObservableProperty]
        private Visibility _firstRunGridVisibility = Visibility.Collapsed;
        
        [ObservableProperty]
        private Visibility _unlockGridVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility _resetGridVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private string _unlockMainPassword = string.Empty;

        [ObservableProperty]
        private string _unlockErrorText = string.Empty;

        [ObservableProperty]
        private string _mainPassword = string.Empty;

        [ObservableProperty]
        private string _mainPasswordErrorText = string.Empty;

        [ObservableProperty]
        private bool _isUnlocking;

        [RelayCommand]
        public async Task InvokeSplashScreen()
        {
            // Show splash screen
            RootGridVisibility = Visibility.Collapsed;
            SplashGridVisibility = Visibility.Visible;

            var pendingMigrations = _dbContext.Database.GetPendingMigrations();
            if (pendingMigrations.Any())
            {
                _dbContext.Database.Migrate();
            }

            VaultState state = await _vaultService.GetStateAsync();

            LoaderStackVisibility = Visibility.Collapsed;

            if (state == VaultState.Uninitialized)
            {
                AppCred? stdCred = await _dbContext.Credentials.FirstOrDefaultAsync(x => x.Type == CredentialType.Standard);
                if (stdCred != null)
                {
                    string[] identity = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split("\\");
                    stdCred.Domain = identity[0];
                    stdCred.Username = identity.Length > 1 ? identity[1] : identity[0];
                    await _dbContext.SaveChangesAsync();
                }

                FirstRunGridVisibility = Visibility.Visible;
            }
            else
            {
                UnlockGridVisibility = Visibility.Visible;
            }
        }

        [RelayCommand]
        public async Task DefineMainPassword()
        {
            if (IsUnlocking)
                return;

            if (MainPassword.Length < VaultService.MinimumPasswordLength)
            {
                MainPasswordErrorText = $"The main password must be at least {VaultService.MinimumPasswordLength} characters long.";
                return;
            }

            MainPasswordErrorText = string.Empty;
            IsUnlocking = true;

            try
            {
                // Key derivation is intentionally expensive, keep the UI thread free.
                string password = MainPassword;
                await Task.Run(() => _vaultService.CreateAsync(password));

                MainPassword = string.Empty;

                SplashGridVisibility = Visibility.Collapsed;
                RootGridVisibility = Visibility.Visible;
            }
            finally
            {
                IsUnlocking = false;
            }
        }

        [RelayCommand]
        public async Task UnlockDatabase()
        {
            if (IsUnlocking)
                return;

            UnlockErrorText = string.Empty;
            IsUnlocking = true;

            try
            {
                string password = UnlockMainPassword;
                bool unlocked = await Task.Run(() => _vaultService.TryUnlockAsync(password));

                if (unlocked)
                {
                    _failedUnlockAttempts = 0;
                    UnlockMainPassword = string.Empty;

                    SplashGridVisibility = Visibility.Collapsed;
                    RootGridVisibility = Visibility.Visible;
                    return;
                }

                // Back off a little more on every failure to make online guessing impractical.
                _failedUnlockAttempts++;
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(250 * _failedUnlockAttempts, 5000)));

                UnlockErrorText = "Invalid password";
            }
            finally
            {
                IsUnlocking = false;
            }
        }

        #endregion

        #region Forgotten Main Password

        [ObservableProperty]
        private string _resetMainPassword = string.Empty;

        [ObservableProperty]
        private string _resetMainPasswordConfirmation = string.Empty;

        [ObservableProperty]
        private string _resetErrorText = string.Empty;

        /// <summary>Switches from the unlock prompt to the reset prompt.</summary>
        [RelayCommand]
        public void ShowResetVault()
        {
            if (IsUnlocking)
                return;

            ResetMainPassword = string.Empty;
            ResetMainPasswordConfirmation = string.Empty;
            ResetErrorText = string.Empty;
            UnlockErrorText = string.Empty;

            UnlockGridVisibility = Visibility.Collapsed;
            ResetGridVisibility = Visibility.Visible;
        }

        /// <summary>Goes back to the unlock prompt without touching anything.</summary>
        [RelayCommand]
        public void CancelResetVault()
        {
            if (IsUnlocking)
                return;

            ResetMainPassword = string.Empty;
            ResetMainPasswordConfirmation = string.Empty;
            ResetErrorText = string.Empty;

            ResetGridVisibility = Visibility.Collapsed;
            UnlockGridVisibility = Visibility.Visible;
        }

        /// <summary>
        /// Drops the unreadable secrets and rebuilds the vault around a new main password. The
        /// applications and the usernames they run as are kept; only the passwords are lost.
        /// </summary>
        [RelayCommand]
        public async Task ResetVault()
        {
            if (IsUnlocking)
                return;

            if (ResetMainPassword.Length < VaultService.MinimumPasswordLength)
            {
                ResetErrorText = $"The main password must be at least {VaultService.MinimumPasswordLength} characters long.";
                return;
            }

            if (ResetMainPassword != ResetMainPasswordConfirmation)
            {
                ResetErrorText = "Both passwords must match.";
                return;
            }

            ResetErrorText = string.Empty;
            IsUnlocking = true;

            try
            {
                // Key derivation is intentionally expensive, keep the UI thread free.
                string password = ResetMainPassword;
                int clearedSecrets = await Task.Run(() => _vaultService.ResetAsync(password));

                ResetMainPassword = string.Empty;
                ResetMainPasswordConfirmation = string.Empty;
                _failedUnlockAttempts = 0;

                ResetGridVisibility = Visibility.Collapsed;
                SplashGridVisibility = Visibility.Collapsed;
                RootGridVisibility = Visibility.Visible;

                _snackbarService.Show(
                    "Main password reset",
                    clearedSecrets == 0
                        ? "No stored password had to be cleared."
                        : $"{clearedSecrets} stored password(s) were cleared. Enter them again from the Creds page.",
                    ControlAppearance.Caution,
                    new SymbolIcon(SymbolRegular.ShieldKeyhole24),
                    TimeSpan.FromSeconds(10)
                );
            }
            finally
            {
                IsUnlocking = false;
            }
        }

        #endregion
    }
}
