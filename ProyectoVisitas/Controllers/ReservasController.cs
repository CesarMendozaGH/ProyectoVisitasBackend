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

            // OJO: Si cambian la fecha aquí, deberías volver a correr la validación de conflictos.
            // Por simplicidad, aquí solo guardamos.

            _context.Entry(reserva).State = EntityState.Modified;

            // Protegemos campos críticos
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

        // Cancelacion CancelacionBinaria
        [HttpDelete("{id}/Cancelacion")]
        public async Task<IActionResult> Cancelar(int id)
        {
            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null)
            {
                return NotFound();
            }

            
            reserva.EstatusReserva = false;


            await _context.SaveChangesAsync();

            return NoContent();
        }

        //METODO PARA INGRESAR VARIOS VISITANTES

        [HttpPost("{id}/asistentes")]
        public async Task<IActionResult> AgregarAsistentesMasivos(int id, [FromBody] List<ReservasListaAsistente> listaAsistentes)
        {

            //Validacion de lista vacia
            if (listaAsistentes == null || listaAsistentes.Count == 0) {
                return BadRequest("La lista de asistentes esta vacia");
            }

            //Verificar resera
            var reservaExiste = await _context.Reservas.AnyAsync(r=> r.IdReserva == id);
              if(!reservaExiste) return NotFound("La reserva no existe");

            // 3. PREPARACIÓN DE DATOS
            // Como el Front manda el objeto "crudo", nosotros completamos lo que falta
            foreach (var asistente in listaAsistentes)
            {
                // El front NO sabe el ID de la reserva (o puede mentir), 
                // así que lo forzamos con el ID de la URL
                asistente.IdReservaFk = id;

                asistente.IdLista = 0; // Ignoramos cualquier ID que manden
                asistente.Asistio = false; // Nace como "No ha llegado"
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

            // Invertimos el valor: Si era false pasa a true, y viceversa (por si se equivocaron)
            asistente.Asistio = !asistente.Asistio;

            // Guardamos solo ese cambio
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

            // 1. Buscamos en la BD todos los asistentes cuyo ID esté en la lista que mandaste
            // SQL equivalente: WHERE idLista IN (1, 5, 8, ...)
            var asistentes = await _context.ReservasListaAsistentes
                                           .Where(a => idsAsistentesConfirmados.Contains(a.IdLista))
                                           .ToListAsync();

            if (asistentes.Count == 0)
            {
                return NotFound("No se encontraron asistentes con esos IDs.");
            }

            // 2. Recorremos los resultados y les ponemos Asistio = true
            foreach (var asistente in asistentes)
            {
                asistente.Asistio = true;
            }

            // 3. Guardamos TODO de un solo golpe
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Se marcó asistencia a {asistentes.Count} personas." });
        }

        private bool ReservaExists(int id)
        {
            return _context.Reservas.Any(e => e.IdReserva == id);
        }
    }
}
