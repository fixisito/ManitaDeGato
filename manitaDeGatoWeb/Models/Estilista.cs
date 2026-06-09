using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace manitaDeGatoWeb.Models
{
    [Table("estilistas")]
    public class Estilista
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(100)]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El RUT es obligatorio")]
        [StringLength(20)]
        [Display(Name = "RUT")]
        public string Rut { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Usuario { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Contraseña { get; set; } = string.Empty;

        // Navigation Properties
        public virtual ICollection<Servicio>? Servicios { get; set; }
        public virtual ICollection<Cita>? Citas { get; set; }
    }
}
