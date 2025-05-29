using Microsoft.EntityFrameworkCore;
using MultiLaunch.DbContexts;
using MultiLaunch.Models;
using MultiLaunch.Services;
using MultiLaunch.Statics;
using MultiLaunch.Views.Windows;
using System.ComponentModel;
using System.Diagnostics;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace MultiLaunch.ViewModels.Pages
{
    public partial class AppsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private SQLiteDbContext _dbContext;
        private readonly WindowsProviderService _windowsProviderService;
        private readonly IContentDialogService _contentDialogService;
        private readonly ISnackbarService _snackbarService;

        public AppsViewModel(SQLiteDbContext dbContext, WindowsProviderService windowsProviderService, IContentDialogService contentDialogService, ISnackbarService snackbarService)
        {
            _dbContext = dbContext;
            _windowsProviderService = windowsProviderService;
            _contentDialogService = contentDialogService;
            _snackbarService = snackbarService;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            Apps = _dbContext.Apps
                .Include(a => a.Credential)
                .OrderBy(a => a.Name)
                .ToList();

            return Task.CompletedTask;
        }

        private void InitializeViewModel()
        {
            _isInitialized = true;
        }

        #region Apps Management

        [ObservableProperty]
        private IEnumerable<AppEntry> _apps;

        [RelayCommand]
        private void NewApp()
        {
            AppEntry? appEntry = _windowsProviderService.ShowEditor<AppEditorWindow>(new AppEntry() { Credential = _dbContext.Credentials.First(c => c.Type == Enums.CredentialType.Standard)});
            if (appEntry != null)
            {
                Apps = _dbContext.Apps
                .Include(a => a.Credential)
                .OrderBy(a => a.Name)
                .ToList();
            }
        }

        [RelayCommand]
        private void EditApp(AppEntry app)
        {
            AppEntry? appEntry = _windowsProviderService.ShowEditor<AppEditorWindow>(app);

            if (appEntry != null)
            {
                Apps = _dbContext.Apps
                .Include(a => a.Credential)
                .OrderBy(a => a.Name)
                .ToList();
            }
        }

        [RelayCommand]
        private async Task DeleteApp(AppEntry app)
        {
            if (app == null)
                return;

            ContentDialogResult result = await _contentDialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions()
                {
                    Title = "Delete Application",
                    Content = $"Are you sure you want to delete the application '{app.Name}'?",
                    PrimaryButtonText = "Delete",                
                    CloseButtonText = "Cancel",
                }
            );

            if(result != ContentDialogResult.Primary)
                return;

            _dbContext.Apps.Remove(app);
            await _dbContext.SaveChangesAsync();

            Apps = _dbContext.Apps
                .Include(a => a.Credential)
                .OrderBy(a => a.Name)
                .ToList();
        }

        #endregion

        #region Start Apps

        [RelayCommand]
        private void StartApp(AppEntry app)
        {
            switch(app.Credential.Type)
            {
                case Enums.CredentialType.Standard:
                    if(
                        System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split("\\")[0] == app.Credential.Domain
                        && 
                        System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split("\\")[1] == app.Credential.Username
                    )
                    {
                        StartAppWithCurrent(app);
                    } else
                    {
                        StartAppWithCredentials(app);
                    }                       
                    break;
                case Enums.CredentialType.Privilege:
                case Enums.CredentialType.Custom:
                    StartAppWithCredentials(app);
                    break;
                default:
                    StartAppWithCurrent(app);
                    break;
            }
        }

        private Process CreateProcess(AppEntry app)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = app.Path,
                    Arguments = app.Arguments ?? string.Empty,
                    WorkingDirectory = app.WorkingDirectory ?? string.Empty,
                }
            };
            if (app.RunAsAdmin)
            {
                process.StartInfo.UseShellExecute = true; // Required for running as admin
                process.StartInfo.Verb = "runas"; // Run as administrator
            }

            return process;
        }

        private void StartAppWithCurrent(AppEntry app)
        {
            Process process = CreateProcess(app);
            try
            {                
                process.Start();
            }
            catch (Win32Exception ex)
            {
                ShowError(app, ex);
            }
            catch (Exception ex)
            {
                //_windowsProviderService.ShowDialog<ErrorWindow>(ex.Message);
                _snackbarService.Show(
                    $"Failed to start the application {app.Name}",
                    ex.Message,
                    ControlAppearance.Info,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(5)
                );
            }
            finally
            {
                process.Dispose();   
            }
        }

        private void StartAppWithCredentials(AppEntry app)
        {
            Process process = CreateProcess(app);
            try
            {                
                process.StartInfo.UserName = app.Credential.Username;
                process.StartInfo.Password = Crypto.ConvertToSecureString(app.Credential.Password!);
                process.StartInfo.Domain = app.Credential.Domain;

                process.Start();
            }
            catch(Win32Exception ex)
            {
                ShowError(app, ex);
            }
            catch (Exception ex)
            {
                _snackbarService.Show(
                    $"Failed to start the application {app.Name}",
                    ex.Message,
                    ControlAppearance.Info,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(5)
                );
            }
            finally
            {
                process.Dispose();
            }
        }

        private void ShowError(AppEntry app, Win32Exception ex)
        {
            string message = ex.Message;
            switch (ex.NativeErrorCode)
            {
                case 1314: // ERROR_PRIVILEGE_NOT_HELD
                    message = "The required privilege is not held by the client. Please check your permissions.";
                    break;
                case 1326: // ERROR_LOGON_FAILURE
                    message = "Logon failed. Please check your credentials.";
                    break;
                case 5: // ERROR_ACCESS_DENIED
                    message = "Access denied. Please check your permissions.";
                    break;
                case 87: // ERROR_INVALID_PARAMETER
                    message = "Invalid parameters provided. Please check the application settings.";
                    break;
                case 1069: // ERROR_NOT_OWNER
                    message = "The process is not owned by the user. Please check the application settings.";
                    break;
                case 1312: // ERROR_NO_SUCH_LOGON_SESSION
                    message = "No such logon session. Please check your credentials.";
                    break;
                case 1385: // ERROR_LOGON_FAILURE
                    message = "Logon failure: the user has not been granted the requested logon type at this machine. Please check your permissions.";
                    break;
                case 123: // ERROR_INVALID_NAME
                    message = "The specified name is invalid. Please check the application path and credentials.";
                    break;
                case 1236: // ERROR_NO_SUCH_USER
                case 1327: // ERROR_NO_SUCH_USER
                    message = "No such user exists. Please check the username and domain.";
                    break;
                case 1237: // ERROR_NO_SUCH_DOMAIN
                    message = "No such domain exists. Please check the domain name.";
                    break;
                case 1238: // ERROR_NO_SUCH_LOGON_SESSION
                    message = "No such logon session exists. Please check your credentials.";
                    break;
                case 1909:
                    message = "The referenced account is currently locked out and may not be logged on to.";
                    break;
            }

            _snackbarService.Show(
                $"Failed to start the application {app.Name}",
                message,
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5)
            );
        }
        

        #endregion
    }
}
