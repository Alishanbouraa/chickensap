using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoultrySlaughterPOS.Models
{
    [Table("TRUCKS")]
    public class Truck
    {
        [Key]
        public int TruckId { get; set; }

        [Required]
        [StringLength(50)]
        public string TruckNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string DriverName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime UpdatedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual ICollection<TruckLoad> TruckLoads { get; set; } = new List<TruckLoad>();
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<DailyReconciliation> DailyReconciliations { get; set; } = new List<DailyReconciliation>();
    }
}
