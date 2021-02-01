using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lmt.db.tables
{
    [Table("PackageBadVersions")]
    public class PackageBadVersion
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
        public long PackageId { get; set; }

        [Column]
        public string FileVersionFrom { get; set; }

        [Column]
        public string FileVersionTo { get; set; }

        [Column]
        public string ProductVersionFrom { get; set; }

        [Column]
        public string ProductVersionTo { get; set; }

        #endregion
    }
}