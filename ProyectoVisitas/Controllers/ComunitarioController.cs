using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoVisitas.Models;
using ProyectoVisitas.Services;


namespace ProyectoVisitas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ComunitarioController : ControllerBase
    {
        private readonly BdvisitasContext _context;
        private readonly IReportesService _reportesService;

        public ComunitarioController(BdvisitasContext context, IReportesService reportesService)
        {
            _context = context;
            _reportesService = reportesService;
        }

        // ==========================================
        // 1. BUSCAR PERFIL (Por Nombre o Expediente)
        // ==========================================
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<ComunitarioPerfile>>> BuscarPerfil(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<ComunitarioPerfile>();

            // 1. Detectar si el usuario escribió un número (ID)
            bool esNumero = int.TryParse(query, out int idBusqueda);

            var resultados = await _context.ComunitarioPerfiles
                .Where(p =>
                    // A) Si es un número, buscamos coincidencia exacta con el ID
                    (esNumero && p.IdPerfilComunitario == idBusqueda) ||

                    // B) Siempre buscamos coincidencias en el Nombre y Apellidos
                    (p.Nombre != null && p.Nombre.Contains(query)) ||
                    (p.ApellidoPaterno != null && p.ApellidoPaterno.Contains(query)) ||
                    (p.ApellidoMaterno != null && p.ApellidoMaterno.Contains(query))
                )
                .Take(10) // Limitamos a 10 para no saturar
                .ToListAsync();

            return resultados;
        }
        // ==========================================
        // 2. CREAR NUEVO PERFIL (Alta de Infractor)
        // ==========================================
        [HttpPost("crear-perfil")]
        public async Task<ActionResult> CrearPerfil([FromBody] ComunitarioPerfile perfil)
        {
            // 1. NUEVA VALIDACIÓN: Por Nombre y Apellidos
            // Verificamos si ya existe alguien con el mismo Nombre y Apellido Paterno exactos.
            // (Opcional: puedes incluir el Materno también si quieres ser más estricto)

            bool existe = await _context.ComunitarioPerfiles
                .AnyAsync(p => p.Nombre == perfil.Nombre
                            && p.ApellidoPaterno == perfil.ApellidoPaterno
                            && p.ApellidoMaterno == perfil.ApellidoMaterno); // Opcional

            if (existe)
            {
                return BadRequest($"Ya existe un perfil registrado a nombre de {perfil.Nombre} {perfil.ApellidoPaterno}.");
            }

            // 2. Inicializar valores (Igual que antes)
            perfil.HorasAcumuladasActuales = 0;
            perfil.FechaRegistro = DateOnly.FromDateTime(DateTime.Now);
            perfil.EstatusServicio = "ACTIVO";

            _context.ComunitarioPerfiles.Add(perfil);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Perfil creado exitosamente", idPerfil = perfil.IdPerfilComunitario });
        }


        //PRUEBAS DE DATA OBJECT TRANSFER (DTO's)
        // Esta clase es solo "la cajita" para recibir los datos limpios
        public class EntradaDto
        {
            public int PerfilId { get; set; }
            public int HorasACubrir { get; set; }
        }
        // ==========================================
        // 3. REGISTRAR ASISTENCIA (La Magia del Espejo)
        // ==========================================
        // POST: api/Comunitario/registrar-entrada
        [HttpPost("registrar-entrada")]
        public async Task<IActionResult> RegistrarEntrada([FromBody] EntradaDto datos) 
        {
            // A. Validaciones usando el DTO
            if (datos.PerfilId <= 0) return BadRequest("ID de perfil inválido.");
            if (datos.HorasACubrir <= 0) return BadRequest("Las horas a cubrir deben ser mayor a 0.");

            //VALIDACION ANTI DUPLICADOS
            DateOnly fechaHoy = DateOnly.FromDateTime(DateTime.Now);

            //si ya tiene una entrada en el dia y no ha registrado salida, no puede registrar otra entrada
            bool yaEstaAdentro = await _context.ComunitarioAsistencias
                .AnyAsync(a => a.PerfilId == datos.PerfilId 
                            && a.FechaAsistencia == fechaHoy 
                            && a.HoraDeSalida == null);

            if (yaEstaAdentro)
            {
                return BadRequest("Ya existe una entrada abierta para este perfil el día de hoy. Por favor registre la salida antes de intentar nuevamente.");
            }
            // B. Lógica de Bitácora (Igual que antes...)
           
            var bitacoraDia = await _context.BitacoraGeneralAccesos.FirstOrDefaultAsync(b => b.Fecha == fechaHoy);

            if (bitacoraDia == null)
            {
                bitacoraDia = new BitacoraGeneralAcceso { Fecha = fechaHoy, EstatusGlobal = "ABIERTA", CreatedAt = DateTime.Now };
                _context.BitacoraGeneralAccesos.Add(bitacoraDia);
                await _context.SaveChangesAsync();
            }

            // C. CREAR LA ENTIDAD REAL (Aquí pasamos del DTO a la Base de Datos)
            var asistencia = new ComunitarioAsistencia // <--- 2. CAMBIO AQUÍ
            {
                PerfilId = datos.PerfilId,         // Tomado del DTO
                HorasACubrir = datos.HorasACubrir, // Tomado del DTO

                // Todo lo demás automático
                IdBitacoraGeneral = bitacoraDia.IdBitacoraGeneral,
                FechaAsistencia = fechaHoy,
                HoraDeInicio = TimeOnly.FromDateTime(DateTime.Now),
                HoraDeSalida = null,
                Asistio = true,
                CreatedAt = DateTime.Now
            };

            // D. Espejo de Nombres (Igual que antes...)
            var perfil = await _context.ComunitarioPerfiles.FindAsync(datos.PerfilId);
            if (perfil == null) return NotFound("Perfil no encontrado.");

            asistencia.Nombre = perfil.Nombre;
            asistencia.ApellidoPaterno = perfil.ApellidoPaterno;
            asistencia.ApellidoMaterno = perfil.ApellidoMaterno;

            _context.ComunitarioAsistencias.Add(asistencia);
            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Entrada registrada exitosamente",
                idAsistencia = asistencia.IdAsistenciasComunitarias,
                bitacoraDiaId = asistencia.IdBitacoraGeneral, // Para confirmar que lo asignó
                nombre = $"{asistencia.Nombre} {asistencia.ApellidoPaterno}",
                horasProgramadas = asistencia.HorasACubrir
            });
        }
        //JSON DE PRUEBA Y RESPUESTA
        /*
         {
          "perfilId": 5,           // El ID del chavo
          "horasACubrir": 4        // MANUAL: "Hoy va a pagar 4 horas"
            }
         */


        
        // PUT: api/Comunitario/check-out-por-perfil/{perfilId}
        [HttpPut("check-out-por-perfil/{perfilId}")]
        public async Task<IActionResult> CheckOutPorPerfil(int perfilId)
        {
            // 1. BUSCAR LA VISITA ABIERTA DE HOY
            // Buscamos: 
            // - Que sea de este Perfil
            // - Que sea fecha de HOY
            // - Que NO tenga hora de salida (o sea, que siga adentro)
            DateOnly hoy = DateOnly.FromDateTime(DateTime.Now);

            var asistencia = await _context.ComunitarioAsistencias
                .Where(a => a.PerfilId == perfilId
                         && a.FechaAsistencia == hoy
                         && a.HoraDeSalida == null)
                .FirstOrDefaultAsync();

            if (asistencia == null)
            {
                return NotFound($"No se encontró una entrada abierta para el Perfil {perfilId} el día de hoy.");
            }

            // 2. MARCAR SALIDA
            TimeOnly horaActual = TimeOnly.FromDateTime(DateTime.Now);
            asistencia.HoraDeSalida = horaActual;

            // 3. CALCULAR HORAS REALES TRABAJADAS
            // Restamos Salida - Entrada
            TimeSpan duracion = horaActual - asistencia.HoraDeInicio;
            int horasRealizadas = (int)duracion.TotalHours;

            // Ojo: Aquí NO sobreescribimos 'HorasACubrir' porque ese fue el compromiso manual al llegar.
            // Si quieres saber cuánto CUMPLIÓ vs cuánto PROMETIÓ, podrías guardar esto en otro campo, 
            // pero por ahora solo sumamos lo real al acumulado.

            if (horasRealizadas < 0) horasRealizadas = 0; // Protección por si hay error de reloj

            // 4. ACTUALIZAR AL PERFIL (Abonar a la deuda)
            var perfil = await _context.ComunitarioPerfiles.FindAsync(perfilId);
            if (perfil != null)
            {
                perfil.HorasAcumuladasActuales = (perfil.HorasAcumuladasActuales ?? 0) + horasRealizadas;
                _context.ComunitarioPerfiles.Update(perfil);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Salida registrada exitosamente",
                nombre = $"{asistencia.Nombre} {asistencia.ApellidoPaterno}",
                horaSalida = horaActual,
                horasSumadas = horasRealizadas,
                totalAcumulado = perfil?.HorasAcumuladasActuales
            });
        }


        // ==========================================
        // 4. SUBIR EVIDENCIA (Opcional por ahora)
        // ==========================================

        public class EvidenciaDto
        {
            public int PerfilId { get; set; }
            public IFormFile Archivo { get; set; }
            public DateOnly? FechaDelTrabajo { get; set; } // <--- EL NUEVO CAMPO QUE FALTABA
        }

        [HttpPost("subir-evidencia")]
        public async Task<IActionResult> SubirEvidencia([FromForm] EvidenciaDto datos)
        {
            if (datos.Archivo == null || datos.Archivo.Length == 0)
                return BadRequest("No se ha enviado ningún archivo.");

            var perfil = await _context.ComunitarioPerfiles.FindAsync(datos.PerfilId);
            if (perfil == null) return NotFound("El perfil indicado no existe.");

            var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "evidencias");
            if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);

            string extension = Path.GetExtension(datos.Archivo.FileName);
            string nombreArchivo = $"evidencia_{datos.PerfilId}_{Guid.NewGuid()}{extension}";
            string rutaCompleta = Path.Combine(carpeta, nombreArchivo);

            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
            {
                await datos.Archivo.CopyToAsync(stream);
            }

            // Magia: Si mandan fecha usamos esa, si no, usamos la de hoy por defecto.
            DateOnly fechaCarga = datos.FechaDelTrabajo ?? DateOnly.FromDateTime(DateTime.Now);

            var nuevaEvidencia = new ComunitarioEvidencia
            {
                PerfilId = datos.PerfilId,
                UrlDocumento = "/evidencias/" + nombreArchivo,
                FechaCarga = fechaCarga // <--- GUARDAMOS LA FECHA REAL
            };

            _context.ComunitarioEvidencias.Add(nuevaEvidencia);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Evidencia subida correctamente", url = nuevaEvidencia.UrlDocumento });
        }


        // ==========================================
        // 5. OBTENER TODOS LOS PERFILES (Para listas)
        // ==========================================
        // GET: api/Comunitario/todos
        [HttpGet("todos")]
        public async Task<ActionResult<IEnumerable<ComunitarioPerfile>>> ObtenerTodos()
        {
            // Usamos ToListAsync para traer todo.
            // Opcional: Puedes agregar .OrderBy(p => p.ApellidoPaterno) si los quieres ordenados
            return await _context.ComunitarioPerfiles
                                 .OrderByDescending(p => p.IdPerfilComunitario) // Muestra los más nuevos primero
                                 .ToListAsync();
        }


        // ==========================================
        // 6. MODIFICAR PERFIL (Corregir datos)
        // ==========================================
        // PUT: api/Comunitario/modificar-perfil/{id}
        [HttpPut("modificar-perfil/{id}")]
        public async Task<IActionResult> ModificarPerfil(int id, [FromBody] ComunitarioPerfile perfilModificado)
        {
            if (id != perfilModificado.IdPerfilComunitario)
                return BadRequest("El ID de la URL no coincide con el cuerpo.");

            var perfilExistente = await _context.ComunitarioPerfiles.FindAsync(id);
            if (perfilExistente == null) return NotFound("Perfil no encontrado.");

            // Actualizamos solo los campos permitidos
            perfilExistente.Nombre = perfilModificado.Nombre;
            perfilExistente.ApellidoPaterno = perfilModificado.ApellidoPaterno;
            perfilExistente.ApellidoMaterno = perfilModificado.ApellidoMaterno;
            perfilExistente.HorasTotalesDeuda = perfilModificado.HorasTotalesDeuda;
            //por cualquier cosa requerida
            perfilExistente.HorasAcumuladasActuales = perfilModificado.HorasAcumuladasActuales;
            // Opcional: Si quieres permitir cambiar la foto también
            perfilExistente.UrlFotoRostro = perfilModificado.UrlFotoRostro;


            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Perfil actualizado correctamente." });
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, "Error de concurrencia al actualizar.");
            }
        }

        // ==========================================
        // 7. DESACTIVAR / REACTIVAR (Borrado Lógico)
        // ==========================================
        // PUT: api/Comunitario/cambiar-estatus/{id}
        [HttpPut("cambiar-estatus/{id}")]
        public async Task<IActionResult> CambiarEstatus(int id)
        {
            var perfil = await _context.ComunitarioPerfiles.FindAsync(id);
            if (perfil == null) return NotFound("Perfil no encontrado.");

            // Switch simple: Si es ACTIVO pasa a INACTIVO y viceversa
            if (perfil.EstatusServicio == "ACTIVO")
            {
                perfil.EstatusServicio = "INACTIVO";
            }
            else
            {
                perfil.EstatusServicio = "ACTIVO";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"El perfil ahora está {perfil.EstatusServicio}",
                nuevoEstatus = perfil.EstatusServicio
            });
        }

        // ==========================================
        // 8. GENERAR REPORTE EXCEL (Con Evidencias y Firmas)
        // ==========================================
        public class GenerarReporteDto
        {
            public DateOnly? Fecha { get; set; }
            public IFormFile? FotoFirmas { get; set; } // Opcional (por si un día quieres el excel sin firmas)
        }

        [HttpPost("generar-reporte")]
        public async Task<IActionResult> GenerarReporteDiario([FromForm] GenerarReporteDto datos)
        {
            try
            {
                DateOnly fechaFiltro = datos.Fecha ?? DateOnly.FromDateTime(DateTime.Today);

                // Le mandamos la fecha y la foto al servicio
                byte[] excelBytes = await _reportesService.GenerarReporteAsistenciaComunitariaAsync(fechaFiltro, datos.FotoFirmas);

                string fileName = $"Reporte_Asistencia_{fechaFiltro:dd_MM_yyyy}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al generar el reporte: {ex.Message}");
            }
        }

    }
}
