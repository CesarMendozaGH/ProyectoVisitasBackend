using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ProyectoVisitas.Models; // Aquí viven BdvisitasContext y tus tablas

namespace ProyectoVisitas.Services
{
    public interface IReportesService
    {
        Task<byte[]> GenerarReporteAsistenciaComunitariaAsync(DateOnly fecha);
    }

    public class ReportesService : IReportesService
    {
        private readonly BdvisitasContext _context; // <--- CORRECCIÓN 1: Tu contexto real
        private readonly IWebHostEnvironment _env;

        public ReportesService(BdvisitasContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<byte[]> GenerarReporteAsistenciaComunitariaAsync(DateOnly fecha)
        {
            // 1. Obtener datos + INCLUIR el Perfil usando el nombre correcto "Perfil"
            var asistencias = await _context.ComunitarioAsistencias
                .Include(a => a.Perfil) // <--- CORRECCIÓN 2: Propiedad de navegación real
                .Where(a => a.FechaAsistencia == fecha)
                .OrderBy(a => a.HoraDeInicio)
                .ToListAsync();

            // 2. Ruta de tu NUEVA plantilla limpia
            string templatePath = Path.Combine(_env.WebRootPath, "Templates", "LISTA DE ASITENCIA (el nuevo).xlsx");
            FileInfo fileInfo = new FileInfo(templatePath);

            if (!fileInfo.Exists)
                throw new FileNotFoundException("La plantilla de Excel no se encontró en el servidor.", templatePath);

            // 3. Rellenar el Excel
            using (var package = new ExcelPackage(fileInfo))
            {
                var worksheet = package.Workbook.Worksheets[0];

                // Rellenar Fecha y Folio
                worksheet.Cells["J3"].Value = fecha.ToString("dd-MMM-yyyy").ToUpper();
                worksheet.Cells["J4"].Value = $"FOLIO-{fecha:MMdd}";

                // La tabla de datos ahora empieza en la fila 7
                int row = 7;
                int consecutivo = 1;

                foreach (var item in asistencias)
                {
                    worksheet.Cells[row, 1].Value = consecutivo;
                    worksheet.Cells[row, 2].Value = $"{item.Nombre} {item.ApellidoPaterno} {item.ApellidoMaterno}".Trim();
                    worksheet.Cells[row, 3].Value = item.HoraDeInicio.ToString("HH:mm");
                    worksheet.Cells[row, 4].Value = item.HoraDeSalida?.ToString("HH:mm");
                    worksheet.Cells[row, 5].Value = item.HorasACubrir;

                    // LÓGICA HORAS RESTANTES (Columna 6) - ¡Ahora sí calculará perfecto!
                    int deudaTotal = item.Perfil?.HorasTotalesDeuda ?? 0;
                    int acumuladas = item.Perfil?.HorasAcumuladasActuales ?? 0;
                    worksheet.Cells[row, 6].Value = Math.Max(0, deudaTotal - acumuladas);

                    worksheet.Cells[row, 7].Value = "SERVICIO COMUNITARIO";
                    worksheet.Cells[row, 8].Value = "BANCO DE ALIMENTOS"; // Área

                    row++;
                    consecutivo++;
                }

                return await package.GetAsByteArrayAsync();
            }
        }
    }
}