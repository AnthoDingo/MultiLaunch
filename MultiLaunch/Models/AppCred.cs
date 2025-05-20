using MultiLaunch.Enums;
using System.ComponentModel.DataAnnotations;
namespace MultiLaunch.Models
{
    public class AppCred
    {
        [Key]
        public CredentialType Type { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Domain { get; set; }
    }
}
