using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace manitaDeGatoWeb.Models
{
    [Table("servicios")]
    public class Servicio
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        public int Duracion { get; set; } // En minutos

        [StringLength(500)]
        public string Descripcion { get; set; } = string.Empty;

        // Foreign Keys
        [ForeignKey("Categoria")]
        public int Id_categoria { get; set; }
        public virtual Categoria? Categoria { get; set; }

        [ForeignKey("Estilista")]
        public int? IdEstilista { get; set; }
        public virtual Estilista? Estilista { get; set; }

        // Navigation Property
        public virtual ICollection<Cita>? Citas { get; set; }
    }
}
