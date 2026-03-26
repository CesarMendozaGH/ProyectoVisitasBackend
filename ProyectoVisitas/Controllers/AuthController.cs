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

        // 2. DECLARAS LA VARIABLE DE CONFIGURACIÓN AQUÍ
        private readonly IConfiguration _configuration;

        private readonly string _reactAppUrl = "http://localhost:5173";

        // 3. AGREGAS IConfiguration AL CONSTRUCTOR
        public AuthController(BdvisitasContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration; // <-- 4. Lo asignas para poder usarlo en tus métodos
        }

        [HttpPost("SSORedirect")]
        [AllowAnonymous]
        public async Task<IActionResult> SSORedirect([FromForm] string access_token, [FromForm] string sid)
        {
            try
            {
                if (string.IsNullOrEmpty(access_token) || string.IsNullOrEmpty(sid))
                {
                    return BadRequest("Datos incompletos desde la Intranet.");
                }

                // 1. OBTENER LA CONFIGURACIÓN DEL APPSETTINGS.JSON
                var keyString = _configuration["Jwt:Key"];
                var issuer = _configuration["Jwt:Issuer"];
                var audience = _configuration["Jwt:Audience"];

                var key = Encoding.UTF8.GetBytes(keyString);

                // 2. CONFIGURAR EL "CADENERO" ESTRICTO
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key), // Tu llave: BaLe0n-Intranet-2026...

                    ValidateIssuer = true,
                    ValidIssuer = issuer, // "IntranetAPI"

                    ValidateAudience = true,
                    ValidAudience = audience, // "IntranetFrontend"

                    // Si el token ya expiró, lo rechaza
                    ValidateLifetime = true,
                    // Quitamos los 5 minutos de gracia que da C# por defecto
                    ClockSkew = TimeSpan.Zero
                };

                // 3. VALIDAR EL TOKEN (Si alguien manda un token falso, esto lanza una excepción)
                ClaimsPrincipal principal = tokenHandler.ValidateToken(access_token, validationParameters, out SecurityToken validatedToken);

                // 4. EXTRAER LOS DATOS DEL TOKEN VALIDADO
                // Buscamos el nombre (Dependiendo de cómo lo llame la Intranet)
                var nombre = principal.FindFirst("unique_name")?.Value ??
                             principal.FindFirst(ClaimTypes.Name)?.Value ??
                             "Usuario Desconocido";

                // 5. JIT PROVISIONING (Crear/Actualizar en BD)
                var usuarioLocal = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == sid);

                if (usuarioLocal == null)
                {
                    usuarioLocal = new UsuarioWeb
                    {
                        SSID = sid,
                        NombreCompleto = nombre,
                        Rol = (sid == "S-1-5-21-514523672-912588543-873931468-1115") ? "SUPERADMIN" : "USUARIO_NORMAL",
                        FechaRegistro = DateTime.Now
                    };
                    _context.UsuariosWebs.Add(usuarioLocal);
                    await _context.SaveChangesAsync();
                }

                // 6. REDIRIGIR A REACT
                string urlDestino = $"{_reactAppUrl}/auth/callback?token={access_token}&rol={usuarioLocal.Rol}";
                return Redirect(urlDestino);
            }
            catch (SecurityTokenExpiredException)
            {
                return Unauthorized("El token de la Intranet ha expirado. Por favor, inicie sesión nuevamente.");
            }
            catch (SecurityTokenSignatureKeyNotFoundException)
            {
                return Unauthorized("Firma del token inválida. Acceso denegado.");
            }
            catch (Exception ex)
            {
                // Cualquier otro error en la validación del JWT o en BD
                return BadRequest($"Error en la autenticación SSO: {ex.Message}");
            }
        }


        // ⚠️ BORRAR ANTES DE MANDAR A PRODUCCIÓN ⚠️
        [HttpGet("GenerarPaseDev")]
        [AllowAnonymous]
        public IActionResult GenerarPaseDev()
        {
            // ... (Tu código de GenerarPaseDev se queda igual) ...
            var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "MISAEL PEREZ"),
                new System.Security.Claims.Claim("SID", "S-1-5-21-514523672-912588543-873931468-1115")
            };

            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("BaLe0n-Intranet-2026-JWT-Key-Segura-01"));
            var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: "IntranetAPI", audience: "IntranetFrontend",
                claims: claims, expires: DateTime.Now.AddHours(8),
                signingCredentials: creds
            );

            return Ok(new { token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token) });
        }
    }
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
    