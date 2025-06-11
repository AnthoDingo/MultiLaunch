using Microsoft.EntityFrameworkCore;
using MultiLaunch.DbContexts;
using MultiLaunch.Enums;
using MultiLaunch.Models;
using MultiLaunch.Services;
using MultiLaunch.Statics;
using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace MultiLaunch.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private SQLiteDbContext _dbContext;
        private MainPasswordService _mainPasswordService;
        //private EncryptionService _encryptionService;

        public MainWindowViewModel(SQLiteDbContext dbContext, MainPasswordService mainPasswordService)
        {
            _dbContext = dbContext;
            _mainPasswordService = mainPasswordService;
            //_encryptionService = new EncryptionService();
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
        private string _unlockMainPassword = string.Empty;

        [ObservableProperty]
        private string _unlockErrorText = string.Empty;

        [ObservableProperty]
        private string _mainPassword = string.Empty;

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
            Setting setting = await _dbContext.Settings.FirstAsync(x => x.Key == "validator");

            LoaderStackVisibility = Visibility.Collapsed;
            if (setting.Value == string.Empty || setting.Value == System.Security.Principal.WindowsIdentity.GetCurrent().Name)
            {
                if (setting.Value == string.Empty)
                {
                    setting.Value = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                }

                AppCred? stdCred = await _dbContext.Credentials.FirstOrDefaultAsync(x => x.Type == CredentialType.Standard);
                stdCred.Username = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split("\\")[1];
                stdCred.Domain = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split("\\")[0];
                await _dbContext.SaveChangesAsync();

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
            Setting setting = await _dbContext.Settings.FirstAsync(x => x.Key == "validator");
            setting.Value = Crypto.Encrypt(MainPassword, System.Security.Principal.WindowsIdentity.GetCurrent().Name);

            await _dbContext.SaveChangesAsync();

            //_mainPasswordService.Set(MainPassword);
            _mainPasswordService.SetPassword(MainPassword);

            SplashGridVisibility = Visibility.Collapsed;
            RootGridVisibility = Visibility.Visible;
        }

        [RelayCommand]
        public async Task UnlockDatabase()
        {
            Setting setting = await _dbContext.Settings.FirstAsync(x => x.Key == "validator");
            if (Crypto.Decrypt(UnlockMainPassword, setting.Value) == System.Security.Principal.WindowsIdentity.GetCurrent().Name)
            {
                //_mainPasswordService.Set(MainPassword);
                _mainPasswordService.SetPassword(UnlockMainPassword);
                SplashGridVisibility = Visibility.Collapsed;
                RootGridVisibility = Visibility.Visible;
            }
            else
            {
                UnlockErrorText = "Invalid password";
            }
        }

#endregion
    }
}
