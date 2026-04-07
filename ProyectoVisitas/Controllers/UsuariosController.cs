using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoVisitas.Models;

namespace ProyectoVisitas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 🔒 Bloquea el controlador por defecto
    public class UsuariosController : ControllerBase
    {
        private readonly BdvisitasContext _context;

        public UsuariosController(BdvisitasContext context)
        {
            _context = context;
        }

        // ====================================================================
        // UN SOLO GET PARA AMBOS (INTRANET Y REACT)
        // ====================================================================
        [HttpGet]
        [AllowAnonymous] // Tiene que estar abierto para que la Intranet valide
        public async Task<IActionResult> Get()
        {
            var lista = await _context.UsuariosWebs
                .Select(u => new
                {
                    u.IdUsuario,
                    u.NombreCompleto,
                    u.Rol,
                    u.FechaRegistro,
                    ssid = u.SSID // La Intranet necesita esto, React simplemente lo ignora si no lo usa
                })
                .ToListAsync();

            return Ok(lista);
        }

        // ====================================================================
        // PUT: api/Usuarios/5/Rol
        // ====================================================================
        [HttpPut("{id}/Rol")]
        [Authorize(Roles = "SUPERADMIN")] // 🔒 Reemplaza tus IFs manuales. Si no es SuperAdmin, C# lo rebota automáticamente.
        public async Task<IActionResult> ActualizarRol(int id, [FromBody] ActualizarRolDto dto)
        {
            // Solo buscamos al usuario que vamos a editar
            var usuarioAEditar = await _context.UsuariosWebs.FindAsync(id);
            if (usuarioAEditar == null) return NotFound("Usuario no encontrado.");

            // Evitar que el creador se quite el rol a sí mismo
            if (usuarioAEditar.SSID == "S-1-5-21-514523672-912588543-873931468-1115")
            {
                return BadRequest("No puedes modificar el rol del creador del sistema.");
            }

            usuarioAEditar.Rol = dto.NuevoRol;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Rol actualizado correctamente", nuevoRol = usuarioAEditar.Rol });
        }
    }

    // DTO sencillito para recibir el JSON desde React
    public class ActualizarRolDto
    {
        public string NuevoRol { get; set; } = null!;
    }
}