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
        // La URL donde vive tu React en local o producción
        private readonly string _reactAppUrl = "http://localhost:5173";

        public AuthController(BdvisitasContext context)
        {
            _context = context;
        }

        // 🚨 EL NUEVO PUNTO DE ENTRADA DESDE LA INTRANET 🚨
        // Recibe el POST de formulario (application/x-www-form-urlencoded)
        [HttpPost("SSORedirect")]
        [AllowAnonymous] // Permitimos entrar porque aquí mismo validaremos el token
        public async Task<IActionResult> SSORedirect([FromForm] string access_token, [FromForm] string sid)
        {
            try
            {
                if (string.IsNullOrEmpty(access_token) || string.IsNullOrEmpty(sid))
                {
                    return BadRequest("Datos incompletos desde la Intranet.");
                }

                // 1. ABRIR Y VALIDAR EL TOKEN QUE MANDÓ LA INTRANET
                var handler = new JwtSecurityTokenHandler();

                // (Opcional) Aquí deberías validar la firma del token con la llave secreta, 
                // pero por ahora confiaremos en que el token viene de la Intranet
                var jwtToken = handler.ReadJwtToken(access_token);

                // Extraemos el nombre del token (asumiendo que viene como unique_name o Name)
                var nombre = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value ??
                             jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ??
                             "Usuario Desconocido";

                // 2. JIT PROVISIONING: CREAR O ACTUALIZAR USUARIO EN BD
                var usuarioLocal = await _context.UsuariosWebs.FirstOrDefaultAsync(u => u.SSID == sid);

                if (usuarioLocal == null)
                {
                    usuarioLocal = new UsuarioWeb
                    {
                        SSID = sid,
                        NombreCompleto = nombre,
                        // Asignamos SUPERADMIN temporalmente si es el SID de Misael, sino NORMAL
                        Rol = (sid == "S-1-5-21-514523672-912588543-873931468-1115") ? "SUPERADMIN" : "USUARIO_NORMAL",
                        FechaRegistro = DateTime.Now
                    };
                    _context.UsuariosWebs.Add(usuarioLocal);
                    await _context.SaveChangesAsync();
                }

                // 3. LA MAGIA: REDIRIGIR A REACT PASANDO EL TOKEN
                // Redirigimos al navegador del usuario hacia tu React, y le pasamos el token en la URL
                // para que React lo atrape, lo guarde y lo empiece a usar.
                string urlDestino = $"{_reactAppUrl}/auth/callback?token={access_token}&rol={usuarioLocal.Rol}";
                return Redirect(urlDestino);
            }
            catch (Exception ex)
            {
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
    