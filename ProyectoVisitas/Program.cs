using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml; 
using ProyectoVisitas.Models;
using ProyectoVisitas.Services;
using System.Text;

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

//BUILDER JWT: Configura la autenticación JWT con los parámetros de validación
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true, // Revisa automáticamente que no hayan pasado los 60 minutos
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

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


app.UseAuthentication(); // 1. Valida el token

app.UseAuthorization(); // 2. Valida los roles/claims del usuario (si el token es válido, sino ni llega aquí)

app.MapControllers();

app.UseStaticFiles(); // Esto permite a .NET exponer la carpeta wwwroot al exterior

app.Run();