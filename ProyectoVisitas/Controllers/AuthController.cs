using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProyectoVisitas.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;



public class IntercambioRequest
{
    public string access_token { get; set; }
    public string sid { get; set; }
}

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

        [HttpPost("IntercambiarToken")]
        [AllowAnonymous]
        public async Task<IActionResult> IntercambiarToken([FromBody] IntercambioRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.access_token) || string.IsNullOrEmpty(request.sid))
                    return BadRequest("Datos incompletos.");

                // 1. CONFIGURACIÓN DEL CADENERO
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
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

                // 2. VALIDAR EL TOKEN DE LA INTRANET
                ClaimsPrincipal principal = tokenHandler.ValidateToken(request.access_token, validationParameters, out SecurityToken validatedToken);

                var nombre = principal.FindFirst("unique_name")?.Value ??
                             principal.FindFirst(ClaimTypes.Name)?.Value ?? "Usuario Desconocido";

                // 3. BUSCAR EN BD
                var usuarioLocal = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == request.sid);
                if (usuarioLocal == null)
                {
                    usuarioLocal = new UsuarioWeb
                    {
                        SSID = request.sid,
                        NombreCompleto = nombre,
                        Rol = (request.sid == "S-1-5-21-514523672-912588543-873931468-1115") ? "SUPERADMIN" : "USUARIO_NORMAL",
                        FechaRegistro = DateTime.Now
                    };
                    _context.UsuariosWebs.Add(usuarioLocal);
                    await _context.SaveChangesAsync();
                }

                // 4. CREAR EL TOKEN INTERNO
                var localClaims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, usuarioLocal.NombreCompleto),
            new Claim("SID", usuarioLocal.SSID),
            new Claim(ClaimTypes.Role, usuarioLocal.Rol)
        };

                var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
                var tokenLocal = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: localClaims,
                    expires: DateTime.Now.AddHours(8),
                    signingCredentials: creds
                );

                string miTokenInterno = tokenHandler.WriteToken(tokenLocal);

                // 5. REGRESAR JSON A REACT (Ya no hacemos Redirect)
                // 5. REGRESAR JSON A REACT
                return Ok(new
                {
                    token = miTokenInterno,
                    rol = usuarioLocal.Rol,
                    nombre = usuarioLocal.NombreCompleto // <-- Le pasamos el nombre que sacamos de la BD
                });
            }
            catch (Exception ex)
            {
                return Unauthorized($"Error validando acceso: {ex.Message}");
            }
        }

        // ⚠️ BORRAR ANTES DE MANDAR A PRODUCCIÓN ⚠️
        // Este método sigue sirviendo para generar tokens de prueba válidos para el simulador HTML
        [HttpGet("GenerarPaseDev")]
        [AllowAnonymous]
        public IActionResult GenerarPaseDev()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "MISAEL PEREZ"),
                new Claim("SID", "S-1-5-21-514523672-912588543-873931468-1115")
                // Nota: Ya no necesitamos ponerle el Rol aquí, porque el SSORedirect 
                // ahora se encarga de buscarlo en la BD y fabricar un token nuevo.
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
}