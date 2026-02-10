using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProyectoVisitas.Models;

public partial class Espacio
{
    public int IdEspacios { get; set; }

    public string Nombre { get; set; } = null!;

    public int Capacidad { get; set; }

    public bool? Activo { get; set; }

    [JsonIgnore]
    public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
