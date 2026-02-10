using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProyectoVisitas.Models;

public partial class ComunitarioPerfile
{
    public int IdPerfilComunitario { get; set; }

    public int HorasTotalesDeuda { get; set; }

    public int? HorasAcumuladasActuales { get; set; }

    public string? UrlFotoRostro { get; set; }

    public string? EstatusServicio { get; set; }

    public DateOnly? FechaRegistro { get; set; }

    public string? Nombre { get; set; }

    public string? ApellidoPaterno { get; set; }

    public string? ApellidoMaterno { get; set; }

    [JsonIgnore]
    public virtual ICollection<ComunitarioAsistencia> ComunitarioAsistencia { get; set; } = new List<ComunitarioAsistencia>();

    [JsonIgnore]
    public virtual ICollection<ComunitarioEvidencia> ComunitarioEvidencia { get; set; } = new List<ComunitarioEvidencia>();
}
