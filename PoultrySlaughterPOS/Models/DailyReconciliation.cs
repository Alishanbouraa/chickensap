using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoultrySlaughterPOS.Models
{
    [Table("DAILY_RECONCILIATION")]
    public class DailyReconciliation
    {
        [Key]
        public int ReconciliationId { get; set; }

        [Required]
        public int TruckId { get; set; }

        [Required]
        public DateTime ReconciliationDate { get; set; } = DateTime.Today;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal LoadWeight { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal SoldWeight { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal WastageWeight { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal WastagePercentage { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "PENDING"; // PENDING, COMPLETED, REVIEWED

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("TruckId")]
        public virtual Truck Truck { get; set; } = null!;
    }
}