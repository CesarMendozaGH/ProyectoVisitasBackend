using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoVisitas.Models;

namespace ProyectoVisitas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReservasController : ControllerBase
    {

        private readonly BdvisitasContext _context;

        public ReservasController(BdvisitasContext context)
        {
            _context = context;
        }

        // GET: api/Reservas
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Reserva>>> GetReservas()
        {
            // Incluimos datos del Espacio para que el front sepa cuál es
            return await _context.Reservas
                .Include(r => r.Espacio)
                .ToListAsync();
        }

        // GET: api/Reservas/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Reserva>> GetReserva(int id)
        {
            var reserva = await _context.Reservas.FindAsync(id);

            if (reserva == null)
            {
                return NotFound();
            }

            return reserva;
        }

        //INGRESAR RESERVAS 
        // POST: api/Reservas
        [HttpPost]
        public async Task<ActionResult<Reserva>> PostReserva(Reserva reserva)
        {
            // --- VALIDACIÓN 1: Fechas lógicas ---
            if (reserva.FechaInicio >= reserva.FechaFin)
            {
                return BadRequest("La fecha de fin debe ser posterior a la de inicio.");
            }

            // --- VALIDACIÓN 2: DETECTOR DE CONFLICTOS (Overlapping) ---
            // Buscamos si hay OTRA reserva en el MISMO espacio que CHOCA con estas horas
            // Fórmula de colisión: (InicioA < FinB) y (FinA > InicioB)
            bool estaOcupado = await _context.Reservas.AnyAsync(r =>
                r.EspacioId == reserva.EspacioId &&
                r.EstatusReserva == true && // Solo contamos las activas
                r.FechaInicio < reserva.FechaFin &&
                r.FechaFin > reserva.FechaInicio
            );

            if (estaOcupado)
            {
                return Conflict("El espacio seleccionado ya está ocupado en ese horario.");
            }

            // --- LÓGICA DE BITÁCORA (FUTURA) ---
            // OJO: Aquí usamos la fecha de la RESERVA, no la de HOY.
            var fechaReserva = DateOnly.FromDateTime(reserva.FechaInicio);

            //Aqui comparamos si ya hay una bitacora existente el dia de hoy y si no la hay crea una nueva 
            var bitacoraFutura = await _context.BitacoraGeneralAccesos
                .FirstOrDefaultAsync(b => b.Fecha == fechaReserva && b.EstatusGlobal == "ACTIVO");

            if (bitacoraFutura == null)
            {
                bitacoraFutura = new BitacoraGeneralAcceso
                {
                    Fecha = fechaReserva,
                    EstatusGlobal = "ACTIVO",
                    CreatedAt = DateTime.Now
                };
                _context.BitacoraGeneralAccesos.Add(bitacoraFutura);
                await _context.SaveChangesAsync();
            }

            // Asignamos el papá encontrado/creado
            reserva.IdBitacoraGeneral = bitacoraFutura.IdBitacoraGeneral;

            // Completamos datos
            reserva.EstatusReserva = true; // Por defecto nace confirmada
            reserva.CreatedAt = DateTime.Now;

            _context.Reservas.Add(reserva);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetReserva", new { id = reserva.IdReserva }, reserva);
        }

        // PUT: api/Reservas/5 (Para cancelar o reprogramar)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutReserva(int id, Reserva reserva)
        {
            if (id != reserva.IdReserva)
            {
                return BadRequest();
            }

            // --- VALIDACIÓN 1: Fechas lógicas en EDICIÓN ---
            if (reserva.FechaInicio >= reserva.FechaFin)
            {
                return BadRequest("La fecha de fin debe ser posterior a la de inicio.");
            }

            // --- VALIDACIÓN 2: Evitar conflictos al editar (Opcional pero recomendado) ---
            bool estaOcupado = await _context.Reservas.AnyAsync(r =>
                r.EspacioId == reserva.EspacioId &&
                r.IdReserva != id && // Excluimos la reserva actual para que no choque consigo misma
                r.EstatusReserva == true &&
                r.FechaInicio < reserva.FechaFin &&
                r.FechaFin > reserva.FechaInicio
            );

            if (estaOcupado)
            {
                return Conflict("El espacio seleccionado ya está ocupado en ese horario.");
            }

            _context.Entry(reserva).State = EntityState.Modified;
            _context.Entry(reserva).Property(x => x.IdBitacoraGeneral).IsModified = false;
            _context.Entry(reserva).Property(x => x.CreatedAt).IsModified = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReservaExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        //METODO PARA INGRESAR VARIOS VISITANTES

        [HttpPost("{id}/asistentes")]
        public async Task<IActionResult> AgregarAsistentesMasivos(int id, [FromBody] List<ReservasListaAsistente> listaAsistentes)
        {
            if (listaAsistentes == null || listaAsistentes.Count == 0)
            {
                return BadRequest("La lista de asistentes esta vacia");
            }

            // Buscamos la reserva completa para poder leer su FechaFin
            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null) return NotFound("La reserva no existe");

            // 🔒 CANDADO BACKEND: Evitar agregar asistentes si la junta ya terminó
            if (reserva.FechaFin < DateTime.Now)
            {
                return BadRequest("La reserva ya finalizó. No se pueden agregar nuevos asistentes a la lista oficial.");
            }

            foreach (var asistente in listaAsistentes)
            {
                asistente.IdReservaFk = id;
                asistente.IdLista = 0;
                asistente.Asistio = false;
                asistente.CreatedAt = DateTime.Now;
            }

            await _context.ReservasListaAsistentes.AddRangeAsync(listaAsistentes);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Se guardaron {listaAsistentes.Count} asistentes correctamente." });
        }

        //Rellenar lista de asistencia 
        [HttpGet("{id}/asistentes")]
        public async Task<ActionResult<IEnumerable<ReservasListaAsistente>>> GetAsistentesPorReserva(int id)
        {
            // 1. Buscamos en la tabla de Asistentes filtrando por el ID de la Reserva
            var listaAsistentes = await _context.ReservasListaAsistentes
                                                .Where(a => a.IdReservaFk == id)
                                                .ToListAsync();

            // 2. Si no hay nadie, devolvemos una lista vacía (es mejor que un error 404 para las tablas)
            if (listaAsistentes == null)
            {
                return new List<ReservasListaAsistente>();
            }

            return listaAsistentes;
        }

        //PASAR LISTA (TEST) GUARDA UNO POR UNO 
        // PUT: api/Reservas/Asistentes/5/checkin
        // Sirve para: Marcar que "Juan Perez" ya llegó (Toggle)
        [HttpPut("Asistentes/{idAsistente}/checkin")]
        public async Task<IActionResult> MarcarAsistencia(int idAsistente)
        {
            var asistente = await _context.ReservasListaAsistentes.FindAsync(idAsistente);
            if (asistente == null) return NotFound("Asistente no encontrado");

            var reserva = await _context.Reservas.FindAsync(asistente.IdReservaFk);

            // 🔒 CANDADO BACKEND: No se puede cambiar la asistencia si ya terminó
            if (reserva != null && reserva.FechaFin < DateTime.Now)
            {
                return BadRequest("La reserva ya finalizó. El registro de asistencia quedó sellado y no puede modificarse.");
            }

            asistente.Asistio = !asistente.Asistio;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = asistente.Asistio == true ? "Asistencia confirmada" : "Asistencia cancelada",
                estadoActual = asistente.Asistio
            });
        }

        // PUT: api/Reservas/Asistentes/checkin-masivo
        // Sirve para: Marcar asistencia a MULTIPLES personas de un solo golpe
        [HttpPut("Asistentes/checkin-masivo")]
        public async Task<IActionResult> CheckInMasivo([FromBody] List<int> idsAsistentesConfirmados)
        {
            if (idsAsistentesConfirmados == null || idsAsistentesConfirmados.Count == 0)
            {
                return BadRequest("No seleccionaste a nadie.");
            }

            var asistentes = await _context.ReservasListaAsistentes
                                           .Where(a => idsAsistentesConfirmados.Contains(a.IdLista))
                                           .ToListAsync();

            if (asistentes.Count == 0)
            {
                return NotFound("No se encontraron asistentes con esos IDs.");
            }

            // Buscamos la reserva a la que pertenecen (tomamos el ID del primer asistente)
            var idReserva = asistentes.First().IdReservaFk;
            var reserva = await _context.Reservas.FindAsync(idReserva);

            // 🔒 CANDADO BACKEND
            if (reserva != null && reserva.FechaFin < DateTime.Now)
            {
                return BadRequest("La reserva ya finalizó. El registro de asistencia quedó sellado y no puede modificarse.");
            }

            foreach (var asistente in asistentes)
            {
                asistente.Asistio = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Se marcó asistencia a {asistentes.Count} personas." });
        }



        // DELETE: api/Reservas/Asistentes/5
        // Sirve para: Eliminar a un asistente, excepto si la junta ya terminó.
        [HttpDelete("Asistentes/{idAsistente}")]
        public async Task<IActionResult> EliminarAsistente(int idAsistente)
        {
            var asistente = await _context.ReservasListaAsistentes.FindAsync(idAsistente);
            if (asistente == null)
            {
                return NotFound("El asistente no existe o ya fue eliminado.");
            }

            // Buscamos a qué reserva pertenece este asistente
            var reserva = await _context.Reservas.FindAsync(asistente.IdReservaFk);

            // 🔒 CANDADO BACKEND: Validamos si la fecha y hora de la reserva ya pasó
            if (reserva != null && reserva.FechaFin < DateTime.Now)
            {
                return BadRequest("La reserva ya finalizó. La lista de asistencia es oficial y no puede ser modificada.");
            }

            _context.ReservasListaAsistentes.Remove(asistente);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Asistente eliminado correctamente." });
        }

        // DELETE: api/Reservas/5/Cancelacion
        [HttpDelete("{id}/Cancelacion")]
        public async Task<IActionResult> CancelarReserva(int id)
        {
            var reserva = await _context.Reservas.FindAsync(id);

            if (reserva == null)
            {
                return NotFound("La reserva no existe o ya fue eliminada.");
            }

            if (reserva.FechaFin < DateTime.Now)
            {
                return BadRequest("No puedes cancelar una reserva que ya finalizó.");
            }

            reserva.EstatusReserva = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Reserva cancelada correctamente." });
        }


        private bool ReservaExists(int id)
        {
            return _context.Reservas.Any(e => e.IdReserva == id);
        }
    }
}
