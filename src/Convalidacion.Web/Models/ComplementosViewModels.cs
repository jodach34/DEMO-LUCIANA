using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Convalidacion.Web.Models;

public class CalculadoraViewModel
{
    [Range(1, int.MaxValue), Display(Name = "Asignatura USMP ilustrativa")] public int AsignaturaId { get; set; }
    [Required, StringLength(120), Display(Name = "Asignatura de origen ficticia")] public string AsignaturaOrigen { get; set; } = "";
    [Range(1, 10), Display(Name = "Créditos de origen")] public int CreditosOrigen { get; set; } = 4;
    [Range(0, 100), Display(Name = "Coincidencia de contenidos estimada (%)")] public int Coincidencia { get; set; } = 80;
    [Display(Name = "Existe afinidad temática directa (declaración de demostración)")] public bool AfinidadDirecta { get; set; }
    [BindNever, ValidateNever] public IReadOnlyList<Asignatura> Catalogo { get; set; } = [];
    [BindNever, ValidateNever] public ResultadoCalculadora? Resultado { get; set; }
}
public record ResultadoCalculadora(Asignatura Destino, string Mensaje);
public record EquivalenciasViewModel(IReadOnlyList<Asignatura> Catalogo, IReadOnlyList<Solicitud> Expedientes,
    Solicitud? Expediente, string? Buscar, int? Creditos);
public record IndicadoresViewModel(int Total, int Finalizadas, double? PromedioHoras, int FinalizadasConFechas,
    int Observadas, decimal? TasaObservaciones, int Aprobadas, int Decididas, decimal? TasaConvalidacion,
    IReadOnlyDictionary<int, int> PorEtapa, IReadOnlyDictionary<PerfilDemo, int> PorResponsable);
public record NotificacionesViewModel(IReadOnlyList<NotificacionExpediente> Avisos, int SinLeer, bool SoloSinLeer, int Pagina, int Paginas);
