# Sistema de Convalidación de Asignaturas FIA–EPICS

Demo universitaria del curso **Gestión de Procesos · Universidad de San Martín de Porres (USMP)**.

Aplicación ASP.NET Core MVC con Razor, Bootstrap local, CSS y JavaScript; .NET 10, Entity Framework Core 10 y SQLite. Sin autenticación. Los perfiles se simulan por sesión y cualquier visitante puede cambiarlos. **Solo datos ficticios; las decisiones no tienen validez administrativa.**

Repositorio esperado: <https://github.com/jodach34/DEMO-LUCIANA.git>.

## Ejecutar localmente

Requisitos: SDK .NET 10 estable; Git. Docker es opcional para comprobar el contenedor. `global.json` admite las bandas estables del SDK 10.0, incluido el 10.0.400 instalado durante la implementación.

Desde la raíz:

```powershell
dotnet restore --locked-mode
dotnet tool restore
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/Convalidacion.Web --launch-profile http
```

Abre <http://localhost:5283>. El inicio aplica las migraciones y prepara cuatro asignaturas ilustrativas. Cada sesión nueva recibe seis expedientes propios con múltiples asignaturas y ejemplos de las cuatro etapas. SQLite local se guarda en `src/Convalidacion.Web/App_Data/convalidacion.db`, fuera de Git. Las fechas se almacenan en UTC y se muestran en hora de Perú (UTC−5).

Los paquetes y sus dependencias quedan registrados en `packages.lock.json`. Para actualizar versiones, modifica las referencias, ejecuta `dotnet restore`, revisa los locks y vuelve a probar.

## Recorrido de la demo

1. Con **Estudiante**, presenta un expediente de una a veinte asignaturas. Escribe cada curso de origen y selecciona su destino del catálogo ficticio, créditos, nota y sílabo simulado. Selecciona también un certificado ficticio.
2. El selector superior cambia el perfil automáticamente y abre su panel. En **Expediente activo** puedes regresar al expediente que estabas consultando.
3. Con **Oficina de Admisión**, recibe el expediente. Puedes observarlo y devolverlo al estudiante para subsanar; verifica después la corrección. Confirma la revisión del certificado y de todos los sílabos para derivarlo al Departamento Académico.
4. Con **Director del Departamento Académico**, evalúa y justifica cada asignatura. Emite el dictamen y remítelo al Decano. Los resultados pueden ser distintos dentro del mismo expediente.
5. Con **Decano**, devuelve el dictamen con observaciones o apruébalo. La devolución exige una nueva revisión del Director y una nueva versión del dictamen. Tras aprobarlo, emite una resolución ficticia con número que comience por `DEMO-` y fecha; notifica al estudiante y a Registros Académicos y remite para la carga.
6. Con **Registros Académicos**, confirma la carga de las asignaturas aprobadas y finaliza. Las rechazadas se excluyen; el expediente puede finalizar sin asignaturas aprobadas, dejando constancia de ello.
7. Consulta los dictámenes y resoluciones dentro de la aplicación, el historial del expediente y las versiones de documentos y evaluaciones. No tienen validez oficial.

```mermaid
stateDiagram-v2
    [*] --> Presentada: Estudiante
    Presentada --> VerificacionDocumental: Admisión recibe
    VerificacionDocumental --> Observada: Admisión observa
    Observada --> PendienteSubsanacion: Admisión devuelve
    PendienteSubsanacion --> Subsanada: Estudiante corrige documentos
    Subsanada --> VerificacionDocumental: Admisión verifica corrección
    VerificacionDocumental --> EvaluacionAcademica: Admisión deriva
    EvaluacionAcademica --> Observada: Director solicita corrección documental
    EvaluacionAcademica --> DictamenEmitido: Director evalúa todos los cursos y emite
    DictamenEmitido --> RevisionDecanal: Director remite
    RevisionDecanal --> EvaluacionAcademica: Decano devuelve con sustento
    RevisionDecanal --> DictamenAprobado: Decano aprueba el dictamen
    DictamenAprobado --> ResolucionEmitida: Decano emite resolución ficticia
    ResolucionEmitida --> NotificacionRealizada: Decano notifica
    NotificacionRealizada --> RegistroAcademicoPendiente: Decano remite
    RegistroAcademicoPendiente --> RegistroAcademicoPendiente: Registros confirma carga parcial o total
    RegistroAcademicoPendiente --> Finalizada: Registros finaliza
    Finalizada --> [*]
```

Reglas **didácticas**, no normativa oficial de la USMP: nota mínima 11/20 y créditos de origen suficientes. Coincidencia referencial de 80%, o excepción desde 70% por afinidad temática directa justificada y créditos equivalentes. El Director registra la coincidencia y decide manualmente aprobar, rechazar u observar: ningún porcentaje aprueba automáticamente. Se pueden registrar cursos con criterios insuficientes para demostrar su evaluación y rechazo.

Alcance: varias asignaturas por expediente; documentos ficticios seleccionables, sin carga física; historial de correcciones y evaluaciones; dictámenes versionados y resolución consultable con advertencia de demostración. Los expedientes de la versión anterior se conservan para consulta con su autoría original. La bandeja muestra hasta 100 expedientes de la sesión y permite filtrar por estado, pendientes del perfil, dictamen y resolución.

## Organización

```text
src/Convalidacion.Web/
  Controllers/       Acciones HTTP y validación de formularios
  Models/            Entidades, enumeraciones y modelos de las vistas
  Services/          Reglas del proceso y perspectiva de perfil simulado
  Data/              DbContext, migraciones y datos de demostración
  Views/             Razor y componentes parciales
  wwwroot/           Bootstrap/jQuery locales, CSS y JavaScript
tests/Convalidacion.Tests/
  SolicitudServiceTests.cs   Reglas del proceso con SQLite real en memoria
  WebTests.cs                Integración HTTP con MVC, cookies y antiforgery
  MigrationTests.cs          Conservación de datos anteriores y persistencia
tests/browser/
  flujo-convalidacion.mjs    Recorrido real de los cinco perfiles mediante Chrome DevTools
```

Se usa directamente EF Core desde servicios y consultas MVC, sin repositorios genéricos ni capas adicionales. Cada cambio guarda estado e historial en la misma operación transaccional. Un token de concurrencia impide sobrescribir una revisión más reciente. Los filtros globales de EF Core aíslan solicitudes e historial mediante una clave aleatoria generada en el servidor y guardada en sesión. No se acepta esa clave desde formularios o parámetros. El perfil simulado no constituye autenticación ni un control de acceso para expedientes reales.

Validación del navegador y del servidor; antiforgery en POST; redirecciones limitadas a rutas locales; páginas para errores y HTTP 404; logs de consola con identificadores y estados, sin registrar nombres ni contenido del expediente. `/health` comprueba conectividad SQLite. Los recursos visuales se sirven localmente.

## Migraciones y configuración

```powershell
dotnet tool restore
dotnet ef migrations list --project src/Convalidacion.Web
dotnet ef database update --project src/Convalidacion.Web
dotnet ef migrations has-pending-model-changes --project src/Convalidacion.Web
# Después de modificar un modelo:
dotnet ef migrations add NombreDelCambio --project src/Convalidacion.Web --output-dir Data/Migrations
```

`MigrateAsync` al iniciar es una decisión para esta demo de una instancia. Para una aplicación con datos duraderos o varias réplicas, diseña una estrategia de migración separada y persistencia externa. Consulta la [documentación de EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

| Configuración | Desarrollo | Producción / Docker |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `App_Data/convalidacion.db` relativo al proyecto | `/tmp/convalidacion/convalidacion.db` |
| `Demo:SeedData` | `true` | `true` |
| `ASPNETCORE_ENVIRONMENT` | `Development` en launch profile | `Production` |
| `PORT` | Sin definir; launch profile 5283 | Puerto suministrado por Render |

Las variables de entorno sustituyen los valores JSON usando doble guion bajo, por ejemplo `ConnectionStrings__DefaultConnection` y `Demo__SeedData`. Para iniciar sin ejemplos, define `Demo__SeedData=false` antes del primer arranque con una base vacía. No elimina ejemplos ya existentes.

El sembrado es determinista e idempotente: conserva las solicitudes de cada sesión. La migración de aislamiento agrega una columna y un índice sin borrar registros anteriores; estos permanecen en un ámbito interno y no se publican a visitantes. Para una demostración nueva, utiliza otro navegador o una sesión privada. La sesión caduca tras dos horas de inactividad y se pierde al reiniciar el proceso, aunque el archivo SQLite local continúe existiendo. Las claves de cookies también son efímeras. No uses datos reales ni consideres la sesión una identidad autenticada.

No se necesitan contraseñas ni claves. No agregues secretos, bases SQLite, archivos `.env` o credenciales de producción al repositorio. Conserva las licencias de las librerías incluidas en `wwwroot/lib`.

## Docker y Render Free

```powershell
docker build -t convalidacion-demo .
docker run --rm -p 8080:8080 -e PORT=8080 convalidacion-demo
```

Abre <http://localhost:8080> y <http://localhost:8080/health>. Imagen multi-stage SDK/ASP.NET 10; publicación Release; proceso sin root; sin volúmenes o discos de pago. Kestrel escucha `0.0.0.0` cuando existe `PORT`. Render termina HTTPS en su proxy; la aplicación no fuerza redirecciones desde el HTTP interno.

Para desplegar después de publicar la versión estable en `main`:

Consulta los [pasos exactos de conexión, configuración y verificación](docs/DESPLIEGUE.md). El despliegue requiere una cuenta Render conectada; no basta con publicar el repositorio.

1. Conecta tu cuenta de GitHub a Render y autoriza **exactamente** `jodach34/DEMO-LUCIANA`.
2. Crea un Blueprint usando `render.yaml`, o un Web Service con runtime Docker, rama `main`, Dockerfile `./Dockerfile`, contexto raíz y plan **Free**.
3. Mantén `ASPNETCORE_ENVIRONMENT=Production`, la conexión SQLite de `/tmp` y `/health` como comprobación de salud.
4. No agregues discos persistentes. El Blueprint solicita despliegue después de pasar los checks de GitHub; verifica que la integración esté activa.
5. Revisa logs de migración/arranque, `/health` y el recorrido funcional en la URL asignada.

Render Free pierde el sistema de archivos al reiniciar, redesplegar o suspender por inactividad. La base y las sesiones son temporales; el esquema se reconstruye al arrancar con un disco vacío y los ejemplos se preparan al abrir una sesión. Puede haber espera al reactivar el servicio. Esto es adecuado para la demo, no para expedientes reales. Ver [límites oficiales de Render Free](https://render.com/docs/free) y [Blueprints](https://render.com/docs/blueprint-spec).

## Git e integración continua

- `main`: versión estable y desplegable.
- `develop`: integración.
- `feature/*`: nuevas funcionalidades; esta demo se implementó en `feature/demo-convalidacion`, creada desde `develop`.
- `fix/*`: correcciones concretas; crea una rama desde `develop` cuando corresponda. `*` expresa una convención, no un nombre literal de rama.

Integra mediante pull request: feature/fix → develop → main. Evita `push --force`. Antes de cualquier publicación, verifica el remoto de lectura y escritura:

```powershell
git remote -v
git remote get-url origin
git remote get-url --push origin
git fetch origin
git status --short --branch
```

El destino debe ser exactamente `https://github.com/jodach34/DEMO-LUCIANA.git`. Si aparecen commits remotos, intégralos sin sobrescribir el historial. La publicación y las protecciones de rama necesitan acceso a la cuenta de GitHub; no están implícitas en los archivos locales.

`.github/workflows/ci.yml` restaura en modo bloqueado, compila Release con advertencias como errores, ejecuta las pruebas si hay proyectos de prueba, revisa migraciones y conserva resultados TRX. Otro job construye Docker y prueba `/health` y `/` usando `PORT=10000`. También se puede iniciar desde **Actions → .NET y Docker → Run workflow**, seleccionando la rama que se desea comprobar.

## Verificación de la implementación

Consulta [docs/VERIFICACION.md](docs/VERIFICACION.md) para los resultados observados y las limitaciones. La existencia del workflow no demuestra que GitHub Actions haya ejecutado los checks: debe verificarse tras publicar el repositorio.

El [informe del Bloque 2](docs/BLOQUE-2-FLUJO-CONVALIDACION.md) documenta los perfiles, transiciones, migración aditiva, pruebas y revisión local. Su implementación está en `feature/flujo-convalidacion`, creada desde `develop` después de integrar el rediseño del Bloque 1. No se desplegó ni se fusionó el Bloque 2 con `main`.
