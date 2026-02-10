using Microsoft.EntityFrameworkCore;
using ProyectoVisitas.Models;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 2. USAR EL CORS AQUÍ (OJO: Antes de Authorization y MapControllers)
app.UseCors("NuevaPolitica"); 

app.UseAuthorization();
app.MapControllers();

app.Run();