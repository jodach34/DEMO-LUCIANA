# Bloque 2: proceso de convalidación USMP

Implementación local para revisión. Conserva el diseño del Bloque 1 y no incluye los módulos reservados para el Bloque 3.

## Cambios realizados

- Cinco perfiles definitivos y paneles según su responsabilidad, sin autenticación, correos ni contraseñas.
- Selector superior automático mediante `change` y POST con antiforgery; abre el panel del perfil y conserva el expediente activo en la sesión.
- Expedientes con una a veinte asignaturas, documentos ficticios seleccionables y resultados independientes.
- Verificación de Admisión, observación, devolución, subsanación y nueva verificación.
- Evaluación individual manual, versiones de dictamen, revisión decanal, resolución consultable, notificación y carga académica parcial o total.
- Historial de estados, documentos y evaluaciones, con UTC en persistencia y hora de Perú en pantalla.
- Conservación del menú granate, logo, banner, tarjetas, tablas, indicadores existentes y navegación responsive.

## Los cinco perfiles

| Perfil | Responsabilidad implementada |
| --- | --- |
| Estudiante | Presentar varias asignaturas, seleccionar documentos simulados, consultar expediente/dictámenes/resolución y subsanar referencias documentales |
| Oficina de Admisión | Recibir, verificar, observar con sustento, devolver al estudiante, comprobar correcciones y derivar al Departamento Académico |
| Director del Departamento Académico | Comparar cada origen y destino, revisar créditos/nota/contenidos simulados, registrar coincidencia y resultado justificado, solicitar corrección de sílabos y emitir/remitir el dictamen |
| Decano | Revisar o devolver el dictamen, aprobarlo, asignar número y fecha ficticios a la resolución, emitirla, notificar y remitir para el registro |
| Registros Académicos | Recibir el resultado, cargar sólo asignaturas aprobadas, confirmar la actualización simulada y finalizar |

`PerfilDemo` contiene exactamente cinco valores. La nueva clave de sesión `PerfilFlujoV2` evita interpretar los nombres o números de perfiles antiguos como nuevas responsabilidades.

El cambio de perfil abre `/Home/Index`, cuyo contenido y bandeja corresponden al perfil seleccionado. El último expediente consultado o creado se conserva en `ExpedienteActivoId`, siempre validado mediante los filtros del ámbito de sesión. El enlace **Expediente activo** permite regresar a él. Con JavaScript activo no se muestra un botón adicional; `noscript` ofrece un envío accesible para navegadores sin JavaScript.

## Cuatro etapas y transiciones

| Etapa | Acciones y estados |
| --- | --- |
| 1. Recepción y verificación de expediente | Estudiante presenta → Presentada. Admisión recibe → Verificación documental. Puede observar → Observada y devolver → Pendiente de subsanación. Estudiante corrige → Subsanada. Admisión verifica → Verificación documental y confirma los documentos para derivar → Evaluación académica |
| 2. Evaluación técnica y académica | Director evalúa individualmente sin avanzar el estado del expediente. Puede solicitar corrección de sílabos observados a través de Admisión, recorriendo la subsanación de la etapa 1. Con todos los resultados definitivos emite → Dictamen emitido y remite → Revisión decanal |
| 3. Aprobación oficial y emisión de resolución | Decano devuelve con observaciones → Evaluación académica; exige nueva revisión y nueva versión. O aprueba → Dictamen aprobado y emite → Resolución emitida |
| 4. Notificación y carga académica | Decano notifica al estudiante y a Registros → Notificación realizada; remite → Registro académico pendiente. Registros confirma la carga, conservando ese estado, y después finaliza → Finalizada |

El estado adicional **Dictamen aprobado** separa la aprobación de la emisión del documento. Las evaluaciones individuales y la confirmación de carga también quedan registradas como acciones aunque mantengan el mismo estado. No se permiten acciones de otro responsable, saltos ni reapertura después de finalizar.

El indicador visual obtiene la etapa del estado real del expediente activo. En el detalle sigue al expediente consultado; no utiliza porcentajes ficticios.

## Varias asignaturas y documentos simulados

Cada `AsignaturaSolicitud` conserva curso de origen, destino del catálogo ficticio, créditos de origen/destino, nota, referencia de sílabo y su versión, coincidencia, resultado, justificación, excepción y fecha de evaluación. Los créditos de destino se copian del catálogo al presentar el expediente. No se admiten destinos duplicados dentro del expediente.

El formulario permite agregar y quitar filas con índices continuos, conservando la validación del cliente y del servidor. Los porcentajes ya no los introduce el estudiante: los registra el Director durante la evaluación.

Se requiere un certificado de la colección ficticia y un sílabo por asignatura. Los identificadores se validan contra una lista permitida; no se recibe ni almacena ningún archivo real. Admisión confirma expresamente la revisión de todos los documentos antes de derivar.

La subsanación exige al menos una referencia distinta y exactamente las asignaturas del expediente. Cada cambio registra anterior, nueva referencia, responsable, fecha y sustento. Cambiar un sílabo incrementa su versión e invalida su evaluación actual; conserva todas sus evaluaciones anteriores. No modifica notas, créditos ni las asignaturas de origen presentadas.

## Evaluación, dictamen y resolución

La decisión individual es explícita: aprobar, rechazar u observar, con justificación. Se conservan los criterios ilustrativos previos de nota mínima 11 y créditos suficientes. Para aprobar se requiere coincidencia desde 80%, o desde 70% con la excepción seleccionada de afinidad temática directa, justificación específica y créditos exactamente equivalentes. Los porcentajes nunca generan aprobaciones automáticas.

No puede emitirse el dictamen si hay cursos pendientes u observados. El dictamen conserva una instantánea JSON de cada resultado, créditos, nota, porcentaje, justificación, excepción y fecha de evaluación. Una devolución decanal mantiene esa instantánea, su observación y versión; solicita una nueva revisión y un dictamen nuevo. Las evaluaciones anteriores también permanecen consultables.

La resolución se emite únicamente después de aprobar el dictamen. Requiere un número ficticio que comience por `DEMO-` y una fecha ficticia entre 2000 y 2100; evita reutilizar un número dentro de la sesión. Conserva la instantánea del dictamen aprobado y muestra resultados aprobados y rechazados, créditos, decisión total/parcial/sin aprobaciones, expediente, estudiante y referencia al Decano simulado.

Las vistas de dictamen y resolución incluyen:

**DOCUMENTO ACADÉMICO DE DEMOSTRACIÓN — SIN VALIDEZ OFICIAL.**

La resolución se consulta en HTML, sin PDF ni firma digital. Aprobar el dictamen no cambia sus resultados individuales.

La acción **Notificar** crea avisos mínimos para Estudiante y Registros Académicos, con enlace a la resolución. La campana conserva los avisos temporales del sistema y muestra hasta cinco notificaciones recientes del perfil, sin inventar un estado de lectura. No se implementó un centro de notificaciones ni historial avanzado.

Registros crea una entrada únicamente para cada curso aprobado en la resolución y confirma la carga. No duplica registros ni carga cursos rechazados. Un dictamen sin cursos aprobados puede completar el proceso dejando constancia de que no hay créditos por cargar. La finalización exige la confirmación de carga y comprueba su coherencia.

## Modelos y migración

Se mantienen EF Core, SQLite y el acceso directo mediante servicios y controladores. No se agregaron frameworks ni dependencias de servicios externos.

| Modelo | Cambio |
| --- | --- |
| `Solicitud` | Versión de proceso, documentos, confirmación de verificación/carga y nuevas colecciones; conserva las columnas originales |
| `AsignaturaSolicitud` | Detalles independientes por curso y fotografía de los créditos de destino |
| `EvaluacionAsignatura` | Evaluaciones sucesivas con responsable, documento evaluado y fecha |
| `Seguimiento` | Añade estado anterior, acción y nombre histórico del responsable; conserva las columnas originales |
| `CorreccionDocumento` | Referencias anterior/nueva, curso cuando corresponde, justificación, perfil y fecha |
| `DictamenAcademico` | Versiones, instantánea de resultados y observación decanal |
| `ResolucionDecanal` | Número/fecha ficticios e instantánea del dictamen aprobado |
| `NotificacionExpediente` | Avisos mínimos de resolución para los dos destinatarios |
| `RegistroAcademico` | Una entrada por curso aprobado, ligada a resolución y responsable |

Migración nueva: **`20261110034722_FlujoConvalidacion`**, con su diseñador y actualización de `AppDbContextModelSnapshot`.

La migración agrega columnas y tablas, copia el curso de cada expediente anterior a la nueva tabla y conserva sus identificadores, estados, notas, porcentajes, fechas, ámbito y comentarios originales. Marca esos expedientes con `VersionProceso = 1` y conserva su consulta histórica sin someterlos silenciosamente a reglas nuevas. El proceso nuevo usa versión 2.

Los valores históricos de perfil se conservan numéricamente y su nombre original se copia a `PerfilHistorico`; así las actuaciones de Secretaría y Comisión académica siguen atribuidas a sus responsables originales. No se inventan documentos ni evaluaciones detalladas que la versión anterior no almacenaba.

Los nuevos ejemplos se crean únicamente en ámbitos sin expedientes. Hay seis casos con dos cursos cada uno: presentada, pendiente de subsanación, evaluación académica, revisión decanal, registro pendiente y finalizada con aprobación parcial. Se generan mediante el mismo servicio y máquina de estados, con los cinco responsables actuales. No se reescriben expedientes existentes para reemplazarlos por semillas nuevas.

Todas las entidades dependientes que se consultan desde la aplicación tienen filtros por el ámbito de su solicitud. El ámbito lo genera el servidor y no se acepta desde formularios. El token `Revision` del expediente protege también evaluaciones, correcciones y carga; `SaveChangesAsync` guarda estado, detalles, historial y nueva revisión de forma atómica.

No se borró ni reinicializó la base local del usuario. La verificación funcional utilizó `artifacts/flujo-qa.db`; las pruebas .NET usan SQLite en memoria.

## Pruebas ejecutadas

| Verificación | Resultado observado |
| --- | --- |
| Bloque 1 antes de integrarlo | Release sin advertencias/errores; 31 pruebas correctas |
| `dotnet restore` | Correcto |
| `dotnet build -c Release --warnaserror` | 0 advertencias, 0 errores |
| `dotnet test -c Release` | 51 correctas, 0 fallidas, 0 omitidas |
| `dotnet ef migrations has-pending-model-changes --project src/Convalidacion.Web --configuration Release --no-build` | Sin cambios pendientes |
| Kestrel real en `http://127.0.0.1:5285` | Inició con la base exclusiva de pruebas y aplicó/conservó sus migraciones |
| Chrome con DevTools | 207 comprobaciones correctas; 0 errores JavaScript y 0 recursos HTTP fallidos durante el recorrido |

Se adaptaron los escenarios de regresión de los tres perfiles al proceso aprobado y se añadieron pruebas nuevas. Se mantienen registro, validación, antiforgery, errores HTTP, filtros, control de perfiles, concurrencia y aislamiento. La cobertura adicional verifica:

1. Varias asignaturas, catálogo y referencias documentales requeridas.
2. Observación, devolución, subsanación y verificación de Admisión.
3. Evaluación individual, criterios insuficientes, excepción del 70%, créditos equivalentes y decisión manual.
4. Prohibición de emitir dictámenes incompletos, devolución decanal y conservación de versiones.
5. Resolución con aprobación parcial, notificaciones, exclusión de rechazos y cierre sin aprobaciones.
6. Registro sin duplicados y finalización condicionada a la carga.
7. Revisión desactualizada y concurrencia real de dos contextos.
8. Cinco perfiles, panel automático y conservación del expediente activo.
9. Aislamiento de expedientes, comentarios, dictámenes, resoluciones y avisos entre navegadores.
10. Migración desde el esquema anterior con comparación de todos los campos originales, conservación de autores, claves foráneas y persistencia entre contextos.

El navegador recorrió los cinco paneles en 1366 × 768, 1920 × 1080, 1024 × 768, 768 × 1024, 390 × 844 y 320 × 740. Comprobó selector, logo, ausencia de desbordamiento general, menú móvil, agregar/quitar cursos y validaciones. Recorrió un expediente de tres asignaturas, subsanó una observación, emitió un dictamen, lo devolvió desde el Decano, revaluó y emitió otra versión, aprobó parcialmente, notificó, cargó dos cursos y excluyó el tercero, finalizando con 24 eventos conservados.

Se abrieron las capturas de paneles, registro, evaluación y resolución en escritorio/móvil para inspeccionar el resultado visual. El diseño del Bloque 1 se conserva; las pantallas nuevas amplían el contenido dentro del mismo layout.

Evidencia local ignorada por Git:

- `artifacts/TestResults/bloque2-final.trx`
- `artifacts/bloque2-tests-final.log`
- `artifacts/flujo-browser-qa.json`
- `artifacts/flujo-panel-{perfil}-{ancho}.png`
- `artifacts/flujo-registro-multiple.png`, `flujo-evaluacion-individual.png`, `flujo-subsanacion.png`
- `artifacts/flujo-dictamen-v1-{ancho}.png`, `flujo-dictamen-v2-{ancho}.png`, `flujo-resolucion-{ancho}.png`, `flujo-expediente-finalizado-{ancho}.png`

El script reproducible es `tests/browser/flujo-convalidacion.mjs`. Requiere Node 24, una instancia de pruebas separada y Chrome con DevTools; no instala paquetes npm. Usa por defecto `http://127.0.0.1:5285`, puerto DevTools `9225` y carpeta `artifacts`. Se pueden configurar mediante `QA_ORIGIN`, `QA_CDP_PORT` y `QA_ARTIFACT_DIR`. Crea datos ficticios, por lo que debe apuntar a una base exclusiva de pruebas.

## Incidencias y correcciones

- La vista previa anterior mantuvo bloqueado el ejecutable del Bloque 1; se identificó su ruta y se detuvo únicamente ese proceso antes de compilar.
- Se corrigieron dos avisos del analizador de xUnit en las pruebas nuevas. La compilación final con advertencias como errores pasó sin avisos.
- Se preservó explícitamente la autoría histórica para evitar reinterpretar los valores numéricos antiguos de perfil.
- Se amplió el selector en escritorio para los nombres definitivos y se comprobó su accesibilidad hasta 320 px.
- El registro del sustento de evaluación conserva el texto completo dentro del límite del historial; el detalle de la asignatura y su versión queda en la entidad de evaluación.

El entorno restringido de Windows sigue registrando mensajes de DataProtection/DPAPI al intentar abrir el almacén de claves del usuario. También aparecen avisos de antiforgery al imponer `no-cache, no-store`. Son condiciones del entorno/configuración ya presentes antes de este bloque; no impidieron el recorrido real, la validación de formularios, las sesiones ni las pruebas. No se cambió la configuración de claves del usuario.

No se ejecutaron Docker, Render ni GitHub Actions en este bloque. Dockerfile, `render.yaml` y `Program.cs` permanecen sin cambios.

## Git y revisión

Se inspeccionó el árbol limpio, las ramas y el historial. Se verificó `feature/rediseno-usmp` (`1a15a24`) mediante compilación y sus 31 pruebas; se actualizó `origin` y se integró por fast-forward en `develop`, conservando el historial. Desde ese `develop` se creó **`feature/flujo-convalidacion`**.

El commit del Bloque 2 se crea sólo en esa rama. `main` sigue en `e3745f4` y `develop` en `1a15a24`. No se fusiona el Bloque 2 con ninguna de ellas, no se hace push ni se despliega. El hash definitivo se entrega junto con el resultado y puede consultarse con `git log -1 --oneline`.

Para revisar localmente con datos separados de la base habitual:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
$env:ConnectionStrings__DefaultConnection = "Data Source=$((Join-Path (Get-Location) 'artifacts/flujo-revision.db'))"
dotnet run --project src/Convalidacion.Web -c Release --no-build --no-launch-profile --urls http://localhost:5283
```

Abrir `http://localhost:5283`, recorrer el selector y usar **Expediente activo** al cambiar de perfil. La [guía principal](../README.md) describe las acciones de cada responsable. La primera ejecución sobre la base de revisión aplica las migraciones y prepara los ejemplos, sin borrar datos existentes.

## Pendiente del Bloque 3

No se implementaron calculadora avanzada, KPI completos, comparación AS-IS/TO-BE, páginas nuevas de preguntas frecuentes ni un centro avanzado de notificaciones. Los conteos existentes del dashboard se mantienen con datos reales y los avisos mínimos del flujo funcionan. El Bloque 2 queda para revisión antes de continuar.
