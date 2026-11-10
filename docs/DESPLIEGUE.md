# Publicar la demo en Render Free

El servicio se declara en `render.yaml`: **plan free**, Docker, rama `main`, salud `/health`, SQLite en `/tmp/convalidacion/convalidacion.db`. No declara bases externas, discos persistentes, trabajos de pago ni secretos.

## Conectar con GitHub

1. Inicia sesión en [Render](https://dashboard.render.com/).
2. Selecciona **New → Blueprint**.
3. Conecta GitHub si Render lo solicita. Autoriza el acceso al repositorio **jodach34/DEMO-LUCIANA**, sin extenderlo a otros repositorios innecesariamente.
4. Selecciona `https://github.com/jodach34/DEMO-LUCIANA`, rama `main`, archivo `render.yaml` de la raíz.
5. Revisa que el único recurso sea `fia-epics-convalidaciones-demo` con plan **Free**. No aceptes cambios a un plan de pago ni agregues discos, bases o recursos adicionales. Si Render exige un recurso de pago, detente y solicita autorización antes de continuar.
6. Aplica el Blueprint. El primer despliegue construye el Dockerfile de la raíz. Los siguientes despliegues están configurados para esperar los checks de GitHub mediante `autoDeployTrigger: checksPass`.

Alternativa si prefieres crear el recurso manualmente: **New → Web Service**, conectar el mismo repositorio, elegir `main`, runtime **Docker**, contexto `.`, ruta `./Dockerfile`, plan **Free**. Usar las variables y la comprobación de salud indicadas abajo.

## Valores exactos

| Campo | Valor |
| --- | --- |
| Repositorio | `https://github.com/jodach34/DEMO-LUCIANA` |
| Rama | `main` |
| Runtime | Docker |
| Dockerfile | `./Dockerfile` |
| Contexto Docker | `.` (raíz) |
| Instancia | Free |
| Health check | `/health` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `Data Source=/tmp/convalidacion/convalidacion.db` |
| `Demo__SeedData` | `true` |
| `PORT` | Usar el que suministra Render; la aplicación lo lee automáticamente |
| Discos / volúmenes | Ninguno |

No se necesitan credenciales dentro de la aplicación. La conexión GitHub–Render se autoriza desde sus interfaces, sin agregar tokens al repositorio.

## Verificar el primer despliegue

1. En **Events**, espera un despliegue finalizado correctamente y revisa que corresponda al commit actual de `main`.
2. En los logs, confirma la aplicación de `InitialCreate` y `IsolateDemoSessions` en un disco vacío, seguida de `Now listening on: http://0.0.0.0:<PORT>`.
3. Abre la URL asignada por Render y luego `/health`; debe responder HTTP 200 y `{"status":"ok"}`.
4. Recorre registro → Secretaría → Comisión → aprobación con datos ficticios. Comprueba filtros e historial.
5. Abre una ventana privada o un navegador distinto. Debe tener sus propios ejemplos, sin mostrar las solicitudes o comentarios del primer navegador. Copiar la URL de un expediente ajeno debe devolver HTTP 404.
6. Tras un reinicio o suspensión, comprueba que la aplicación vuelva a funcionar y prepare una sesión nueva. La pérdida de datos y sesiones es esperada en esta demo efímera.
7. Solo comunica una URL como desplegada después de estas comprobaciones. Un archivo `render.yaml` o una construcción correcta en GitHub no equivalen a un despliegue Render.

## Costos y límites

Mantén el plan **Free** y no habilites upgrades, discos ni recursos de pago. Revisa en la cuenta los límites y ajustes de facturación antes de aceptar cualquier cambio con costo. Render puede contabilizar ancho de banda y minutos de construcción; el plan gratuito no es una garantía de consumo ilimitado. La política de esta demo es solicitar autorización antes de cualquier acción con costo.

Render Free usa almacenamiento efímero y puede suspender servicios por inactividad. El código reconstruye el esquema al iniciar y separa los datos por sesión de navegador. No es autenticación, no protege datos académicos reales y no debe recibirlos. Al caducar la sesión, reiniciar el proceso o perder el disco, una sesión nueva vuelve a recibir ejemplos ficticios.

Referencias: [Render Free](https://render.com/docs/free), [Blueprint YAML](https://render.com/docs/blueprint-spec), [Docker en Render](https://render.com/docs/docker).
