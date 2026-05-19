using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace manitaDeGatoWeb.Models
{
    [Table("administradores")]
    public class Administrador
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Usuario { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Contraseña { get; set; } = string.Empty;
    }
}
