using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoVisitas.Models
{
    [Table("UsuariosWeb")]
    public partial class UsuarioWeb
    {
        [Key]
        public int IdUsuario { get; set; }

        [Required]
        [StringLength(255)]
        public string SSID { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string NombreCompleto { get; set; } = null!;

        public int? IdDepartamento { get; set; }

        [Required]
        [StringLength(50)]
        public string Rol { get; set; } = "USUARIO_NORMAL";

        public DateTime FechaRegistro { get; set; }
    }
}