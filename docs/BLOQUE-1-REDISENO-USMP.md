# Bloque 1: rediseño visual USMP

Implementación local para revisión. Este bloque no reorganiza perfiles ni amplía el proceso de negocio.

## Resultado visual

Se inspeccionaron visualmente `docs/referencias/mockup-usmp.png` y el JPG institucional antes de implementar. Se reprodujeron la estructura lateral granate, la barra superior, el banner con campus, la distribución del dashboard, las tarjetas blancas, los bordes redondeados, las sombras y los accesos con iconos uniformes.

El selector permanece arriba a la derecha y conserva su formulario POST, antiforgery, valores de perfil y `returnUrl`. El mismo layout se utiliza en inicio, bandeja, registro, detalle, información de la demo y páginas de error. Los colores aproximados del mockup, espaciados y componentes se centralizan en variables y reglas de `site.css`; no se presentan como códigos oficiales de marca.

El dashboard utiliza los datos existentes: solicitudes recientes, estados, asignaturas e indicadores. El resumen elige la solicitud pendiente más reciente entre los cinco expedientes que ya entrega el dashboard y declara ese alcance; si no encuentra ninguna, permite consultar la bandeja completa. No se añadieron consultas al controlador.

La búsqueda superior envía el parámetro `buscar` a la bandeja existente. La campana presenta el aviso de confirmación de la página generado mediante `TempData["Mensaje"]`; su contador sólo aparece cuando existe ese aviso. No se creó un sistema de notificaciones persistentes.

## Componentes reutilizables

| Archivo en `Views/Shared` | Uso |
| --- | --- |
| `_Layout.cshtml` | Estructura común, contenido, aviso y pie institucional |
| `_MenuLateral.cshtml` | Menú responsive, navegación activa y acceso a pendientes por perfil |
| `_BarraSuperior.cshtml` | Búsqueda y presentación de los avisos existentes |
| `_SelectorPerfil.cshtml` | Selector de los tres perfiles y formulario original |
| `_Icon.cshtml` | Iconos SVG locales mediante una sola colección de símbolos |
| `_Aviso.cshtml` | Confirmaciones del sistema, con `role="status"` |
| `_EstadoSolicitud.cshtml` | Etiquetas de los cinco estados, reutilizadas en tabla, resumen y detalle |
| `_EtapasProceso.cshtml` | Guía visual de las cuatro macroetapas solicitadas |
| `_ResumenSolicitud.cshtml` | Solicitud pendiente dentro de los expedientes recientes |
| `_SolicitudesTabla.cshtml` | Tabla original compartida, con desplazamiento horizontal accesible |

Se usa Bootstrap ya instalado para el menú móvil y el desplegable de avisos. No se añadieron frameworks, dependencias de producción ni bibliotecas externas de iconos. El JavaScript existente de validación y prevención de doble envío permanece intacto.

## Archivos modificados

- `src/Convalidacion.Web/Views/Home/Index.cshtml`
- `src/Convalidacion.Web/Views/Shared/_Layout.cshtml`
- `src/Convalidacion.Web/Views/Shared/_Layout.cshtml.css` — sustituye estilos residuales de la plantilla por una referencia al CSS común.
- `src/Convalidacion.Web/Views/Shared/_SolicitudesTabla.cshtml`
- `src/Convalidacion.Web/Views/Solicitudes/Detalle.cshtml` — reutiliza la etiqueta de estado; conserva todos los formularios y acciones.
- `src/Convalidacion.Web/wwwroot/css/site.css`

## Archivos incorporados

- Los ocho parciales nuevos: `_MenuLateral`, `_BarraSuperior`, `_SelectorPerfil`, `_Icon`, `_Aviso`, `_EstadoSolicitud`, `_EtapasProceso` y `_ResumenSolicitud`.
- `src/Convalidacion.Web/wwwroot/images/icons.svg` — símbolos vectoriales locales.
- `src/Convalidacion.Web/wwwroot/images/usmp-logo.jpg` — archivo local proporcionado, sin modificaciones.
- `docs/referencias/mockup-usmp.png` — referencia local proporcionada, sin modificaciones.
- `src/Convalidacion.Web/wwwroot/images/usmp-campus-referencia.png` — copia idéntica del mockup. CSS encuadra únicamente la fotografía del banner y la ilustración lateral, sin mostrar ni incorporar como interfaz los controles del mockup. Se conserva el original en documentación.
- Este informe.

## Comportamiento conservado

- Perfiles Estudiante, Secretaría y Comisión académica; cambio de perspectiva dentro de la misma sesión y permanencia en la ruta local original.
- Registro de una asignatura por solicitud, todos los campos, validaciones de cliente/servidor y confirmaciones documentales simuladas.
- Búsqueda y filtros por estado; consulta del detalle y del historial.
- Revisión, observación, subsanación, aprobación y rechazo, con permisos simulados, criterios ilustrativos y control de concurrencia existentes.
- Formularios POST, tokens antiforgery, mensajes de confirmación, estados HTTP y aislamiento entre navegadores.
- Rutas y controladores, servicios, modelos, relaciones, migraciones, configuración y archivos de despliegue sin cambios.

## Verificación realizada

| Comprobación | Resultado |
| --- | --- |
| `dotnet restore` | Correcto; dependencias actualizadas |
| `dotnet build -c Release` | Correcto; 0 advertencias, 0 errores |
| `dotnet test -c Release` | 31 correctas, 0 fallidas, 0 omitidas |
| Inicio real de Kestrel | Correcto en `http://127.0.0.1:5284` |
| `GET /health` | HTTP 200, `{"status":"ok"}` |
| Chrome mediante DevTools | 117 comprobaciones correctas |
| JavaScript y carga de recursos durante la prueba de navegador | 0 excepciones registradas y 0 respuestas HTTP de error |

La verificación del navegador cubrió inicio, bandeja, formulario, detalle e información de la demo en 1366 × 768, 1920 × 1080, 1024 × 768, 768 × 1024, 390 × 844 y 320 × 740. Se comprobaron la ausencia de desbordamiento horizontal general, carga del logotipo y acceso al selector en cada combinación. También se probaron apertura/cierre del menú móvil, enlaces laterales, búsqueda superior, filtros, los tres perfiles, conservación de expedientes al cambiar de perfil, avisos y campana, validación del formulario y el flujo real de registro → revisión → aprobación con historial.

Se abrieron las capturas y se compararon visualmente con el mockup. La revisión detectó y corrigió el recorte de la imagen de fondo en móviles, el ajuste del selector a 320 px y una barra vertical innecesaria dentro de la tabla.

Las pruebas usaron exclusivamente `artifacts/usmp-qa.db`, separado de la base local de la aplicación. Los resultados y capturas están en `artifacts/`, que ya estaba ignorado por Git:

- `usmp-browser-qa.json`: resultados de las 117 comprobaciones.
- `usmp-browser-qa.mjs`: script local de comprobación mediante Chrome DevTools, sin dependencias npm.
- `usmp-inicio-1366.png`, `usmp-inicio-1920.png`, `usmp-inicio-1024.png`, `usmp-inicio-768.png`, `usmp-inicio-390.png`, `usmp-inicio-320.png`.
- `usmp-formulario-1366.png`, `usmp-formulario-390.png`, `usmp-detalle-390.png`, `usmp-acerca-390.png`, `usmp-menu-movil.png`.
- `usmp-perfil-secretaria.png`, `usmp-perfil-comision.png`.

Durante el arranque en el entorno restringido aparecieron mensajes de DataProtection/DPAPI relativos al almacén de claves del usuario. No impidieron servir la aplicación ni completar los formularios y cambios de perfil. La configuración del backend no se modificó. También se registran los avisos habituales de antiforgery al imponer `no-cache, no-store`.

## Diferencias respecto al mockup

- El JPG proporcionado es rojo sobre blanco; se muestra íntegro y sin deformación dentro de un contenedor blanco. No coincide con el logotipo blanco sobre granate de la referencia.
- No se recibió una fotografía independiente del campus: el banner encuadra la incluida en el mockup. Una fotografía original permitiría mejorar la nitidez y evitar distribuir una copia completa de la referencia como recurso visual.
- Los nombres de las etapas son los cuatro aprobados en el pedido, más largos que los textos del mockup. Son una guía y no simulan porcentajes de avance ni etapas completadas.
- Los accesos corresponden a funciones reales. Calculadora, equivalencias, preguntas frecuentes, ayuda y contacto no tienen pantallas independientes en la aplicación actual.
- El resumen muestra el estado existente y un enlace al historial; no presenta un porcentaje «2 de 4 etapas» que el backend no registra.
- Se mantienen las columnas originales, las cinco solicitudes recientes y los cuatro indicadores dinámicos. La referencia muestra tres filas y otros campos; a 1366 × 768 la página requiere desplazamiento vertical para ver el contenido completo.
- No se inventa una identidad autenticada como la del mockup: la cabecera identifica el perfil simulado.
- Los beneficios describen capacidades comprobables de la demo, sin afirmar reducciones de tiempo medidas.

## Pendientes del Bloque 2

Estas diferencias se documentan, sin implementarlas en este bloque:

1. Reorganizar los tres perfiles actuales en los cinco perfiles aprobados y definir sus responsabilidades/rutas.
2. Definir cómo las cuatro macroetapas se relacionan con estados, decisiones, resolución, notificación y registro académico. Los estados actuales siguen siendo Recibida, EnRevision, Observada, Aprobada y Rechazada.
3. Evaluar pantallas y funcionalidades independientes de dictámenes, resoluciones y registros académicos; hoy no existen.
4. Evaluar requisitos, equivalencias, calculadora, ayuda y contacto independientes. No se crearon enlaces vacíos.
5. Definir, si corresponde, notificaciones persistentes y una pantalla propia de indicadores; actualmente existen avisos temporales e indicadores en el dashboard.

## Git y revisión local

Se inspeccionaron estado, ramas, remotos e historial. Las únicas novedades locales iniciales eran el mockup y el logotipo. Se actualizaron las referencias de `origin`, se verificó `develop` mediante fast-forward y se creó `feature/rediseno-usmp` desde `develop` (`e3745f4`). No se trabajó sobre `main`, no se descartaron archivos y no se fusionó ni desplegó.

Desde la raíz del repositorio:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project src/Convalidacion.Web -c Release --no-build --no-launch-profile --urls http://localhost:5283
```

Abrir `http://localhost:5283`. Probar los tres perfiles con el botón **Cambiar**, abrir una solicitud desde la tabla, recorrer el formulario y reducir el ancho de la ventana para revisar el menú móvil. El selector conserva la página actual al cambiar de perfil.

Para revisar con una base de pruebas separada, antes de ejecutar `dotnet run`:

```powershell
$env:ConnectionStrings__DefaultConnection = "Data Source=$((Join-Path (Get-Location) 'artifacts/usmp-revision.db'))"
```

La rama y el hash del commit se pueden consultar con `git branch --show-current` y `git log -1 --oneline`. El hash definitivo se incluye en la entrega. El Bloque 1 queda preparado para revisión; el Bloque 2 no se inició.
