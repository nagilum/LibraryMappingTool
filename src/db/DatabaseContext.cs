using lmt.db.tables;
using Microsoft.EntityFrameworkCore;

namespace lmt.db
{
    public class DatabaseContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                string.Format(
                    "Data Source={0}; Initial Catalog={1}; User ID={2}; Password={3};",
                    Program.LoadedConfig.Database.Hostname,
                    Program.LoadedConfig.Database.Database,
                    Program.LoadedConfig.Database.Username,
                    Program.LoadedConfig.Database.Password));
        }

        #region Db Sets

        public DbSet<FileEntry> FileEntries { get; set; }

        public DbSet<Package> Packages { get; set; }

        public DbSet<PackageBadVersion> PackageBadVersions { get; set; }

        #endregion
    }
}