using MultiLaunch.Enums;
using MultiLaunch.Statics;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows.Media.Imaging;

namespace MultiLaunch.Models
{
    [Table("Apps")]
    public class AppEntry
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        [NotMapped]
        public BitmapSource? Icon
        {
            get
            {
                //if(string.IsNullOrEmpty(Path) || !System.IO.File.Exists(Path))
                //    return null;

                return IconExtractor.ExtractIcon(Path);
            }
        }
        public string? Arguments { get; set; }
        public string? WorkingDirectory { get; set; }
        public bool RunAsAdmin { get; set; } = false;

        public int AppCredId { get; set; }
        
        [ForeignKey("AppCredId")]
        public virtual AppCred Credential { get; set; }
    }
}
