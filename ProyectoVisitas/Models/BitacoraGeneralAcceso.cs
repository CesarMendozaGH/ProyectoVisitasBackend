using System;
using System.Collections.Generic;

namespace ProyectoVisitas.Models;

public partial class BitacoraGeneralAcceso
{
    public int IdBitacoraGeneral { get; set; }

    public DateOnly? Fecha { get; set; }

    public string? EstatusGlobal { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<ComunitarioAsistencia> ComunitarioAsistencia { get; set; } = new List<ComunitarioAsistencia>();

    public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();

    public virtual ICollection<VisitasBitacora> VisitasBitacoras { get; set; } = new List<VisitasBitacora>();
}
