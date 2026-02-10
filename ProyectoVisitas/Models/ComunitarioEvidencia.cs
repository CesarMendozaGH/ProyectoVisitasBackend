using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProyectoVisitas.Models;

public partial class ComunitarioEvidencia
{
    public int IdEvidencias { get; set; }

    public int? PerfilId { get; set; }

    public string UrlDocumento { get; set; } = null!;

    public DateOnly? FechaCarga { get; set; }

    [JsonIgnore]
    public virtual ComunitarioPerfile? Perfil { get; set; }
}
