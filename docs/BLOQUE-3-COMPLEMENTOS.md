# Bloque 3: complementos funcionales de DEMO-LUCIANA

Verificación local: **10 de noviembre de 2026, America/Lima**.

**BLOQUE 3 IMPLEMENTADO LOCALMENTE — PENDIENTE DE AUTORIZACIÓN PARA PUBLICAR EN GITHUB**.

## Repositorio y base de trabajo

- Ruta exclusiva: `C:\Users\JOEL CONDOR\Desktop\DEMO LUCIANA`.
- Lectura y escritura de `origin`: `https://github.com/jodach34/DEMO-LUCIANA.git`.
- Rama: `feature/bloque-3-complementos`.
- Estado inicial limpio; no había modificaciones de usuario que preservar.
- `git fetch origin` confirmó `origin/main` en `cd6c2eb`, merge del Bloque 2 (`77e5194`), y `origin/develop` en `e3745f4`.
- Se inspeccionaron controladores, entidades, servicios, vistas e historial real. El contenido de `origin/main` coincide con el Bloque 2; su commit adicional es el merge.
- `develop` local estaba en `1a15a24`, ancestro de `origin/main`: se avanzó solo localmente con `merge --ff-only origin/main`, sin conflictos. La rama del Bloque 3 parte de esa base para futura integración mediante `develop`.
- El hash del commit final se obtiene con `git log -1 --oneline` en la rama de trabajo y se comunica en el cierre. No se hizo push, PR, merge remoto ni despliegue.

## Funcionalidades

| Pantalla / acción | Comportamiento |
| --- | --- |
| `/Complementos/Calculadora` | Consulta curso USMP ilustrativo, créditos de destino, origen, créditos y coincidencia estimada. Referencia general de 80%; excepción desde 70% con afinidad directa y créditos iguales. Valida en servidor y no modifica expedientes. |
| `/Complementos/Equivalencias` | Catálogo del plan ficticio DEMO, búsqueda por nombre o código y filtro por créditos. Permite comparar con los cursos y decisiones actuales de un expediente propio. Rechaza expedientes de otros visitantes. |
| `/Complementos/Indicadores` | Tarjetas y gráficos de barras accesibles con conteos y tasas calculados por sesión. |
| `/Complementos/Proceso` | Comparación visual AS-IS / TO-BE: revisión manual, retrasos, seguimiento, tiempos y acumulación frente a checklist, aprobaciones simuladas, avisos, trazabilidad y bandejas. Beneficios expresamente esperados, sin métricas históricas inventadas. |
| `/Notificaciones` | Historial paginado de 20 avisos por página del perfil actual; todos o solo sin leer. Fecha y hora de Perú, destinatario, tipo, enlace al detalle y marcado individual como leído. La campana cuenta todos los avisos sin leer, aunque solo muestre los cinco más recientes. |
| `/Complementos/Ayuda` | Doce preguntas desplegables: convalidación, solicitud, documentos, varios cursos, subsanación, estados, responsables, resolución, resultado, 80/70, avisos/reinicio y almacenamiento temporal. |
| `POST /Complementos/Reiniciar` | Modal de confirmación con casilla obligatoria, antiforgery y validación de confirmación en servidor. Restablece únicamente la sesión actual y limpia el expediente activo, manteniendo el perfil y el catálogo. |

La calculadora explica cómo estimar contenidos: diez temas ficticios de igual peso, 10 puntos por tema coincidente. El usuario introduce su estimación entre 0 y 100. No analiza archivos ni inventa equivalencias oficiales. La decisión corresponde al **Director del Departamento Académico** y el flujo conserva sus comprobaciones adicionales de nota y documentos. Alcanzar un umbral no aprueba una asignatura.

Se mantienen cinco perfiles instantáneos sin login, cuatro etapas, solicitudes de varias asignaturas, documentos simulados, correcciones, decisiones individuales, dictámenes versionados, aprobación decanal, resolución en pantalla, carga parcial e historial. Se conserva la imagen institucional existente. No hay modo de exposición, correos, PDF, autenticación ni servicios de pago.

## Cálculo de indicadores

Todas las consultas respetan el filtro global de sesión; no hay valores fijados para aparentar resultados.

| Indicador | Fórmula y criterio |
| --- | --- |
| Total | Número de expedientes de la sesión. |
| Finalizadas | Estado actual `Finalizada`. |
| Tiempo promedio | Media de horas entre `CreadaUtc` y el último movimiento `Finalizar`, únicamente en expedientes actualmente finalizados. Excluye movimientos ausentes, fechas anteriores a creación y fechas futuras. No usa `ActualizadaUtc` como sustituto. Muestra el número de expedientes con fechas válidas. |
| Observaciones | Expedientes con estado actual observado/subsanación, historial observado o devolución decanal, o al menos una evaluación de curso observada. Cada expediente cuenta una vez, aunque tenga varias observaciones o ya se hayan resuelto. Porcentaje = ese conteo / total × 100. |
| Tasa de convalidación | Asignaturas con resultado actual `Aprobada` / asignaturas actualmente `Aprobada` o `Rechazada` × 100. Excluye pendientes y observadas y no cuenta repetidamente versiones de evaluaciones. No es una tasa institucional. |
| Por etapa | Cantidad según las cuatro etapas del modelo; incluye finalizados en etapa 4. |
| Pendientes por responsable | Expedientes del proceso actual cuyo estado pertenece a la bandeja del perfil, usando la misma regla del flujo existente. |

Con denominador cero o sin fechas válidas se presenta **Sin datos**. Los conteos cero sí se muestran como cero. El resumen de observaciones del inicio reutiliza el criterio histórico de los KPI.

Los seis ejemplos contienen doce cursos: tres aprobados y tres rechazados entre seis cursos con decisión, y seis pendientes. Por eso inicialmente la tasa es 50%, las observaciones son 1/6 y existe un expediente finalizado. Es un resultado de las consultas, no una constante en la interfaz.

## Notificaciones y seguridad

Los eventos incluyen presentación, recepción, observación, subsanación, derivación, dictamen, resolución, notificación decanal y registro académico finalizado. Cada evento se dirige al perfil que interviene o necesita conocerlo; la notificación decanal conserva a Estudiante y Registros Académicos como destinatarios.

El aviso se guarda con la transición, historial y revisión del expediente en una operación EF atómica. Se asigna un identificador de evento compartido entre sus destinatarios, con índice único `(SolicitudId, Destinatario, EventoId)`. Recargar vistas no genera avisos. El token de revisión y las reglas del proceso impiden repetir una transición ya aplicada. Marcar como leído es idempotente y requiere sesión y destinatario propios; es una acción POST protegida con antiforgery.

La migración **`20261111020744_ComplementosNotificaciones`** añade `EventoId`, `Tipo` y `LeidaUtc`, y amplía el índice de notificaciones. Conserva mensajes, identificadores, fechas y destinatarios previos; los avisos antiguos siguen identificados como notificación decanal y comienzan sin leer. El timestamp del nombre de migración corresponde a UTC; la verificación del informe usa hora de Perú. No se borraron bases existentes para migrar.

## Reinicio y ejemplos

Tras confirmar se abre una transacción. Se eliminan primero registros académicos y resoluciones propios por sus referencias restrictivas, y después los expedientes del ámbito actual con sus dependencias. Se vuelve a sembrar dentro de la misma transacción. Cualquier fallo revierte también el borrado. No se ejecutan `EnsureDeleted`, borrados de archivos de base de datos ni consultas sin filtro de ámbito.

La nueva sesión y el reinicio recuperan exactamente estos estados:

1. Solicitud presentada.
2. Pendiente de subsanación, con observación y devolución documentadas.
3. Evaluación académica.
4. Revisión decanal pendiente, con dictamen individual emitido.
5. Registro académico pendiente, con resolución y avisos decanales.
6. Finalizada, con registro únicamente de cursos aprobados.

Todos contienen dos asignaturas ficticias y movimientos coherentes. Los avisos iniciales se reconstruyen desde las mismas transiciones, sin reutilizar lecturas de la sesión anterior. El reinicio no afecta al catálogo compartido, al ámbito histórico interno ni a otros visitantes.

## Ejecución y verificación

Desde la raíz del repositorio, con SDK .NET 10:

```powershell
dotnet restore --locked-mode
dotnet tool restore
dotnet build -c Release
dotnet test -c Release
dotnet ef migrations has-pending-model-changes --project src/Convalidacion.Web --configuration Release --no-build
dotnet run --project src/Convalidacion.Web --launch-profile http
```

Abrir `http://localhost:5283`. No se requieren claves. El inicio migra SQLite sin eliminar registros. Para probar migraciones en una base separada, definir `ConnectionStrings__DefaultConnection` con una ruta dentro de `artifacts`, y usar `dotnet ef database update --project src/Convalidacion.Web --configuration Release --no-build` y `dotnet ef migrations list` con los mismos parámetros.

Resultados ejecutados:

- Restore bloqueado correcto. Una primera ejecución restringida mostró NU1900 por conectividad; la restauración posterior con red completó la auditoría sin advertencias.
- Compilación Release correcta; verificación adicional con advertencias como errores.
- **83 pruebas .NET aprobadas, cero fallidas, cero omitidas**. Incluyen límites 69/70/79/80/100, créditos, validación de catálogo, KPI sin datos y fechas inválidas, observaciones repetidas, unicidad de eventos, lectura por perfil, dos visitantes, reinicio, reversión ante fallo, migraciones y flujo completo.
- Herramienta `dotnet-ef` 10.0.14 restaurada; cuatro migraciones aplicadas a `artifacts/bloque3-migraciones.db`; modelo sin cambios pendientes. La migración se verificó además sobre datos históricos y avisos del Bloque 2 mediante pruebas SQLite.
- **367 comprobaciones Chrome correctas**: 207 del flujo existente y 160 de complementos. Sin excepciones JavaScript ni recursos HTTP fallidos en los recorridos. Resoluciones parciales, cinco perfiles y navegación conservados.
- Anchos comprobados: 1920, 1366, 1024, 768, 390 y 320 píxeles. Sin desbordamiento de página, selector visible, logo cargado; confirmación de reinicio móvil comprobada. Se inspeccionaron capturas frente al mockup institucional.

Los scripts reproducibles son `tests/browser/flujo-convalidacion.mjs` y `tests/browser/bloque-3.mjs`. Requieren Node 24, una instancia **local de QA con base exclusiva** en puerto 5285 y Chrome con un perfil exclusivo y DevTools en puerto 9225. Admiten `QA_ORIGIN`, `QA_CDP_PORT` y `QA_ARTIFACT_DIR`. Los recorridos crean datos ficticios y el segundo reinicia su sesión de QA.

```powershell
node tests/browser/flujo-convalidacion.mjs
node tests/browser/bloque-3.mjs
```

TRX, reportes JSON, capturas, perfiles de navegador y bases de QA permanecen ignorados en `TestResults/` y `artifacts/`; no forman parte del commit.

## Archivos del Bloque 3

- Controladores: `HomeController.cs`, `ComplementosController.cs`, `NotificacionesController.cs`.
- Modelos: `Solicitud.cs`, `ComplementosViewModels.cs`.
- Servicios: `SolicitudService.cs`, `ComplementosService.cs`, `NotificacionService.cs`.
- Datos: `AppDbContext.cs`, `DemoSeeder.cs`, migración `ComplementosNotificaciones` con su diseñador y `AppDbContextModelSnapshot.cs`.
- Configuración de servicios: `Program.cs`.
- Vistas: `Home/Index.cshtml`, `Home/Privacy.cshtml`, cinco vistas en `Complementos/`, `Notificaciones/Index.cshtml`, `_MenuLateral.cshtml`, `_BarraSuperior.cshtml`, `_Layout.cshtml`.
- Recursos: `wwwroot/css/site.css`, `wwwroot/js/site.js`. Logo y referencias visuales intactos.
- Pruebas: `ComplementosTests.cs`, `ComplementosWebTests.cs`, ajustes de expectativas en `SolicitudServiceTests.cs` y `WebTests.cs`; nuevo `tests/browser/bloque-3.mjs`.
- Documentación: `README.md`, este informe, `VERIFICACION.md`, `DESPLIEGUE.md` y avisos de contexto histórico en los informes de Bloques 1 y 2.

## Límites y publicación

Se mantiene Render Free con SQLite y sesiones en memoria. El almacenamiento es temporal: puede perderse al reiniciar, desplegar o suspender el servicio; las sesiones caducan tras dos horas de inactividad y sus claves son efímeras. La demo no es una plataforma oficial ni un archivo académico duradero. `Dockerfile` y `render.yaml` permanecen intactos.

No quedan funcionalidades solicitadas del Bloque 3 pendientes de implementación local. La publicación en GitHub y cualquier despliegue están pendientes de autorización expresa. No se ejecutaron GitHub Actions remotos ni un despliegue de Render.
