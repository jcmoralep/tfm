# Ruta de Producto

Plataforma interna para que personas de producto lleven una iniciativa desde la idea hasta artefactos listos para desarrollo, con un asistente que aplica el método BMAD. El alcance y las reglas están en `AGENTS.md`, `REQUISITOS.md` y `PROPUESTA-MVP.md`.

> **Nota de marcas.** BMad y BMAD-METHOD son marcas de BMad Code, LLC. Este proyecto no está afiliado ni avalado por ellos: usa el método BMAD (licencia MIT del código) y solo lo menciona para describir con qué es compatible. Por eso el producto no lleva «BMAD» en su nombre.

Estado actual: **paso 7 (contexto adjunto)** de `PROPUESTA-MVP.md` §7, **entregado** (cambio `context-attachments`, 2026-10-15), sobre el paso 6 (Arquitectura y Épicas e historias, cambio `architecture-epics`, 2026-10-14). Incluye la solución por capas, MySQL, Serilog, el inicio de sesión, el estilo visual base, las iniciativas, la conversación guiada, la generación, edición y aprobación del Brief, el PRD, la especificación, la Arquitectura y las Épicas e historias, el contexto adjunto de cada iniciativa (archivos, enlaces y notas) y la opción de que Gemini redacte las preguntas y los documentos (`Assistant:Provider=Gemini`; por defecto sigue el asistente de demostración). Sigue pendiente la exportación completa (paso 8).

## Advertencia sobre Gemini (capa gratuita)

Con `Assistant:Provider=Gemini`, el asistente usa la **capa gratuita de Gemini**. En esa capa, Google puede usar los datos enviados para mejorar sus productos. Mientras se use la capa gratuita, trabaje **solo con datos de ejemplo o no sensibles**. Antes de usar iniciativas reales de la empresa, es necesario conectar un modelo de la compañía o migrar a un plan de pago. La clave de API va en user-secrets o en la variable de entorno `Gemini__ApiKey`, nunca en el repositorio. La interfaz **no muestra** este aviso (decisión del usuario, RN-05); queda solo en la documentación.

## Asistente: Fake o Gemini

La conversación con el asistente (`/iniciativas/{id}/asistente`) y la redacción de los documentos tienen dos proveedores, elegidos con `Assistant:Provider`:

- `Fake` (por defecto; también si está vacío o ausente): guion fijo y generador determinista. No necesita clave ni red y no envía nada a servicios externos.
- `Gemini`: el modelo redacta cada pregunta con un ejemplo a medida de la iniciativa, insiste una sola vez por tema cuando la respuesta es floja (nunca ante "No sé" ni respuestas rápidas) y redacta la especificación, el Brief y el PRD a partir de lo respondido, marcando como suposición o pregunta pendiente todo lo que no se dijo. El PRD de una iniciativa Estándar parte del Brief aprobado. El orden de las preguntas, el nivel y el modo los decide siempre el guion, no el modelo.

Cómo ejecutar con Gemini (el valor de la clave es suyo; no lo escriba en archivos del repositorio):

```bash
dotnet user-secrets set "Gemini:ApiKey" "<su clave>" --project src/RutaProducto.Web
# y active el proveedor, por ejemplo con una variable de entorno:
# Assistant__Provider=Gemini
```

También puede usar la variable de entorno `Gemini__ApiKey`. Con `Gemini` activo y sin clave, la aplicación no arranca y el mensaje nombra `Gemini__ApiKey` (nunca el valor). Para volver al asistente de demostración, quite la variable o use `Assistant__Provider=Fake`.

| Opción | Por defecto | Descripción |
|---|---|---|
| `Assistant:Provider` | `Fake` | `Fake` o `Gemini` (sin distinguir mayúsculas); otro valor impide arrancar |
| `Gemini:ApiKey` | (sin valor) | Obligatoria con `Gemini`; solo user-secrets o `Gemini__ApiKey` |
| `Gemini:Model` | `gemini-3.6-flash` | Modelo; es configuración, se puede cambiar sin tocar código |
| `Gemini:BaseUrl` | `https://generativelanguage.googleapis.com/v1beta` | URL base absoluta |
| `Gemini:ThinkingLevel` | `low` | `low`, `medium` o `high` |
| `Gemini:TurnTimeoutSeconds` | `60` | Plazo de una pregunta del asistente (positivo) |
| `Gemini:DraftTimeoutSeconds` | `120` | Plazo de un documento (positivo) |
| `Gemini:MaxConcurrentCalls` | `2` | Llamadas simultáneas al modelo (positivo) |

Qué pasa cuando algo falla (RF-70):

- **Cuota agotada o tiempo agotado en la conversación:** la respuesta queda guardada, aparece el motivo y el botón **Reintentar**; no se puede enviar otra hasta reintentar. Un reintento produce exactamente una respuesta.
- **Límites de la capa gratuita (medido el 2026-10-13):** además de un tope por minuto que se recupera esperando medio minuto, Google aplica un **tope diario de peticiones por proyecto y por modelo** (cuota `GenerateRequestsPerDayPerProjectPerModel-FreeTier`). Con `gemini-3.6-flash` se agotó tras unas 30 llamadas en un día, y una iniciativa Estándar usa unas 12. Mientras dure el tope diario «Reintentar» no sirve; cada modelo tiene su propia cuota, así que cambiar `Gemini:Model` (por ejemplo a `gemini-3.1-flash-lite`) permite seguir. Los límites reales solo se ven en AI Studio.
- **Clave o modelo inválidos:** mensaje de configuración; corrija la opción y reintente.
- **Salida bloqueada o mal formada en un turno:** el asistente usa en silencio la redacción del guion y la conversación sigue.
- **Fallo al generar un documento:** mensaje con orientación junto al botón, no se guarda nada y la versión anterior queda igual. No hay generador falso de respaldo.

Privacidad: los registros (`Logs/`) no guardan el texto de mensajes ni de documentos, ni los prompts, ni las respuestas del modelo, ni la clave; solo identificadores, tema, modelo, estado, duración y conteo de tokens. Por ahora se prueba **solo con información muy básica** (RN-05).

## Artefactos (documentos de la iniciativa)

Desde el detalle de la iniciativa, la tarjeta **Artefactos** lista los documentos que su nivel necesita y los abre en `/iniciativas/{id}/artefactos/{tipo}`. Nada se genera solo: cada documento se crea con el botón **Generar**, a partir de las respuestas de la conversación.

- **Pequeña:** Especificación. **Estándar:** Brief y PRD. **Grande:** PRD, Arquitectura y Épicas e historias; la Arquitectura se puede generar cuando el PRD está aprobado, y las Épicas e historias cuando lo están el PRD y la Arquitectura.
- Cada documento se guarda como **Markdown** con un número de versión y tiene un estado: **Borrador** o **Aprobado**. Solo se puede aprobar un borrador, y solo la persona dueña de la iniciativa.
- **Editar** abre el Markdown en un editor (máximo 20.000 caracteres). Cada cambio guardado sube la versión y devuelve el documento a Borrador; si la iniciativa estaba Lista para construir, vuelve a Planificando. Guardar el mismo texto no cambia nada.
- **Regenerar** reemplaza todo el contenido, incluidos los cambios manuales, y pide confirmación antes de hacerlo.
- Si el Markdown pierde una sección o un identificador (`FR-1`, `NFR-1`, `A-1`…), la página muestra avisos de formato. Los avisos nunca impiden guardar ni aprobar.
- **Preguntas pendientes.** Cada "No sé" de la conversación queda como una línea `- **Q-n**: texto (Responsable: …; Revisar: …)`. Las que genera la plataforma terminan en `(Responsable: por asignar; Revisar: por definir)`. El sufijo se lee solo al final de la línea; el texto de la pregunta puede llevar paréntesis.
- **No se puede aprobar un borrador con preguntas sin asignar** (falta responsable o condición de revisión). La página lo explica con un aviso ámbar y la lista de las preguntas. Los documentos ya aprobados antes de este cambio no se vuelven a revisar, pero si se editan vuelven a Borrador y la regla se aplica de nuevo.
- El panel **Preguntas pendientes** (encima del documento, solo si hay preguntas) permite escribir responsable (con sugerencias: Legal, TI, Finanzas, Quien propone la iniciativa) y condición de revisión de todas las filas y guardar una sola vez. Guardar cuenta como una edición: versión nueva, Borrador y, si la iniciativa estaba Lista para construir, Planificando. Se puede guardar a medias; el editor de texto sigue disponible.
- **Regenerar** devuelve todas las preguntas a «por asignar»: los responsables asignados se pierden junto con los cambios manuales.
- En el PRD, la sección 2.3 "Recorridos clave" ya no inventa recorridos: lleva una sola suposición `[ASSUMPTION: A-n]` hasta que se escriba un caso real. Los identificadores generados llevan guion (`FR-1`, `NFR-1`, `UJ-1`, `A-1`, `Q-1`); la decisión y sus excepciones están en `docs/bmad-referencia.md` §4.
- Conformidad con BMAD: ver «Cierre de conformidad» en `docs/bmad-referencia.md`; lo que va más allá de BMAD está en `docs/mejoras-posteriores.md`.
- La iniciativa pasa a **Lista para construir** cuando todos los documentos de su nivel están aprobados. Una iniciativa Grande termina con el PRD, la Arquitectura y las Épicas e historias aprobados; son borradores para revisión del equipo de desarrollo, y si el documento anterior cambia después, la página del siguiente muestra un aviso (no bloquea).
- Cada documento se puede **descargar en `.md`** y guardar como **PDF** desde la ventana de impresión del navegador (RF-21).
- Con `Assistant:Provider=Fake` (por defecto) el texto lo redacta un **generador falso y determinista** (`FakeArtifactDrafter`): produce siempre el mismo borrador con las mismas respuestas y no envía nada a Gemini. Con `Gemini`, el modelo redacta desde elementos con fuente (respuesta, suposición o desconocido) y no inventa datos (ver «Asistente: Fake o Gemini»); la Arquitectura y las Épicas e historias las redacta desde el PRD y la Arquitectura aprobados, con un esquema JSON propio, y el código numera y valida las citas.
- Los registros no guardan el contenido de los documentos.

## Requisitos previos

- SDK de .NET 10 (versión fijada en `global.json`).
- Docker, para la base de datos MySQL local (opcional si ya dispone de un servidor MySQL 8.4).
- Acceso a internet en la primera compilación: el build descarga el CLI independiente de Tailwind CSS v4.3.3 desde las publicaciones oficiales de GitHub, verifica su SHA-256 y lo guarda en `.tools/` (excluido de Git). No se requiere Node.js.

## Estructura

```
src/
  RutaProducto.Domain/          Entidades y reglas; sin dependencias
  RutaProducto.Application/     Comandos, consultas, handlers, behaviors e interfaces
  RutaProducto.Infrastructure/  EF Core + MySQL (Pomelo), Identity, migraciones
  RutaProducto.Web/             Blazor (Interactive Server), Tailwind, Serilog
tests/                          Pruebas por capa y reglas de dependencia
```

## Variables de entorno

Ningún secreto se guarda en el repositorio. La aplicación lee estas variables:

| Variable | Obligatoria | Descripción |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Sí | Cadena de conexión a MySQL. Ejemplo con el MySQL de Docker Compose: `Server=localhost;Port=3307;Database=rutaproducto;User=rutaproducto;Password=<contraseña>` |
| `Database__MySqlServerVersion` | No | Versión del servidor MySQL (por defecto `8.4.0`, la imagen de Docker Compose). Debe coincidir con el servidor al que se conecta: la imagen de Docker Compose en local o la del servicio MySQL en Railway |
| `SeedUsers__0__Email` | Sí, para poder entrar | Correo del primer usuario inicial |
| `SeedUsers__0__Password` | Sí, para poder entrar | Contraseña del primer usuario inicial |
| `SeedUsers__1__Email`, `SeedUsers__1__Password`, ... | No | Usuarios adicionales, con índices consecutivos |
| `ASPNETCORE_ENVIRONMENT` | No | `Development` activa la salida de registros por consola |

Los usuarios iniciales se crean al arrancar la aplicación si todavía no existen. Si un usuario ya existe, no se modifica (su contraseña no se sobrescribe). La contraseña debe cumplir la política de ASP.NET Core Identity: al menos 6 caracteres, con una mayúscula, una minúscula, un dígito y un carácter no alfanumérico. Si no la cumple, el usuario no se crea y el motivo queda en el registro.

Además, cualquier persona puede crear su propia cuenta en `/registro` (registro abierto, sin verificación de correo por ahora; ver RF-67). Antes de exponer la aplicación en un entorno público conviene agregar la verificación del correo o un código de invitación, y un captcha (pendiente).

En desarrollo también puede usar user-secrets en lugar de variables de entorno:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3307;Database=rutaproducto;User=rutaproducto;Password=<contraseña>" --project src/RutaProducto.Web
dotnet user-secrets set "SeedUsers:0:Email" "<correo>" --project src/RutaProducto.Web
dotnet user-secrets set "SeedUsers:0:Password" "<contraseña>" --project src/RutaProducto.Web
```

Para Docker Compose se usan además estas variables, que puede definir en un archivo `.env` en la raíz (excluido de Git):

| Variable | Descripción |
|---|---|
| `MYSQL_PASSWORD` | Contraseña del usuario de la aplicación (obligatoria) |
| `MYSQL_ROOT_PASSWORD` | Contraseña del usuario `root` (obligatoria) |
| `MYSQL_USER` | Usuario de la aplicación (por defecto `rutaproducto`) |
| `MYSQL_PORT` | Puerto local (por defecto `3307`, para no chocar con otro MySQL en `3306`; use la misma cifra en la cadena de conexión) |

## Archivos adjuntos (contexto de la iniciativa)

Los archivos del contexto se guardan en disco, nunca en MySQL (solo los metadatos). Claves de configuración (variables de entorno con `__`, por ejemplo `Storage__MaxFileSizeMb`):

| Clave | Por defecto | Descripción |
|---|---|---|
| `Storage:Provider` | `Local` | Solo existe `Local`. `S3` (o cualquier otro valor) hace fallar el arranque con un mensaje que nombra la clave |
| `Storage:LocalRootPath` | `App_Data/attachments` | Carpeta raíz de los archivos, relativa al directorio de trabajo. Está en `.gitignore` (`App_Data/`) |
| `Storage:MaxFileSizeMb` | `10` | Tamaño máximo por archivo, entero de 1 a 50 |

Límites por iniciativa: 20 elementos y 50 MB de archivos. Se rechazan los archivos vacíos y los ejecutables (`.exe .dll .bat .cmd .com .msi .scr .js .vbs .ps1 .sh .jar`); la descarga siempre se fuerza como adjunto.

- **En Railway el disco del contenedor es efímero:** los archivos locales se pierden en cada nuevo despliegue hasta que exista el almacenamiento S3.
- Una barra final en `Storage:LocalRootPath` (por ejemplo `/data/`) se ignora. Si un proceso se cae durante una subida puede quedar un archivo temporal `*.part` en la carpeta; no se lista, no cuenta en los límites y no se descarga, y se puede borrar a mano.
- **Contexto de texto y Gemini.** Con `Assistant:Provider=Gemini`, las notas y los archivos de texto (`.txt`, `.md`, `.markdown`, `.csv`) se envían a Gemini como datos de referencia, igual que el resto de la conversación, y por tanto a la capa gratuita descrita en la advertencia de arriba (la interfaz no avisa, RN-05). Con `Fake` no sale nada. Los enlaces, PDF, imágenes y demás archivos solo se guardan.
- **No hay análisis de malware.** La mitigación es la lista de bloqueo de extensiones y la descarga forzada como adjunto.

## Límite de intentos de acceso (RF-78)

Los POST de `/login`, `/registro`, `/confirmar-correo/reenviar`, `/recuperar-contrasena` y `/restablecer-contrasena` pueden limitarse por IP. Está **apagado por defecto**. Claves (variables de entorno con `__`, por ejemplo `RateLimiting__Enabled`):

| Clave | Por defecto | Descripción |
|---|---|---|
| `RateLimiting:Enabled` | `false` | `true` activa el límite; otro valor que no sea booleano impide arrancar |
| `RateLimiting:PermitLimit` | `10` | Intentos por ventana y por IP, entero de 1 a 1000 (solo se valida si está activo) |
| `RateLimiting:WindowSeconds` | `60` | Duración de la ventana en segundos, entero de 1 a 3600 (solo se valida si está activo) |

- El cupo es **uno por IP**, compartido por todos esos formularios. Detrás de un proxy (Railway) la IP que ve la aplicación es la del proxy hasta que se configuren las cabeceras reenviadas (cambio de preparación para Railway): sin ellas todas las personas compartirían un solo cupo, así que no active el límite en ese entorno antes.

## Captcha en el registro (RF-79)

El formulario de `/registro` puede exigir Cloudflare Turnstile. Está **apagado por defecto** (`Captcha:Provider=None`: sin widget ni solicitudes a Cloudflare). Claves (variables de entorno con `__`):

| Clave | Por defecto | Descripción |
|---|---|---|
| `Captcha:Provider` | `None` | `None` o `Turnstile` (sin distinguir mayúsculas); otro valor impide arrancar |
| `Turnstile:SiteKey` | (vacía) | Clave del sitio (pública); obligatoria con `Turnstile` |
| `Turnstile__SecretKey` | (sin valor) | Clave secreta; obligatoria con `Turnstile`; solo user-secrets (`Turnstile:SecretKey`) o variable de entorno, nunca en el repositorio |

- **Cómo obtener las claves:** cuenta gratuita de Cloudflare, sección Turnstile, widget gestionado para el dominio de la aplicación. Para probar sin cuenta hay claves de prueba: sitio `1x00000000000000000000AA` con secreto `1x0000000000000000000000000000000AA` (siempre pasa), secreto `2x0000000000000000000000000000000AA` (siempre falla) y sitio `2x00000000000000000000AB` (siempre bloquea). Úselas solo en su entorno local.
- La verificación **falla cerrado**: si Cloudflare no responde (10 s) o contesta un error, el formulario no se procesa. Para recuperar el registro durante una caída de Turnstile, use `Captcha__Provider=None` y reinicie.
- El script de Cloudflare es la única excepción a «vistas solo con clases CSS» (AGENTS.md §2). Los registros no contienen el token, la clave ni respuestas de Cloudflare: solo el resultado, la duración y códigos de error cortos.

## Correo electrónico (RF-80)

El correo de la cuenta se envía con una cola en segundo plano y un cliente SMTP. Hoy lo usa la confirmación del correo (RF-81); la recuperación de contraseña llegará después. Está **apagado por defecto** (`Email:Provider=None`: no hay cola ni proceso en segundo plano). Claves (variables de entorno con `__`):

| Clave | Por defecto | Descripción |
|---|---|---|
| `Email:Provider` | `None` | `None` o `Smtp` (sin distinguir mayúsculas); otro valor impide arrancar |
| `Email:RequireConfirmedEmail` | `false` | `true` activa la confirmación del correo (RF-81); exige `Email:Provider=Smtp` o el arranque falla |
| `Email:PublicBaseUrl` | (vacía) | Origen público para los enlaces de los correos (por ejemplo `https://ruta.example.com`); obligatorio con `Smtp`, `https` salvo `localhost` |
| `Smtp:Host` | (vacía) | Servidor SMTP; obligatorio con `Smtp` |
| `Smtp:Port` | `587` | 1 a 65535; el **465 (TLS implícito) no se admite** porque `System.Net.Mail` solo habla STARTTLS: use el 587 |
| `Smtp:EnableSsl` | `true` | STARTTLS; `false` solo se acepta con `localhost` o `127.0.0.1` (receptores de prueba) |
| `Smtp:Username` | (vacía) | Opcional; sin usuario no se envían credenciales |
| `Smtp__Password` | (sin valor) | Obligatoria solo si hay `Smtp:Username`; solo user-secrets (`Smtp:Password`) o variable de entorno, nunca en el repositorio |
| `Smtp:From` | (vacía) | Dirección del remitente; obligatoria con `Smtp` |
| `Smtp:FromName` | `Ruta de Producto` | Nombre del remitente |
| `Smtp:TimeoutSeconds` | `30` | 1 a 120 |

- **Proveedores gratuitos para probar o desplegar:** Brevo (plan gratuito con SMTP), Resend (SMTP con clave de API como contraseña) o una cuenta de Gmail con contraseña de aplicación (`smtp.gmail.com`, puerto 587). Compruebe en el plan del despliegue que el puerto 587 de salida está permitido.
- Los enlaces de los correos se construyen **solo** con `Email:PublicBaseUrl`, nunca con la cabecera `Host`.
- La cola es en memoria (100 mensajes): un envío fallido se registra y no se reintenta, y lo que esté en cola se pierde al reiniciar. Los registros guardan solo el tipo de correo, el tipo de excepción y el código de estado SMTP, nunca la dirección, el asunto, el cuerpo ni un enlace.

## Confirmación del correo (RF-81)

Con `Email__Provider=Smtp` y `Email__RequireConfirmedEmail=true`, el registro en `/registro` no inicia sesión: envía un enlace a `/confirmar-correo` (válido 24 horas) y la persona entra después de abrirlo. Un correo ya registrado recibe la misma pantalla y su dueña, un aviso «Ya tienes una cuenta». Los enlaces vencidos o usados se piden de nuevo en `/confirmar-correo/reenviar` (la página no existe con `Email:Provider=None`).

- **Cuentas anteriores:** las cuentas abiertas con el registro propio antes de activar esto quedan **sin confirmar**; al activarlo, esas personas piden su enlace en la página de reenvío. Los usuarios sembrados (`SeedUsers__*`) nacen confirmados y siguen entrando.
- **Redespliegue:** los enlaces se protegen con Data Protection; mientras sus claves no se persistan (preparación para Railway), un reinicio invalida los enlaces pendientes y la persona pide uno nuevo.
- **Probar en local:** `docker run --rm -d --name mailpit -p 1025:1025 -p 8025:8025 axllent/mailpit`, con `Email__Provider=Smtp`, `Email__RequireConfirmedEmail=true`, `Email__PublicBaseUrl=https://localhost:<puerto>`, `Smtp__Host=localhost`, `Smtp__Port=1025`, `Smtp__EnableSsl=false` y `Smtp__From=no-reply@ruta.local`; el correo se lee en `http://localhost:8025`.
- Si la confirmación estorba durante una caída del correo, `Email__RequireConfirmedEmail=false` y reiniciar la deja como antes.

## Base de datos

1. Inicie MySQL 8.4:

   ```bash
   docker compose up -d
   ```

2. Aplique las migraciones. Este comando lee **solo** la variable de entorno `ConnectionStrings__DefaultConnection` (no lee user-secrets):

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/RutaProducto.Infrastructure --startup-project src/RutaProducto.Infrastructure
   ```

   En PowerShell, defina antes la variable con `$env:ConnectionStrings__DefaultConnection = "<cadena>"`; en Bash, con `export ConnectionStrings__DefaultConnection="<cadena>"`.

3. Para crear una migración nueva (no necesita conexión, porque la versión de MySQL es explícita):

   ```bash
   dotnet ef migrations add <Nombre> --project src/RutaProducto.Infrastructure --startup-project src/RutaProducto.Infrastructure --output-dir Persistence/Migrations
   ```

Para detener la base de datos: `docker compose down` (agregue `-v` para borrar también los datos).

### Base de datos en Railway

La base de datos de producción es la plantilla MySQL de Railway. Se conecta por configuración, sin cambiar código ni migraciones:

1. Agregue el servicio MySQL desde la plantilla de Railway.
2. En el servicio de la aplicación, defina `ConnectionStrings__DefaultConnection` con variables de referencia. El nombre del servicio en la referencia debe coincidir con el del servicio MySQL (aquí, `MySQL`):

   ```
   Server=${{MySQL.MYSQLHOST}};Port=${{MySQL.MYSQLPORT}};Database=${{MySQL.MYSQLDATABASE}};User=${{MySQL.MYSQLUSER}};Password=${{MySQL.MYSQLPASSWORD}}
   ```

3. Defina `Database__MySqlServerVersion` con la versión de MySQL que ejecute el servicio. Debe coincidir con la imagen.
4. Las migraciones se aplicarán como comando previo al despliegue (pendiente de implementar; ver `AGENTS.md` §6).

`MYSQL_URL` de Railway tiene formato de URL y no sirve directamente como cadena de conexión de .NET.

## Ejecutar

```bash
dotnet run --project src/RutaProducto.Web
```

La aplicación queda disponible en `http://localhost:5080` (perfil `http`) o `https://localhost:7080` (perfil `https`). La página de inicio requiere sesión; sin ella, redirige a `/login`.

## Pruebas

```bash
dotnet test
```

Las pruebas no necesitan base de datos. Cubren el orden del pipeline de MediatR (registro, validación, handler), que los registros no incluyan el contenido de los comandos, la validación del inicio de sesión, la sincronía entre el modelo de EF Core y las migraciones, la protección contra redirecciones externas y las reglas de dependencia entre capas.

## Registros

Serilog escribe archivos de texto en `Logs/log-AAAAMMDD.log`, relativos al directorio de trabajo de la aplicación, con rotación diaria y un máximo de 7 archivos. En desarrollo, los registros también se muestran en la consola. Cada línea incluye un `CorrelationId` por petición HTTP o por circuito de Blazor. Nunca se registra el contenido de los comandos, solo su nombre y su duración.

## Estilos

Tailwind CSS se compila durante el build de .NET (`src/RutaProducto.Web/Tailwind.targets`): analiza los archivos `.razor` y genera `wwwroot/css/app.css`, que es un resultado de compilación y no se versiona. Todos los colores, tipografías, radios y sombras están en un único archivo, `src/RutaProducto.Web/Styles/theme.css`, con una paleta neutra provisional. Cuando se agregue `DESIGN.md`, basta con ajustar ese archivo.

## Versiones de dependencias relevantes

- **EF Core 9.0.x sobre .NET 10:** `Pomelo.EntityFrameworkCore.MySql` 9.0.0 es la versión estable más reciente y solo admite EF Core 9. Cuando Pomelo publique una versión estable para EF Core 10, se actualizarán juntos Pomelo, EF Core e Identity.EntityFrameworkCore (`Directory.Packages.props`).
- **MediatR 12.5.0:** es la última versión con licencia Apache-2.0. Desde la 13.0.0 la licencia es comercial.
