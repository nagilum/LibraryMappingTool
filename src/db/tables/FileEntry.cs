using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lmt.db.tables
{
    [Table("FileEntries")]
    public class FileEntry
    {
        #region ORM

        [Key]
        [Column]
        public long Id { get; set; }

        [Column]
        public DateTimeOffset Created { get; set; }

        [Column]
        public DateTimeOffset LastScan { get; set; }

        [Column]
        public long? PackageId { get; set; }

        [MaxLength(128)]
        [Column]
        public string ServerName { get; set; }

        [Column]
        public string ServerIps { get; set; }

        [MaxLength(1024)]
        [Column]
        public string FilePath { get; set; }

        [MaxLength(64)]
        [Column]
        public string FileName { get; set; }

        [Column]
        public long FileSize { get; set; }

        [MaxLength(32)]
        [Column]
        public string FileVersion { get; set; }

        [Column]
        public int FileVersionMajor { get; set; }

        [Column]
        public int FileVersionMinor { get; set; }

        [Column]
        public int FileVersionBuild { get; set; }

        [Column]
        public int FileVersionPrivate { get; set; }

        [MaxLength(32)]
        [Column]
        public string ProductVersion { get; set; }

        [Column]
        public int ProductVersionMajor { get; set; }

        [Column]
        public int ProductVersionMinor { get; set; }

        [Column]
        public int ProductVersionBuild { get; set; }

        [Column]
        public int ProductVersionPrivate { get; set; }

        #endregion
    }
}