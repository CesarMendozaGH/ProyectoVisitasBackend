using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using ProyectoVisitas.Models;

namespace ProyectoVisitas.Services
{
    public interface IReportesService
    {
        Task<byte[]> GenerarReporteAsistenciaComunitariaAsync(DateOnly fecha, IFormFile? fotoFirmas = null);
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

        public async Task<byte[]> GenerarReporteAsistenciaComunitariaAsync(DateOnly fecha, IFormFile? fotoFirmas = null)
        {
            var asistencias = await _context.ComunitarioAsistencias
                .Include(a => a.Perfil)
                .Where(a => a.FechaAsistencia == fecha)
                .OrderBy(a => a.HoraDeInicio)
                .ToListAsync();

            var evidencias = await _context.ComunitarioEvidencias
                .Where(e => e.FechaCarga == fecha)
                .ToListAsync();

            string templatePath = Path.Combine(_env.WebRootPath, "Templates", "ListaDeAsistencia.xlsx");
            FileInfo fileInfo = new FileInfo(templatePath);

            if (!fileInfo.Exists)
                throw new FileNotFoundException("La plantilla de Excel no se encontró en el servidor.", templatePath);

            using (var package = new ExcelPackage(fileInfo))
            {
                var worksheet = package.Workbook.Worksheets[0];

                worksheet.Cells["D2:G2"].Merge = true;
                worksheet.Cells["D2"].Value = $"FECHA: {fecha:dd-MMM-yyyy}                                                                                         FOLIO: {fecha:MMdd}".ToUpper();
                worksheet.Cells["D2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                worksheet.Cells["D2"].Style.Font.Bold = true;

                int row = 4;
                int consecutivo = 1;

                // --- LLENADO DE LA TABLA ---
                foreach (var item in asistencias)
                {
                    worksheet.Cells[row, 1].Value = consecutivo;
                    worksheet.Cells[row, 2].Value = $"{item.Nombre} {item.ApellidoPaterno} {item.ApellidoMaterno}".Trim();
                    worksheet.Cells[row, 2].Style.Font.Bold = false;

                    worksheet.Cells[row, 3].Value = item.HoraDeInicio.ToString("HH:mm");
                    worksheet.Cells[row, 3].Style.Font.Bold = false;
                    worksheet.Cells[row, 4].Value = item.HoraDeSalida?.ToString("HH:mm");
                    worksheet.Cells[row, 4].Style.Font.Bold = false;

                    worksheet.Cells[row, 5].Value = item.HorasACubrir;
                    worksheet.Cells[row, 5].Style.Numberformat.Format = "0";
                    worksheet.Cells[row, 5].Style.Font.Bold = false;

                    worksheet.Cells[row, 6].Value = (item.Asistio == true) ? "SÍ" : "NO";
                    worksheet.Cells[row, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[row, 6].Style.Font.Bold = false;

                    worksheet.Cells[row, 7].Value = "";

                    row++;
                    consecutivo++;
                }

                // --- MAGIA 2: LA HOJA ESCANEADA (A LA DERECHA DE LA TABLA) ---
                if (fotoFirmas != null && fotoFirmas.Length > 0)
                {
                    worksheet.Cells[3, 8, 3, 12].Merge = true;
                    worksheet.Cells[3, 8].Style.Font.Bold = true;

                    using (var ms = new MemoryStream())
                    {
                        await fotoFirmas.CopyToAsync(ms);
                        ms.Position = 0;
                        var pictureFirma = worksheet.Drawings.AddPicture("FotoFirmasEscaneada_" + Guid.NewGuid(), ms);
                        pictureFirma.SetPosition(3, 10, 7, 10);
                        pictureFirma.SetSize(350, 400);
                    }
                }

                // --- MAGIA 1: LAS FOTOS DE EVIDENCIA (HASTA EL FONDO) ---
                if (evidencias.Any())
                {
                    // Buscamos cuál es la última fila que tiene texto en tu plantilla (para no tapar las firmas del final)
                    // Y le sumamos 3 filas más de margen hacia abajo, o mínimo la fila 28 si la tabla está muy vacía.
                    int ultimaFilaPlantilla = worksheet.Dimension.End.Row;
                    row = Math.Max(ultimaFilaPlantilla, 28) + 3;

                   // worksheet.Cells[row, 1].Value = "EVIDENCIAS FOTOGRÁFICAS DEL TRABAJO:";
                    worksheet.Cells[row, 1, row, 10].Merge = true;
                    worksheet.Cells[row, 1].Style.Font.Bold = true;

                    row += 1;
                    worksheet.Row(row).Height = 160; // Hacemos la fila alta para que quepan

                    int colImg = 0;
                    foreach (var ev in evidencias)
                    {
                        string urlLimpia = ev.UrlDocumento.TrimStart('/');
                        string filePath = Path.Combine(_env.WebRootPath, urlLimpia.Replace("/", "\\"));

                        if (File.Exists(filePath))
                        {
                            var picture = worksheet.Drawings.AddPicture($"Evidencia_{ev.IdEvidencias}_{Guid.NewGuid()}", new FileInfo(filePath));
                            picture.SetPosition(row - 1, 5, colImg, 5);
                            picture.SetSize(170, 170);

                            colImg += 2;

                            if (colImg >= 6)
                            {
                                colImg = 0;
                                row++;
                                worksheet.Row(row).Height = 150;
                            }
                        }
                    }
                }

                return await package.GetAsByteArrayAsync();
            }
        }
    }
}