# Verificación local · 9 de noviembre de 2026 (America/Lima)

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
