using Microsoft.EntityFrameworkCore;
using OfficeOpenXml; 
using ProyectoVisitas.Models;
using ProyectoVisitas.Services;

var builder = WebApplication.CreateBuilder(args);

ExcelPackage.License.SetNonCommercialOrganization("ProyectoVisitas");

// Base de datos
builder.Services.AddDbContext<BdvisitasContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CadenaSQL")));

// 1. AGREGAR EL SERVICIO CORS AQUÍ
builder.Services.AddCors(options =>
{
    options.AddPolicy("NuevaPolitica", app => // Le cambié el nombre a algo simple para evitar errores
    {
        app.AllowAnyOrigin() // DANGER: Para pruebas, permite TODO. Luego lo restringimos.
           .AllowAnyHeader()
           .AllowAnyMethod();
    });
});

// Servicios
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//builder para excel
builder.Services.AddScoped<IReportesService, ReportesService>();

var app = builder.Build();



// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseHttpsRedirection();

// 2. USAR EL CORS AQUÍ (OJO: Antes de Authorization y MapControllers)
app.UseCors("NuevaPolitica"); 

app.UseAuthorization();
app.MapControllers();

app.UseStaticFiles(); // Esto permite a .NET exponer la carpeta wwwroot al exterior

app.Run();