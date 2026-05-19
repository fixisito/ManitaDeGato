using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace manitaDeGatoWeb.Models
{
    [Table("citas")]
    public class Cita
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime FechaCita { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeSpan HoraCita { get; set; }

        [StringLength(50)]
        public string Estado { get; set; } = "Pendiente";

        // Foreign Keys
        [ForeignKey("Cliente")]
        public int IdCliente { get; set; }
        public virtual Cliente? Cliente { get; set; }

        [ForeignKey("Estilista")]
        public int IdEstilista { get; set; }
        public virtual Estilista? Estilista { get; set; }

        [ForeignKey("Servicio")]
        public int IdServicio { get; set; }
        public virtual Servicio? Servicio { get; set; }
    }
}
