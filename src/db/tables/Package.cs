using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace lmt.db.tables
{
    [Table("Packages")]
    public class Package
    {
        #region ORM

        [Key]
        [Column]
        public long Id { get; set; }

        [Column]
        public DateTimeOffset Created { get; set; }

        [Column]
        public DateTimeOffset Updated { get; set; }

        [Column]
        public DateTimeOffset? Deleted { get; set; }

        [Column]
        public string Name { get; set; }

        [Column]
        public string Files { get; set; }

        [Column]
        public string NuGetUrl { get; set; }

        [Column]
        public string InfoUrl { get; set; }

        [Column]
        public string RepoUrl { get; set; }

        #endregion

        #region Instance functions

        /// <summary>
        /// Get filenames for this package.
        /// </summary>
        /// <returns>List of filenames.</returns>
        public string[] GetFiles()
        {
            try
            {
                return JsonSerializer.Deserialize<string[]>(this.Files);
            }
            catch
            {
                return new string[] { };
            }
        }

        #endregion
    }
}