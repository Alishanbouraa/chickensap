using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoultrySlaughterPOS.Models
{
    [Table("INVOICES")]
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        [Required]
        [StringLength(20)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public int CustomerId { get; set; }

        [Required]
        public int TruckId { get; set; }

        [Required]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal GrossWeight { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal CagesWeight { get; set; }

        [Required]
        public int CagesCount { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal NetWeight { get; set; }

        [Required]
        [Column(TypeName = "decimal(8,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercentage { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal FinalAmount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal PreviousBalance { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal CurrentBalance { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime UpdatedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;

        [ForeignKey("TruckId")]
        public virtual Truck Truck { get; set; } = null!;

        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}