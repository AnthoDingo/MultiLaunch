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

        private static string _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiLaunch", "db.sqlite");
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!File.Exists(_dbPath))
            {
                Console.WriteLine("Base inexistante, création en cours...");
                File.Create(_dbPath).Close(); // Crée un fichier vide, SQLite l'utilisera
            }

            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
            
            var pendingMigrations = this.Database.GetPendingMigrations();
            if(pendingMigrations.Any())
            {
                Console.WriteLine("Migrations en attente, mise à jour de la base de données...");
                this.Database.Migrate();
            }
            else
            {
                Console.WriteLine("Aucune migration en attente.");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppCred>().HasData(
                new AppCred { Type = Enums.CredentialType.Standard, Username = System.Security.Principal.WindowsIdentity.GetCurrent().Name }, 
                new AppCred { Type = Enums.CredentialType.Privilege }
            );
            modelBuilder.Entity<Setting>().HasData(
                );
            
        }
    }
}
