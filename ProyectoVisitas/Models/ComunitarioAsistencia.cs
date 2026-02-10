using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProyectoVisitas.Models;

public partial class ComunitarioAsistencia
{
    public int IdAsistenciasComunitarias { get; set; }

    public int? PerfilId { get; set; }

    public int IdBitacoraGeneral { get; set; }

    public DateOnly FechaAsistencia { get; set; }

    public string? Nombre { get; set; }

    public string? ApellidoPaterno { get; set; }

    public string? ApellidoMaterno { get; set; }

    public TimeOnly HoraDeInicio { get; set; }

    public TimeOnly? HoraDeSalida { get; set; }

    public int HorasACubrir { get; set; }

    public bool? Asistio { get; set; }

    public DateTime? CreatedAt { get; set; }

    [JsonIgnore]
    public virtual BitacoraGeneralAcceso? IdBitacoraGeneralNavigation { get; set; } = null!;

    [JsonIgnore]
    public virtual ComunitarioPerfile? Perfil { get; set; }
}
