# Verificación local · 10 de noviembre de 2026 (America/Lima)

## Bloque 3: evidencia vigente

El [informe del Bloque 3](BLOQUE-3-COMPLEMENTOS.md) documenta las funciones nuevas, sus fórmulas y la verificación local: 83 pruebas .NET y 367 comprobaciones en Chrome (207 del flujo existente y 160 de complementos), todas correctas. Se comprobaron escritorio, tablet y móvil, migración sin pérdida de registros, aislamiento de notificaciones y reinicio transaccional por sesión. No se publicó ni desplegó este bloque.

## Bloque 2: evidencia histórica

El [informe del Bloque 2](BLOQUE-2-FLUJO-CONVALIDACION.md) documenta la verificación actual: compilación Release sin advertencias, 51 pruebas .NET correctas y 207 comprobaciones en Chrome, con capturas de los cinco perfiles en escritorio y móvil. Incluye migraciones sobre datos anteriores, persistencia, flujo completo, aprobación parcial y aislamiento de sesiones. No se desplegó en Render ni se ejecutó GitHub Actions.

Las secciones siguientes conservan la evidencia histórica de las versiones iniciales de tres perfiles.

## Actualización para publicación

Se agregó `IsolateDemoSessions`, una migración aditiva que conserva registros anteriores, y aislamiento por sesión para solicitudes e historial. Las claves de cookies son efímeras, coherentes con la sesión en memoria y Render Free. Los registros anteriores permanecen en un ámbito interno sin exponerse a navegadores nuevos.

Verificación repetida: `dotnet restore --locked-mode`; compilación Release con `--warnaserror` (0 advertencias, 0 errores); **31 pruebas correctas, 0 fallidas, 0 omitidas**; modelo sin cambios pendientes respecto de las migraciones; publicación Release correcta. Las pruebas nuevas comprueban los ejemplos de sesión y que un segundo navegador no pueda leer ni modificar un expediente ajeno, incluso conociendo su identificador y revisión.

Se revisaron las referencias remotas, los archivos versionados y patrones de credenciales en todo el historial. No se detectaron bases locales, archivos temporales o credenciales preparados para publicación. El escaneo por patrones no equivale a una garantía exhaustiva sobre cualquier formato de secreto.

La publicación Release se inició en `Production` con `PORT=10001` y una base SQLite nueva dentro de `artifacts/`. Aplicó ambas migraciones; el siguiente arranque conservó la base sin migraciones pendientes. Una comprobación HTTP real verificó `/health`, bandejas y detalle propio con HTTP 200, ejemplos distintos para dos navegadores y HTTP 404 al consultar un expediente del otro navegador. El artefacto y la base no se versionan.

La sección siguiente conserva la evidencia histórica de la implementación inicial; al momento de esa revisión había 31 pruebas. El resultado remoto de GitHub Actions debe consultarse en el repositorio después de publicar. Los [pasos para Render Free](DESPLIEGUE.md) incluyen la verificación del commit desplegado y del aislamiento entre navegadores.

## Entorno y estado inicial

- Carpeta inicial vacía, sin proyecto ni repositorio Git local; no existía `AGENTS.md`.
- `git ls-remote https://github.com/jodach34/DEMO-LUCIANA.git` respondió correctamente sin referencias. Se comprobó nuevamente después de las interrupciones de la sesión.
- SDK instalado: **10.0.400**. Runtime ASP.NET Core/.NET: **10.0.11**, Windows x64.
- Base creada con `dotnet new mvc`, solución y proyecto de pruebas con las plantillas del SDK.
- Docker no está instalado o disponible en PATH. No se ejecutó `docker build` ni el contenedor localmente.

## Resultados observados

| Comprobación | Resultado |
| --- | --- |
| `dotnet restore --locked-mode` | Correcto, proyectos restaurados con locks |
| `dotnet build --no-restore --configuration Release --warnaserror` | Correcto, 0 advertencias, 0 errores |
| `dotnet test --no-restore` (Debug) | 30 correctas, 0 fallidas, 0 omitidas |
| `dotnet test --no-build --configuration Release` | 30 correctas, 0 fallidas, 0 omitidas |
| `dotnet ef migrations has-pending-model-changes --project src/Convalidacion.Web --configuration Release --no-build` | Sin cambios pendientes |
| Auditoría NuGet con `dotnet list package --vulnerable --include-transitive --no-restore` | Sin vulnerabilidades reportadas para ambos proyectos en las fuentes consultadas |
| `dotnet publish src/Convalidacion.Web --configuration Release --no-restore --output artifacts/publish /p:UseAppHost=false` | Correcto |
| Inicio de la publicación con `Production`, `PORT=10000` y ruta SQLite local explícita | Kestrel escuchó en `http://0.0.0.0:10000` |
| Primer arranque con SQLite vacío | Aplicó `InitialCreate` y sembró los datos ficticios |
| Segundo arranque sobre la misma base | Sin migraciones pendientes; datos conservados |
| HTTP real de `/health`, `/`, `/Solicitudes`, `/Solicitudes/Crear`, `/Solicitudes/Detalle/1`, `/css/site.css` y Bootstrap local | HTTP 200 |
| POST real de cambio a Secretaría, con cookies y antiforgery | Correcto; apareció el formulario de atención |
| Exclusión de base SQLite, `.env`, TRX y publicación | Comprobada con `git check-ignore` |

Los resultados TRX se generan en `TestResults/` y no se versionan. La base de demostración local queda en `src/Convalidacion.Web/App_Data/convalidacion.db` y tampoco se versiona.

## Cobertura de las pruebas

SQLite real en memoria, migraciones y sembrado; registro → revisión → aprobación; observación y subsanación; rechazo y cierre; límites mínimos y criterios insuficientes; perfiles incompatibles; asignatura inexistente; documentos incompletos; revisión desactualizada y concurrencia entre dos contextos; sembrado sin duplicación ni pérdida de solicitudes nuevas.

Integración MVC: páginas y recursos; registro y aprobación vía HTTP con sesiones; validación de formularios sin JavaScript; antiforgery; redirecciones externas rechazadas; valores enum inválidos; filtros y páginas 404. Se corrigieron los fallos de valores enum descubiertos por estas pruebas.

El aislamiento de Windows impidió al primer proceso acceder a claves DPAPI del usuario y al registro de eventos. Se configuraron logs de consola; las pruebas usan claves efímeras aisladas y finalmente pasaron dentro del sandbox. La comprobación del ejecutable publicado se realizó fuera del sandbox con el usuario de Windows, después de solicitar escalación. No se modificó la configuración de claves del usuario.

## Pendiente de comprobación externa

- No se realizó una revisión visual en navegador automatizado; las vistas se compilaron y sus respuestas HTTP fueron comprobadas.
- El Dockerfile y `render.yaml` están preparados; no se ha verificado una ejecución Docker o un despliegue en Render.
- GitHub Actions no se ha ejecutado en GitHub. El workflow incluye compilación, pruebas, revisión del modelo y construcción/arranque Docker en Linux.
- No se hizo `git push`, no se creó un servicio Render y no se configuraron protecciones de ramas. Requieren la publicación solicitada expresamente y el acceso correspondiente a GitHub/Render.
- No se configuraron discos de pago ni secretos de producción.
