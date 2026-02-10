using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ProyectoVisitas.Models;

public partial class BdvisitasContext : DbContext
{
    public BdvisitasContext()
    {
    }

    public BdvisitasContext(DbContextOptions<BdvisitasContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BitacoraGeneralAcceso> BitacoraGeneralAccesos { get; set; }

    public virtual DbSet<ComunitarioAsistencia> ComunitarioAsistencias { get; set; }

    public virtual DbSet<ComunitarioEvidencia> ComunitarioEvidencias { get; set; }

    public virtual DbSet<ComunitarioPerfile> ComunitarioPerfiles { get; set; }

    public virtual DbSet<Espacio> Espacios { get; set; }

    public virtual DbSet<Reserva> Reservas { get; set; }

    public virtual DbSet<ReservasListaAsistente> ReservasListaAsistentes { get; set; }

    public virtual DbSet<VisitasBitacora> VisitasBitacoras { get; set; }

    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BitacoraGeneralAcceso>(entity =>
        {
            entity.HasKey(e => e.IdBitacoraGeneral).HasName("PK__Bitacora__509FF95161B700DE");

            entity.Property(e => e.IdBitacoraGeneral).HasColumnName("idBitacoraGeneral");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.EstatusGlobal)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVO")
                .HasColumnName("estatus_global");
            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("fecha");
        });

        modelBuilder.Entity<ComunitarioAsistencia>(entity =>
        {
            entity.HasKey(e => e.IdAsistenciasComunitarias).HasName("PK__Comunita__73F46E740D19104C");

            entity.Property(e => e.IdAsistenciasComunitarias).HasColumnName("idAsistenciasComunitarias");
            entity.Property(e => e.ApellidoMaterno)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("apellido_materno");
            entity.Property(e => e.ApellidoPaterno)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("apellido_paterno");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.FechaAsistencia).HasColumnName("fecha_asistencia");
            entity.Property(e => e.HoraDeInicio).HasColumnName("hora_de_inicio");
            entity.Property(e => e.HoraDeSalida).HasColumnName("hora_de_salida");
            entity.Property(e => e.HorasACubrir)
                .HasDefaultValue(4)
                .HasColumnName("horas_a_cubrir");
            entity.Property(e => e.IdBitacoraGeneral).HasColumnName("idBitacoraGeneral");
            entity.Property(e => e.Nombre)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("nombre");
            entity.Property(e => e.PerfilId).HasColumnName("perfil_id");

            entity.HasOne(d => d.IdBitacoraGeneralNavigation).WithMany(p => p.ComunitarioAsistencia)
                .HasForeignKey(d => d.IdBitacoraGeneral)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Comunitario_BitacoraGeneral");

            entity.HasOne(d => d.Perfil).WithMany(p => p.ComunitarioAsistencia)
                .HasForeignKey(d => d.PerfilId)
                .HasConstraintName("FK__Comunitar__perfi__10566F31");
        });

        modelBuilder.Entity<ComunitarioEvidencia>(entity =>
        {
            entity.HasKey(e => e.IdEvidencias).HasName("PK__Comunita__5A4772D7E98F475F");

            entity.Property(e => e.IdEvidencias).HasColumnName("idEvidencias");
            entity.Property(e => e.FechaCarga)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("fecha_carga");
            entity.Property(e => e.PerfilId).HasColumnName("perfil_id");
            entity.Property(e => e.UrlDocumento)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("url_documento");

            entity.HasOne(d => d.Perfil).WithMany(p => p.ComunitarioEvidencia)
                .HasForeignKey(d => d.PerfilId)
                .HasConstraintName("FK__Comunitar__perfi__114A936A");
        });

        modelBuilder.Entity<ComunitarioPerfile>(entity =>
        {
            entity.HasKey(e => e.IdPerfilComunitario).HasName("PK__Comunita__0CE03501C94AB979");

            entity.Property(e => e.IdPerfilComunitario).HasColumnName("idPerfilComunitario");
            entity.Property(e => e.ApellidoMaterno)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("apellido_materno");
            entity.Property(e => e.ApellidoPaterno)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("apellido_paterno");
            entity.Property(e => e.EstatusServicio)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVO")
                .HasColumnName("estatus_servicio");
            entity.Property(e => e.FechaRegistro)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("fecha_registro");
            entity.Property(e => e.HorasAcumuladasActuales)
                .HasDefaultValue(0)
                .HasColumnName("horas_acumuladas_actuales");
            entity.Property(e => e.HorasTotalesDeuda).HasColumnName("horas_totales_deuda");
            entity.Property(e => e.Nombre)
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("nombre");
            entity.Property(e => e.UrlFotoRostro)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("url_foto_rostro");
        });

        modelBuilder.Entity<Espacio>(entity =>
        {
            entity.HasKey(e => e.IdEspacios).HasName("PK__Espacios__93E47CA0A00059E0");

            entity.Property(e => e.IdEspacios).HasColumnName("idEspacios");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.Capacidad).HasColumnName("capacidad");
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.HasKey(e => e.IdReserva).HasName("PK__Reservas__94D104C849709436");

            entity.Property(e => e.IdReserva).HasColumnName("idReserva");
            entity.Property(e => e.AreaReservante)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("area_reservante");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.EspacioId).HasColumnName("espacio_id");
            entity.Property(e => e.EstatusReserva).HasColumnName("estatus_reserva");
            entity.Property(e => e.FechaFin)
                .HasColumnType("datetime")
                .HasColumnName("fecha_fin");
            entity.Property(e => e.FechaInicio)
                .HasColumnType("datetime")
                .HasColumnName("fecha_inicio");
            entity.Property(e => e.IdBitacoraGeneral).HasColumnName("idBitacoraGeneral");
            entity.Property(e => e.IdUsuarioReservante).HasColumnName("id_usuario_reservante");
            entity.Property(e => e.InstitucionVisitante)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("institucion_visitante");
            entity.Property(e => e.NombreReservante)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("nombre_reservante");
            entity.Property(e => e.NumeroPersonas).HasColumnName("numero_personas");
            entity.Property(e => e.RepresentanteVisita)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("representante_visita");
            entity.Property(e => e.RequerimientosEspecialesJson)
                .IsUnicode(false)
                .HasColumnName("requerimientos_especiales_json");

            entity.HasOne(d => d.Espacio).WithMany(p => p.Reservas)
                .HasForeignKey(d => d.EspacioId)
                .HasConstraintName("FK__Reservas__espaci__0F624AF8");

            entity.HasOne(d => d.IdBitacoraGeneralNavigation).WithMany(p => p.Reservas)
                .HasForeignKey(d => d.IdBitacoraGeneral)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reserva_BitacoraGeneral");
        });

        modelBuilder.Entity<ReservasListaAsistente>(entity =>
        {
            entity.HasKey(e => e.IdLista).HasName("PK__Reservas__6C8A0FE51934E473");

            entity.Property(e => e.IdLista).HasColumnName("idLista");
            entity.Property(e => e.ApellidoMaterno)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("apellido_materno");
            entity.Property(e => e.ApellidoPaterno)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("apellido_paterno");
            entity.Property(e => e.Asistio)
                .HasDefaultValue(false)
                .HasColumnName("asistio");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.IdReservaFk).HasColumnName("id_reservaFK");
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nombre");

            entity.HasOne(d => d.IdReservaFkNavigation).WithMany(p => p.ReservasListaAsistentes)
                .HasForeignKey(d => d.IdReservaFk)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ReservasL__id_re__123EB7A3");
        });

        modelBuilder.Entity<VisitasBitacora>(entity =>
        {
            entity.HasKey(e => e.IdBitacoraVisitas).HasName("PK__VisitasB__A7E24E268F741563");

            entity.ToTable("VisitasBitacora");

            entity.Property(e => e.IdBitacoraVisitas).HasColumnName("idBitacoraVisitas");
            entity.Property(e => e.AceptoTerminos).HasColumnName("acepto_terminos");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.FechaVisita)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("fecha_visita");
            entity.Property(e => e.HoraEntrada).HasColumnName("hora_entrada");
            entity.Property(e => e.HoraSalida).HasColumnName("hora_salida");
            entity.Property(e => e.IdBitacoraGeneral).HasColumnName("idBitacoraGeneral");
            entity.Property(e => e.MotivoVisita)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("motivo_visita");
            entity.Property(e => e.NombreVisitante)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("nombre_visitante");

            entity.HasOne(d => d.IdBitacoraGeneralNavigation).WithMany(p => p.VisitasBitacoras)
                .HasForeignKey(d => d.IdBitacoraGeneral)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Visitas_BitacoraGeneral");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
