using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoVisitas.Models; // Asegúrate de que coincida con tu namespace

namespace ProyectoVisitas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VisitasController : ControllerBase
    {
        private readonly BdvisitasContext _context;

        public VisitasController(BdvisitasContext context)
        {
            _context = context;
        }

        // GET: api/Visitas
        [HttpGet]
        public async Task<ActionResult<IEnumerable<VisitasBitacora>>> GetVisitasBitacoras()
        {
            return await _context.VisitasBitacoras.ToListAsync();
        }

        // GET: api/Visitas/5
        [HttpGet("{id}")]
        public async Task<ActionResult<VisitasBitacora>> GetVisitasBitacora(int id)
        {
            var visitasBitacora = await _context.VisitasBitacoras.FindAsync(id);

            if (visitasBitacora == null)
            {
                return NotFound();
            }

            return visitasBitacora;
        }

        // PUT: api/Visitas/5
        // USAR PARA: Corregir errores de dedo (nombre, motivo, etc.)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutVisitasBitacora(int id, VisitasBitacora visitasBitacora)
        {
            if (id != visitasBitacora.IdBitacoraVisitas)
            {
                return BadRequest("El ID de la URL no coincide con el del cuerpo.");
            }

            // Le decimos a Entity Framework: "Este objeto ya existe, actualiza TODO lo que traiga"
            _context.Entry(visitasBitacora).State = EntityState.Modified;

            // PROTECCIÓN: Evitamos que al editar se pierda el ID del papá o la fecha original
            // (Opcional, pero recomendado si no usas DTOs)
            _context.Entry(visitasBitacora).Property(x => x.IdBitacoraGeneral).IsModified = false;
            _context.Entry(visitasBitacora).Property(x => x.CreatedAt).IsModified = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VisitasBitacoraExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }


        // PUT: api/Visitas/5/salida
        // USAR PARA: Solo marcar que la persona ya se fue (reloj checador)
        [HttpPut("{id}/salida")]
        public async Task<IActionResult> RegistrarSalida(int id)
        {
            // 1. Buscamos la visita en la BD
            var visita = await _context.VisitasBitacoras.FindAsync(id);

            if (visita == null)
            {
                return NotFound("No se encontró la visita.");
            }

            // 2. Actualizamos SOLO la hora de salida
            // Usamos la hora del servidor para evitar trampas o errores de zona horaria del cliente
            visita.HoraSalida = TimeOnly.FromDateTime(DateTime.Now);

            // 3. Guardamos (EF Core detecta que solo cambió ese campo)
            await _context.SaveChangesAsync();

            return Ok(new { message = "Salida registrada exitosamente", horaSalida = visita.HoraSalida });
        }

        // POST: api/Visitas
        // AQUÍ ESTÁ LA LÓGICA DEL "RECEPCIONISTA INTELIGENTE"
        [HttpPost]
        public async Task<ActionResult<VisitasBitacora>> PostVisitasBitacora(VisitasBitacora visitasBitacora)
        {
            // 1. Obtenemos la fecha de hoy
            var fechaHoy = DateOnly.FromDateTime(DateTime.Now);

            // 2. Buscamos si ya existe una Bitácora General (Carpeta) para hoy
            var bitacoraDelDia = await _context.BitacoraGeneralAccesos
                .FirstOrDefaultAsync(b => b.Fecha == fechaHoy && b.EstatusGlobal == "ACTIVO");

            // 3. Si no existe, la creamos (Es la primera visita del día)
            if (bitacoraDelDia == null)
            {
                bitacoraDelDia = new BitacoraGeneralAcceso
                {
                    Fecha = fechaHoy,
                    EstatusGlobal = "ACTIVO",
                    CreatedAt = DateTime.Now
                };

                _context.BitacoraGeneralAccesos.Add(bitacoraDelDia);
                await _context.SaveChangesAsync(); // SQL genera el ID (ej: 1)
            }

            // 4. Asignamos el papá a la visita
            // Ahora la visita sabe que pertenece a la Bitácora de hoy (ej: ID 1)
            visitasBitacora.IdBitacoraGeneral = bitacoraDelDia.IdBitacoraGeneral;

            // 5. Completamos datos automáticos
            visitasBitacora.FechaVisita = fechaHoy;
            visitasBitacora.CreatedAt = DateTime.Now;

            // 6. Guardamos la visita
            _context.VisitasBitacoras.Add(visitasBitacora);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetVisitasBitacora", new { id = visitasBitacora.IdBitacoraVisitas }, visitasBitacora);
        }

        // DELETE: api/Visitas/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVisitasBitacora(int id)
        {
            var visitasBitacora = await _context.VisitasBitacoras.FindAsync(id);
            if (visitasBitacora == null)
            {
                return NotFound();
            }

            _context.VisitasBitacoras.Remove(visitasBitacora);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool VisitasBitacoraExists(int id)
        {
            return _context.VisitasBitacoras.Any(e => e.IdBitacoraVisitas == id);
        }
    }
}