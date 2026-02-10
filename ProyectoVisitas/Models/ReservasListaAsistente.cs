using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProyectoVisitas.Models;

public partial class ReservasListaAsistente
{
    public int IdLista { get; set; }

    public int IdReservaFk { get; set; }

    public string Nombre { get; set; } = null!;

    public string ApellidoPaterno { get; set; } = null!;

    public string ApellidoMaterno { get; set; } = null!;

    public bool? Asistio { get; set; }

    public DateTime? CreatedAt { get; set; }

    [JsonIgnore]
    public virtual Reserva? IdReservaFkNavigation { get; set; } = null!;
}
