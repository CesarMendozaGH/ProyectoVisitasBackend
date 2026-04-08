using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProyectoVisitas.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ProyectoVisitas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly BdvisitasContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _reactAppUrl = "http://localhost:5173";

        public AuthController(BdvisitasContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ====================================================================
        // 1. EL POST DE LA INTRANET (URL CREATE)
        // ====================================================================
        [HttpPost("SyncCrear")]
        [AllowAnonymous]
        public async Task<IActionResult> SyncCrear([FromBody] SyncUsuarioDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.ssid)) return BadRequest("Datos nulos.");

            var usuario = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == dto.ssid);
            if (usuario == null)
            {
                usuario = new UsuarioWeb
                {
                    SSID = dto.ssid,
                    NombreCompleto = string.IsNullOrEmpty(dto.nombre) ? "Usuario Intranet" : dto.nombre,
                    Rol = (dto.ssid == "S-1-5-21-514523672-912588543-873931468-1115") ? "SUPERADMIN" : "USUARIO_NORMAL",
                    FechaRegistro = DateTime.Now
                };
                _context.UsuariosWebs.Add(usuario);
                await _context.SaveChangesAsync();
            }
            return Ok(new { mensaje = "Registrado correctamente" });
        }

        // ====================================================================
        // 2. EL ACCESO FINAL (URL ACCESO)
        // ====================================================================
       
        [HttpGet("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromQuery] string access_token, [FromQuery] string sid) 
        {
            try
            {
                if (string.IsNullOrEmpty(access_token) || string.IsNullOrEmpty(sid))
                    return BadRequest("Faltan credenciales en la URL.");

                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
                var tokenHandler = new JwtSecurityTokenHandler();

                // Relajamos las validaciones para que acepte el token de la Intranet sin quejarse
                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // 1. Validamos el token de la Intranet
                ClaimsPrincipal principal = tokenHandler.ValidateToken(access_token, validationParams, out SecurityToken validatedToken);

                // 2. Buscamos al usuario en nuestra BD
                var usuarioLocal = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == sid);

                // 3. 🚨 RED DE SEGURIDAD: Si no existe (porque se saltaron el SyncCrear), lo creamos silenciosamente
                if (usuarioLocal == null)
                {
                    // Sacamos el nombre directamente del token ("jsanchez", "iflores", etc.)
                    var nombreEnToken = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value
                                        ?? principal.Identity?.Name
                                        ?? "Usuario Intranet";

                    usuarioLocal = new UsuarioWeb
                    {
                        SSID = sid,
                        NombreCompleto = nombreEnToken,
                        Rol = (sid == "S-1-5-21-514523672-912588543-873931468-1115") ? "SUPERADMIN" : "USUARIO_NORMAL",
                        FechaRegistro = DateTime.Now
                    };

                    _context.UsuariosWebs.Add(usuarioLocal);
                    await _context.SaveChangesAsync();
                }

                // 4. Fabricamos nuestro propio Token Interno (La "Casa de Cambio")
                var localClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usuarioLocal.NombreCompleto),
                    new Claim("SID", usuarioLocal.SSID),
                    new Claim(ClaimTypes.Role, usuarioLocal.Rol)
                };

                var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
                var tokenLocal = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"], audience: _configuration["Jwt:Audience"],
                    claims: localClaims, expires: DateTime.Now.AddHours(8), signingCredentials: creds
                );

                string miTokenInterno = tokenHandler.WriteToken(tokenLocal);

                // 5. El gran salto: Le decimos al navegador que se vaya a React con el token bueno
                string urlDestino = $"{_reactAppUrl}/auth/callback?jwt_token={miTokenInterno}&rol={usuarioLocal.Rol}&nombre={Uri.EscapeDataString(usuarioLocal.NombreCompleto)}";

                return Redirect(urlDestino);
            }
            catch (Exception ex)
            {
                return Unauthorized($"Error al procesar la entrada: {ex.Message}");
            }
        }

        //PRUEBA DE CAMBIAR TOKEN PARA PRODUCCION
        [HttpPost("IntercambiarToken")]
        [AllowAnonymous]
        public async Task<IActionResult> IntercambiarToken([FromBody] IntercambioRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.access_token) || string.IsNullOrEmpty(request.sid))
                    return BadRequest("Faltan credenciales.");

                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // 1. Validamos el token de la Intranet
                ClaimsPrincipal principal = tokenHandler.ValidateToken(request.access_token, validationParams, out SecurityToken validatedToken);

                // 2. Buscamos o creamos al usuario
                var usuarioLocal = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == request.sid);

                if (usuarioLocal == null)
                {
                    var nombreEnToken = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value ?? "Usuario Intranet";
                    usuarioLocal = new UsuarioWeb
                    {
                        SSID = request.sid,
                        NombreCompleto = nombreEnToken,
                        Rol = (request.sid == "S-1-5-21-514523672-912588543-873931468-1115") ? "SUPERADMIN" : "USUARIO_NORMAL",
                        FechaRegistro = DateTime.Now
                    };
                    _context.UsuariosWebs.Add(usuarioLocal);
                    await _context.SaveChangesAsync();
                }

                // 3. Fabricamos tu token oficial
                var localClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usuarioLocal.NombreCompleto),
                    new Claim("SID", usuarioLocal.SSID),
                    new Claim(ClaimTypes.Role, usuarioLocal.Rol)
                };

                var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
                var tokenLocal = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"], audience: _configuration["Jwt:Audience"],
                    claims: localClaims, expires: DateTime.Now.AddHours(8), signingCredentials: creds
                );

                // 4. ¡LA CLAVE! Regresamos JSON puro para que React lo atrape
                return Ok(new
                {
                    jwt_token = tokenHandler.WriteToken(tokenLocal),
                    rol = usuarioLocal.Rol,
                    nombre = usuarioLocal.NombreCompleto
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { error = $"Token inválido o expirado: {ex.Message}" });
            }
        }


        // ====================================================================
        // ⚠️ BORRAR ANTES DE MANDAR A PRODUCCIÓN ⚠️
        // ====================================================================
        [HttpGet("GenerarPaseDev")]
        [AllowAnonymous]
        public IActionResult GenerarPaseDev()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "MISAEL PEREZ"),
                new Claim("SID", "S-1-5-21-514523672-912588543-873931468-1115")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("BaLe0n-Intranet-2026-JWT-Key-Segura-01"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "IntranetAPI", audience: "IntranetFrontend",
                claims: claims, expires: DateTime.Now.AddHours(8),
                signingCredentials: creds
            );

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }
    }

    // ====================================================================
    // CLASES DTO (Deben ir fuera de los endpoints)
    // ====================================================================
    public class SyncUsuarioDto
    {
        public string ssid { get; set; }
        public string nombre { get; set; }
    }

    public class IntercambioRequest
{
    public string access_token { get; set; }
    public string sid { get; set; }
}

}