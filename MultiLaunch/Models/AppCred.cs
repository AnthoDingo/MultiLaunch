using MultiLaunch.Enums;
namespace MultiLaunch.Models
{
    public class AppCred
    {
        public CredentialType Type { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Domain { get; set; }
    }
}
