using Microsoft.EntityFrameworkCore;
using ProyectoVisitas.Models; // Asegúrate de que esto apunte a tu namespace de modelos

namespace ProyectoVisitas.Services
{
    // Heredamos de BackgroundService para que .NET sepa que esto corre en el fondo
    public class CierreAutomaticoService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CierreAutomaticoService> _logger; 

        // Inyectamos el IServiceProvider porque los BackgroundServices son "Singletons" (viven por siempre),
        // pero nuestro DbContext es "Scoped" (vive por petición). Necesitamos crear un scope manual.
        public CierreAutomaticoService(IServiceProvider serviceProvider, ILogger<CierreAutomaticoService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servicio de cierre automático de visitas iniciado.");

            // Este ciclo infinito se ejecutará mientras la API esté encendida
            while (!stoppingToken.IsCancellationRequested)
            {
                var ahora = DateTime.Now;

                // Si son exactamente las 17:30 (5:30 PM)
                if (ahora.Hour == 17 && ahora.Minute == 30)
                {
                    _logger.LogInformation("Son las 17:30. Iniciando barrido de visitas sin salida...");

                    // Creamos un "espacio de trabajo" para poder usar la Base de Datos
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<BdvisitasContext>();
                        var hoy = DateOnly.FromDateTime(ahora);

                        // Buscamos todas las visitas de hoy que sigan adentro (HoraSalida es null)
                        var visitasFantasmas = await context.VisitasBitacoras
                            .Where(v => v.FechaVisita == hoy && v.HoraSalida == null)
                            .ToListAsync(stoppingToken);

                        if (visitasFantasmas.Any())
                        {
                            foreach (var visita in visitasFantasmas)
                            {
                                // Les forzamos la salida a las 17:30 exactas
                                visita.HoraSalida = new TimeOnly(17, 30, 0);
                            }

                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Se cerraron automáticamente {visitasFantasmas.Count} visitas.");
                        }
                    }

                    // Esperamos 61 segundos para asegurar que no se vuelva a ejecutar 
                    // múltiples veces dentro del mismo minuto 17:30
                    await Task.Delay(TimeSpan.FromSeconds(61), stoppingToken);
                }

                // Si no son las 17:30, el velador se duerme 30 segundos y vuelve a revisar el reloj
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}