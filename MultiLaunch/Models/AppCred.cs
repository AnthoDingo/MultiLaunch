using MultiLaunch.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace MultiLaunch.Models
{
    public class AppCred
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public CredentialType Type { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Domain { get; set; }

        [NotMapped]
        public string? FQDN
        {
            get
            {
                if (string.IsNullOrEmpty(Domain) || string.IsNullOrEmpty(Username))
                    return null;
                return $"{Domain}\\{Username}";
            }
        }
    }
}
