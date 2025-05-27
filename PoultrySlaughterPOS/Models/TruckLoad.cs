using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoultrySlaughterPOS.Models
{
    [Table("TRUCK_LOADS")]
    public class TruckLoad
    {
        [Key]
        public int LoadId { get; set; }

        [Required]
        public int TruckId { get; set; }

        [Required]
        public DateTime LoadDate { get; set; } = DateTime.Today;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalWeight { get; set; }

        [Required]
        public int CagesCount { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "LOADED"; // LOADED, IN_TRANSIT, COMPLETED

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime UpdatedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("TruckId")]
        public virtual Truck Truck { get; set; } = null!;
    }
}
