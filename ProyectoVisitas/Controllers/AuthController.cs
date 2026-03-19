using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoVisitas.Models;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

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



        [HttpPost("Login")]
        [Authorize] // 🔒 El cadenero de .NET validará el token de la Intranet automáticamente
        public async Task<IActionResult> Login()
        {
            var sid = User.FindFirst("SID")?.Value;
            var nombre = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(sid)) return BadRequest("Token sin SID.");

            var usuarioLocal = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == sid);

            if (usuarioLocal == null)
            {
                usuarioLocal = new UsuarioWeb
                {
                    SSID = sid,
                    NombreCompleto = nombre ?? "Usuario Desconocido",
                    Rol = (sid == "S-1-5-21-514523672-912588543-873931468-1115") ? "SUPERADMIN" : "USUARIO_NORMAL",
                    FechaRegistro = DateTime.Now
                };
                _context.UsuariosWebs.Add(usuarioLocal);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                mensaje = "Login exitoso",
                usuario = new
                {
                    nombre = usuarioLocal.NombreCompleto,
                    rol = usuarioLocal.Rol,
                    sid = usuarioLocal.SSID
                }
            });
        }


        // ⚠️ BORRAR ANTES DE MANDAR A PRODUCCIÓN ⚠️
        [HttpGet("GenerarPaseDev")]
        [AllowAnonymous]
        public IActionResult GenerarPaseDev()
        {
            var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>
    {
        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "MISAEL PEREZ"),
        new System.Security.Claims.Claim("SID", "S-1-5-21-514523672-912588543-873931468-1115")
    };

            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("BaLe0n-Intranet-2026-JWT-Key-Segura-01"));
            var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: "IntranetAPI", audience: "IntranetFrontend",
                claims: claims, expires: DateTime.Now.AddHours(8), // Le damos 8 horas para que programes a gusto
                signingCredentials: creds
            );

            return Ok(new { token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token) });
        }

        //// --- MÉTODO TEMPORAL DE PRUEBA (Sin [Authorize]) ---
        //// Este método recibe el token como string para no usar el candado de Swagger
        //[HttpPost("PruebaLogin")]
        //[AllowAnonymous]
        //public async Task<IActionResult> PruebaLogin([FromBody] string tokenFalso)
        //{
        //    try
        //    {
        //        // 1. Abrimos el token manualmente (sin validar firma por ahora, solo leemos los datos)
        //        var handler = new JwtSecurityTokenHandler();
        //        var jwtToken = handler.ReadJwtToken(tokenFalso);

        //        // 2. Extraemos el SID y Nombre tal como lo haríamos en producción
        //        var sid = jwtToken.Claims.FirstOrDefault(c => c.Type == "SID")?.Value;
        //        var nombre = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value ??
        //                     jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        //        if (string.IsNullOrEmpty(sid)) return BadRequest("El token no contiene un SID.");

        //        // 3. JIT Provisioning: Buscamos y creamos al usuario
        //        var usuarioLocal = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == sid);

        //        if (usuarioLocal == null)
        //        {
        //            usuarioLocal = new UsuarioWeb
        //            {
        //                SSID = sid,
        //                NombreCompleto = nombre ?? "MISAEL PEREZ",
        //                Rol = (sid == "SID_DE_MISAEL") ? "SUPERADMIN" : "USUARIO_NORMAL",
        //                FechaRegistro = DateTime.Now
        //            };
        //            _context.UsuariosWebs.Add(usuarioLocal);
        //            await _context.SaveChangesAsync();
        //        }

        //        return Ok(new { mensaje = "Éxito. Usuario creado en BD.", usuario = usuarioLocal });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest($"Error al leer el token: {ex.Message}");
        //    }
        //}

        //// --- GENERADOR DE TOKEN FALSO ---
        //[HttpGet("GenerarPase")]
        //[AllowAnonymous]
        //public IActionResult GenerarPase()
        //{
        //    var claims = new List<Claim>
        //    {
        //        new Claim(ClaimTypes.Name, "MISAEL PEREZ"),
        //        new Claim("SID", "SID_DE_MISAEL")
        //    };

        //    var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
        //        System.Text.Encoding.UTF8.GetBytes("BaLe0n-Intranet-2026-JWT-Key-Segura-01")
        //    );
        //    var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        //    var token = new JwtSecurityToken(
        //        issuer: "IntranetAPI",
        //        audience: "IntranetFrontend",
        //        claims: claims,
        //        expires: DateTime.Now.AddMinutes(60),
        //        signingCredentials: creds
        //    );

        //    return Ok(new JwtSecurityTokenHandler().WriteToken(token));
        //}
    }
}