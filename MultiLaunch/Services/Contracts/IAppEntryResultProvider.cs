using MultiLaunch.Models;

namespace MultiLaunch.Services.Contracts
{
    public interface IAppEntryResultProvider
    {
        AppEntry? Result { get; }
    }
}
