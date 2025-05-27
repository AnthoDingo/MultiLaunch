using MultiLaunch.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MultiLaunch.Models
{
    [Table("Apps")]
    public class AppEntry
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string? IconPath { get; set; }
        public string? Arguments { get; set; }
        public string? WorkingDirectory { get; set; }

        public int AppCredId { get; set; }
        
        [ForeignKey("AppCredId")]
        public virtual AppCred Credential { get; set; }
    }
}
