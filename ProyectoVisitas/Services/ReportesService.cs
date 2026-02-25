using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using ProyectoVisitas.Models;

namespace ProyectoVisitas.Services
{
    public interface IReportesService
    {
        Task<byte[]> GenerarReporteAsistenciaComunitariaAsync(DateOnly fecha);
    }

    public class ReportesService : IReportesService
    {
        private readonly BdvisitasContext _context;
        private readonly IWebHostEnvironment _env;

        public ReportesService(BdvisitasContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<byte[]> GenerarReporteAsistenciaComunitariaAsync(DateOnly fecha)
        {
            var asistencias = await _context.ComunitarioAsistencias
                .Include(a => a.Perfil)
                .Where(a => a.FechaAsistencia == fecha)
                .OrderBy(a => a.HoraDeInicio)
                .ToListAsync();

            string templatePath = Path.Combine(_env.WebRootPath, "Templates", "ListaDeAsistencia.xlsx");
            FileInfo fileInfo = new FileInfo(templatePath);

            if (!fileInfo.Exists)
                throw new FileNotFoundException("La plantilla de Excel no se encontró en el servidor.", templatePath);

            using (var package = new ExcelPackage(fileInfo))
            {
                var worksheet = package.Workbook.Worksheets[0];

                // 1. FECHA Y FOLIO EN LA FILA 2 (Para NO aplastar los encabezados de la fila 3)
                // Usamos tu excelente idea de combinar el texto en una sola celda ancha
                worksheet.Cells["D2:G2"].Merge = true;
                worksheet.Cells["D2"].Value = $"FECHA: {fecha:dd-MMM-yyyy}                                                                                         FOLIO: {fecha:MMdd}".ToUpper();
                worksheet.Cells["D2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right; // Alineado a la derecha
                worksheet.Cells["D2"].Style.Font.Bold = true;

                // 2. LA TABLA EMPIEZA EN LA FILA 4
                int row = 4;
                int consecutivo = 1;

                foreach (var item in asistencias)
                {
                    // Col 1 y 2: ID y Nombre
                    worksheet.Cells[row, 1].Value = consecutivo;
                    worksheet.Cells[row, 2].Value = $"{item.Nombre} {item.ApellidoPaterno} {item.ApellidoMaterno}".Trim();
                    worksheet.Cells[row, 2].Style.Font.Bold = false;

                    // Col 3 y 4: Horarios
                    worksheet.Cells[row, 3].Value = item.HoraDeInicio.ToString("HH:mm");
                    worksheet.Cells[row, 3].Style.Font.Bold = false;
                    worksheet.Cells[row, 4].Value = item.HoraDeSalida?.ToString("HH:mm");
                    worksheet.Cells[row, 4].Style.Font.Bold = false;

                    // Col 5: Horas a Cubrir (Asegurándonos que sea un número)
                    worksheet.Cells[row, 5].Value = item.HorasACubrir;
                    worksheet.Cells[row, 5].Style.Numberformat.Format = "0";
                    worksheet.Cells[row, 5].Style.Font.Bold = false;

                    // Col 6: ASISTIÓ (En el formato nuevo esta es la columna 6)
                    worksheet.Cells[row, 6].Value = (item.Asistio == true) ? "SÍ" : "NO";
                    worksheet.Cells[row, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[row, 6].Style.Font.Bold = false;

                    // Col 7: FIRMA (La dejamos completamente en blanco para que firmen con pluma)
                    worksheet.Cells[row, 7].Value = "";

                    // Ya no escribimos NADA en la columna 8 porque no existe en este diseño

                    row++;
                    consecutivo++;
                }

                return await package.GetAsByteArrayAsync();
            }
        }
    }
}