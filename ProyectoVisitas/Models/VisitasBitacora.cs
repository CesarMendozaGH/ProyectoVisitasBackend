using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProyectoVisitas.Models;

public partial class VisitasBitacora
{
    public int IdBitacoraVisitas { get; set; }

    public int IdBitacoraGeneral { get; set; }

    public string NombreVisitante { get; set; } = null!;

    public string MotivoVisita { get; set; } = null!;

    public DateOnly FechaVisita { get; set; }

    public TimeOnly HoraEntrada { get; set; }

    public TimeOnly? HoraSalida { get; set; }

    public bool AceptoTerminos { get; set; }

    public DateTime? CreatedAt { get; set; }

    [JsonIgnore]
    public virtual BitacoraGeneralAcceso? IdBitacoraGeneralNavigation { get; set; } = null!;
}
