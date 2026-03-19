using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoVisitas.Models;

namespace ProyectoVisitas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 🔒 Bloquea todo para usuarios sin token de la Intranet
    public class UsuariosController : ControllerBase
    {
        private readonly BdvisitasContext _context;

        public UsuariosController(BdvisitasContext context)
        {
            _context = context;
        }

        // GET: api/Usuarios
        [HttpGet]
        public async Task<IActionResult> GetUsuarios()
        {
            // 1. Verificar quién está pidiendo la lista
            var miSid = User.FindFirst("SID")?.Value;
            var miUsuario = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == miSid);

            // 2. Solo el SUPERADMIN puede ver la lista de todos
            if (miUsuario == null || miUsuario.Rol != "SUPERADMIN")
            {
                return Forbid("No tienes permisos para ver esta lista."); // 403 Forbidden
            }

            var lista = await _context.UsuariosWebs
                .Select(u => new
                {
                    u.IdUsuario,
                    u.NombreCompleto,
                    u.Rol,
                    u.FechaRegistro
                })
                .ToListAsync();

            return Ok(lista);
        }

        // PUT: api/Usuarios/5/Rol
        [HttpPut("{id}/Rol")]
        public async Task<IActionResult> ActualizarRol(int id, [FromBody] ActualizarRolDto dto)
        {
            // 1. Seguridad: Verificar que el que intenta cambiar el rol sea un SUPERADMIN
            var miSid = User.FindFirst("SID")?.Value;
            var miUsuario = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == miSid);

            if (miUsuario == null || miUsuario.Rol != "SUPERADMIN")
            {
                return Forbid("Solo un SuperAdmin puede cambiar roles.");
            }

            // 2. Buscar al usuario que vamos a editar
            var usuarioAEditar = await _context.UsuariosWebs.FindAsync(id);
            if (usuarioAEditar == null) return NotFound("Usuario no encontrado.");

            // 3. Evitar que Misael se quite el rol a sí mismo por accidente
            if (usuarioAEditar.SSID == "S-1-5-21-514523672-912588543-873931468-1115")
            {
                return BadRequest("No puedes modificar el rol del creador del sistema.");
            }

            // 4. Actualizamos el rol (Recibe el string que mandes desde el dropdown de React)
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