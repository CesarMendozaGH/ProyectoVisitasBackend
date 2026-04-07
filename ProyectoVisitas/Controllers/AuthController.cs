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
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromForm] string access_token, [FromForm] string sid)
        {
            try
            {
                if (string.IsNullOrEmpty(access_token) || string.IsNullOrEmpty(sid))
                    return BadRequest("Token o SID faltantes.");

                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                ClaimsPrincipal principal = tokenHandler.ValidateToken(access_token, validationParams, out SecurityToken validatedToken);

                var usuarioLocal = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == sid);
                if (usuarioLocal == null) return Unauthorized("El usuario no existe en la BD local.");

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

                // Redirección hacia tu React
                string urlDestino = $"{_reactAppUrl}/auth/callback?jwt_token={miTokenInterno}&rol={usuarioLocal.Rol}&nombre={Uri.EscapeDataString(usuarioLocal.NombreCompleto)}";
                return Redirect(urlDestino);
            }
            catch (Exception ex)
            {
                return Unauthorized($"Error de autenticación: {ex.Message}");
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