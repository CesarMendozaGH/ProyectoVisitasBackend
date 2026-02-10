using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProyectoVisitas.Models;

public partial class Reserva
{
    public int IdReserva { get; set; }

    public int? EspacioId { get; set; }

    public int IdBitacoraGeneral { get; set; }

    public int IdUsuarioReservante { get; set; }

    public string NombreReservante { get; set; } = null!;

    public string? AreaReservante { get; set; }

    public string InstitucionVisitante { get; set; } = null!;

    public string RepresentanteVisita { get; set; } = null!;

    public int NumeroPersonas { get; set; }

    public string? RequerimientosEspecialesJson { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public bool? EstatusReserva { get; set; }

    public DateTime? CreatedAt { get; set; }

    [JsonIgnore]
    public virtual Espacio? Espacio { get; set; }

    [JsonIgnore]
    public virtual BitacoraGeneralAcceso? IdBitacoraGeneralNavigation { get; set; } = null!;
    
    [JsonIgnore]
    public virtual ICollection<ReservasListaAsistente> ReservasListaAsistentes { get; set; } = new List<ReservasListaAsistente>();
}
