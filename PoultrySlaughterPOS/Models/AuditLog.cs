using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoultrySlaughterPOS.Models
{
    [Table("AUDIT_LOGS")]
    public class AuditLog
    {
        [Key]
        public int AuditId { get; set; }

        [Required]
        [StringLength(50)]
        public string TableName { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string Operation { get; set; } = string.Empty; // INSERT, UPDATE, DELETE

        [Column(TypeName = "ntext")]
        public string? OldValues { get; set; }

        [Column(TypeName = "ntext")]
        public string? NewValues { get; set; }

        [StringLength(50)]
        public string? UserId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}