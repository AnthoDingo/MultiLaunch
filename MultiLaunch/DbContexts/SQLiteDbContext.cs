using Microsoft.EntityFrameworkCore;
using MultiLaunch.Models;
using System.IO;

namespace MultiLaunch.DbContexts
{
    public class SQLiteDbContext : DbContext
    {
        public DbSet<AppEntry> Apps { get; set; }
        public DbSet<AppCred> Credentials { get; set; }

        public DbSet<Setting> Settings { get; set; }

        // Database path set to C:\Users\<username>\AppData\Roaming\MultiLaunch\db.sqlite
        private static string _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiLaunch", "db.sqlite");
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

            string dbDirectory = Path.GetDirectoryName(_dbPath);
            if (!Directory.Exists(dbDirectory))
            {
                // Creating directory if it doesn't exist
                Directory.CreateDirectory(dbDirectory!);
            }

            if (!File.Exists(_dbPath))
            {
                // Creating database file if it doesn't exist
                File.Create(_dbPath).Close();
            }

            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // By default, add current user and computer name to the database
            modelBuilder.Entity<AppCred>().HasData(
                new AppCred { 
                    Type = Enums.CredentialType.Standard, 
                    Username = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split("\\")[1],
                    Domain = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split("\\")[0],
                }, 
                new AppCred { Type = Enums.CredentialType.Privilege }
            );
            modelBuilder.Entity<Setting>().HasData(
                );
            
        }
    }
}
