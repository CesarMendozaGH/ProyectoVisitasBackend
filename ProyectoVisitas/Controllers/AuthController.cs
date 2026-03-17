using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoVisitas.Models; // Asegúrate de que este using coincida con tu namespace
using System.Security.Claims;

namespace ProyectoVisitas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly BdvisitasContext _context;

        public AuthController(BdvisitasContext context)
        {
            _context = context;
        }

        // POST: api/Auth/Login
        [HttpPost("Login")]
        [Authorize] // 🔒 ESTA ETIQUETA ES MAGIA Pura: Rechaza cualquier petición que no traiga un JWT válido de la Intranet
        public async Task<IActionResult> LoginSync()
        {
            // 1. Si el código llega a esta línea, significa que el Token es válido, tiene la firma correcta y no ha caducado.

            // 2. Extraemos el SID y el Nombre exactamente como la Intranet los metió en el Token
            var sid = User.FindFirst("SID")?.Value;
            var nombre = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(sid))
            {
                return BadRequest(new { mensaje = "El token es válido, pero no contiene el campo SID obligatorio." });
            }

            // 3. Buscamos en nuestra tabla si este usuario ya nos había visitado antes
            var usuarioLocal = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == sid);

            // 4. JIT PROVISIONING: Si es nuevo, lo creamos en este exacto milisegundo
            if (usuarioLocal == null)
            {
                usuarioLocal = new UsuarioWeb
                {
                    SSID = sid,
                    NombreCompleto = nombre ?? "Usuario Desconocido",

                    // 👑 EL HARDCODEO DEL SUPERADMIN (Cambia "SID_DE_MISAEL" por el SID real largo de Misael)
                    Rol = (sid == "SID_DE_MISAEL") ? "SUPERADMIN" : "USUARIO_NORMAL",

                    FechaRegistro = DateTime.Now
                };

                _context.UsuariosWebs.Add(usuarioLocal);
                await _context.SaveChangesAsync();
            }

            // 5. Le devolvemos un OK a React (o a la Intranet) con los datos del usuario para que pinte la pantalla
            return Ok(new
            {
                mensaje = "Acceso concedido y sincronizado con éxito",
                usuario = new
                {
                    nombre = usuarioLocal.NombreCompleto,
                    rol = usuarioLocal.Rol,
                    sid = usuarioLocal.SSID
                }
            });
        }
    }
}