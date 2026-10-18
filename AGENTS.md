# AGENTS.md — Ruta de Producto

Guía para el agente de código (Claude Code / Gentle AI). Léela completa antes de escribir código.
Documentos hermanos en la raíz: `REQUISITOS.md` (qué debe hacer), `PROPUESTA-MVP.md` (alcance, flujo, modelo de datos, orden de construcción) y `DESIGN.md` (guía visual).

## 1. Propósito

Plataforma interna para que personas de producto **no técnicas** lleven una iniciativa desde una idea (a veces solo un video y prototipos hechos con IA) hasta artefactos **listos para que desarrollo construya**, guiadas por un asistente conversacional que aplica el método **BMAD**. La construcción posterior ocurre fuera de la plataforma (Gentle AI / BMAD en el repositorio de desarrollo).

Es un MVP interno y una demostración del flujo completo "iniciativa → construcción" para el área de negocio.

## 2. Stack (decidido)

| Capa | Decisión |
|---|---|
| Lenguaje / runtime | C# sobre la última LTS de .NET (**confirmar versión; sugerida .NET 10**) |
| Interfaz | **Blazor Web App, Interactive Server** |
| Estilos | **Tailwind CSS** con el **CLI independiente** (sin Node.js), integrado al build de .NET |
| JavaScript | Vanilla, solo con módulos ES e `IJSRuntime` para lo que Blazor no cubre (portapapeles, descargas, arrastrar y soltar). **Única excepción de terceros:** el script `api.js` de Cloudflare Turnstile, en `/registro` y `/recuperar-contrasena`, solo con `Captcha:Provider=Turnstile`; se carga directo desde Cloudflare (no se aloja ni se hace pasar por un proxy, lo prohíbe Cloudflare) y vive solo en `CaptchaWidget.razor` |
| Arquitectura | **Clean Architecture** modular |
| Patrón | **CQRS con MediatR** |
| Datos | **MySQL** con EF Core (proveedor `Pomelo.EntityFrameworkCore.MySql`) |
| Logs | **Serilog**, archivos `.log` de texto plano, rotación diaria, **retención máxima 7 días** |
| Autenticación | ASP.NET Core Identity, correo y contraseña, **sin roles** |
| IA del asistente | **Gemini (capa gratuita de Google)** detrás de `IAssistantService` |
| Adjuntos | `IFileStorage` con dos implementaciones intercambiables (disco local o S3), por configuración |
| Diseño visual | `DESIGN.md` estilo **Intercom** (VoltAgent/awesome-design-md) |

## 3. Estructura de la solución

```
RutaProducto/
├── src/
│   ├── RutaProducto.Domain/          (entidades, reglas; sin dependencias externas)
│   ├── RutaProducto.Application/     (comandos, consultas, handlers, behaviors, interfaces)
│   ├── RutaProducto.Infrastructure/  (EF Core/MySQL, almacenamiento, Gemini, exportadores)
│   └── RutaProducto.Web/             (Blazor, Tailwind, wwwroot/js)
├── Dockerfile                        (imagen de producción para Railway)
├── DESIGN.md
├── AGENTS.md
├── REQUISITOS.md
├── PROPUESTA-MVP.md
└── .gitignore                        (incluye Logs/ y archivos de secretos)
```

Reglas de dependencia: Web → Application → Domain; Infrastructure → Application → Domain. Domain no depende de nada.

## 4. Reglas técnicas

### 4.1 Principios
- Aplicar **SOLID** y **DRY**. Aplicar **YAGNI**: no construir roles, pantallas de configuración ni administración en el MVP.
- Código modular para crecer: cada módulo (Iniciativas, Asistente, Artefactos, Contexto) vive en su propia carpeta/feature en cada capa.
- **Monolito modular con un solo `ApplicationDbContext`** (decidido; no se crean varios contextos). Para que una futura separación en servicios sea viable:
  - Cada módulo define sus entidades con su propia clase `IEntityTypeConfiguration` dentro de su carpeta; el contexto solo las aplica.
  - Las tablas de cada módulo llevan un prefijo propio.
  - Un módulo no consulta las entidades ni las tablas de otro; se comunican por comandos, consultas o eventos de MediatR.
  - Una prueba de arquitectura hará cumplir esta regla cuando exista más de un módulo.
- Pruebas: por ahora solo **pruebas unitarias con xUnit**. Las de integración (WebApplicationFactory con Testcontainers) quedan para más adelante.
- Los mensajes de validación (FluentValidation) se escriben directamente en español; no hay localización por ahora.

### 4.2 CQRS con MediatR
- Un comando o consulta por caso de uso; un handler por cada uno.
- Los componentes Blazor inyectan `IMediator` y envían comandos y consultas; no contienen lógica de negocio.
- Un solo modelo de datos en MySQL: los comandos escriben, las consultas leen con `AsNoTracking`. No separar bases de lectura/escritura.
- Orden de behaviors: **Logging → Validación (FluentValidation) → Handler**.

### 4.3 Datos y Blazor Server
- En Blazor Server el circuito del usuario vive mucho más que una petición HTTP: **usar `IDbContextFactory<T>`**, nunca inyectar el `DbContext` directamente en componentes.
- Migraciones de EF Core en Infrastructure.

### 4.4 Serilog
- Paquetes (Web): `Serilog.AspNetCore`, `Serilog.Settings.Configuration`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`, `Serilog.Enrichers.Environment`, `Serilog.Enrichers.Thread`.
- Configuración desde `appsettings.json`. Archivo y consola en todos los entornos, con la misma plantilla de salida (Railway lee la salida estándar); `Microsoft` sigue en `Warning` y no se añade contenido de peticiones.
- Application usa `ILogger<T>` de Microsoft, no Serilog directamente.
- `LoggingBehavior<TRequest,TResponse>` registra nombre del request y duración. **No registrar el contenido completo de los comandos** (pueden contener información de iniciativas): solo nombre e identificadores.
- `CorrelationId` por petición/circuito mediante `LogContext`.
- Configuración base de archivo:

```json
"Serilog": {
  "Using": [ "Serilog.Sinks.File" ],
  "MinimumLevel": {
    "Default": "Information",
    "Override": { "Microsoft": "Warning", "System": "Warning" }
  },
  "WriteTo": [
    {
      "Name": "File",
      "Args": {
        "path": "Logs/log-.log",
        "rollingInterval": "Day",
        "retainedFileCountLimit": 7,
        "shared": true,
        "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
      }
    }
  ]
}
```
- `UseSerilogRequestLogging()` registra peticiones HTTP, no las acciones dentro del circuito Blazor; esas las cubre el behavior.
- La misma regla vale para el correo, el captcha y la autenticación (RF-78 a RF-82, RNF-02): ningún registro contiene direcciones de correo ni destinatarios, asuntos, cuerpos, tokens, enlaces (con o sin token), contraseñas, la respuesta del captcha ni claves secretas. Solo identificadores de usuario, el tipo de plantilla (`ConfirmEmail`, `AccountExists`, `ResetPassword`), el resultado, la duración y, en un fallo de envío, el tipo de la excepción y el `SmtpStatusCode`. Los comandos de estos flujos sobrescriben `ToString()` para no imprimir sus datos.

### 4.5 Tailwind
- Usar el **CLI independiente** de Tailwind (ejecutable, sin Node). Integrarlo al build de .NET (target de MSBuild) para escanear los `.razor` y generar el CSS final.
- El estilo visual de referencia es **Intercom**. El `DESIGN.md` de la raíz (obtenido de VoltAgent/awesome-design-md, carpeta `design-md/intercom`) es la fuente de verdad para toda la interfaz: tokens, tipografía, componentes y patrones conversacionales.
- La **maqueta aprobada** es `docs/design/opcion-c-intercom-azul.png` ("Opción C · estilo Intercom, conversacional, azul"): acento azul, fondo gris azulado muy claro, tarjetas blancas con bordes redondeados, navegación en píldora, burbujas de conversación, respuestas rápidas con contorno azul y recorrido por pasos en un panel lateral. Donde la maqueta y `DESIGN.md` difieran (paleta y acento), **prevalece la maqueta**; `DESIGN.md` aporta estructura, espaciados y patrones.
- No copiar logotipos ni elementos de marca. El naranja `#ff5600` es el color de marca de Fin AI, por lo que no se usa como acento.
- **Vistas solo con clases CSS** (sin `style=` ni JavaScript en línea), con una única excepción documentada: el script de terceros de Cloudflare Turnstile (`https://challenges.cloudflare.com/turnstile/v0/api.js`) dentro de `Components/Shared/CaptchaWidget.razor`, en `/registro` y `/recuperar-contrasena`, solo con `Captcha:Provider=Turnstile`. No se aloja ni se pasa por un proxy (Cloudflare lo prohíbe) y no hay otro script de terceros.
- Los tokens visuales viven en un único archivo de tema de Tailwind, de modo que cualquier ajuste (acento, tipografía) sea un cambio de un solo archivo.

### 4.6 Autenticación
- Inicio de sesión básico con ASP.NET Core Identity (correo + contraseña). **Sin roles, sin administración de usuarios, sin pantallas de configuración.** Existe registro propio abierto en `/registro` (REQUISITOS RF-67). Las protecciones del registro y del inicio de sesión (cambio `registration-hardening`) son **interruptores de configuración, todos apagados por defecto**, de modo que sin configurar nada el comportamiento es el de siempre:

| Interruptor | Valor por defecto | Qué activa |
|---|---|---|
| `RateLimiting:Enabled` (+ `PermitLimit` 10, `WindowSeconds` 60) | `false` | Limita por IP los POST a `/login`, `/registro` y las páginas de recuperación (RF-78); responde 429 en español. Ignora una barra final y las mayúsculas. Detrás del proxy de Railway necesita cabeceras reenviadas (cambio Railway-prep); mientras tanto todos comparten un cubo |
| `Captcha:Provider` (`None` \| `Turnstile`) | `None` | Cloudflare Turnstile en `/registro` y `/recuperar-contrasena`, verificado en el servidor y antes de cualquier consulta de cuentas; falla cerrado (RF-79). Claves: `Turnstile:SiteKey` y `Turnstile__SecretKey` |
| `Registration:InviteCode` | vacío (apagado) | Código de invitación compartido en `/registro`: con valor, el campo es obligatorio y se comprueba tras el captcha y antes de cualquier consulta de cuentas (RF-83). Mínimo 12 caracteres tras recortar o la app no arranca (el mensaje nombra la clave, nunca el valor). Solo user-secrets o `Registration__InviteCode`, nunca `appsettings*.json` |
| `Email:Provider` (`None` \| `Smtp`) | `None` | Envío de correo por una cola en segundo plano que no retrasa la respuesta (RF-80). Con `Smtp`: `Smtp:Host`, `Smtp:From`, `Email:PublicBaseUrl` y, con usuario, `Smtp__Password`. Habilita las páginas de reenvío, recuperación y restablecimiento |
| `Email:RequireConfirmedEmail` | `false` | Una cuenta sin confirmar no inicia sesión y el registro envía un enlace de 24 horas (RF-81); exige `Email:Provider` distinto de `None` |
| `ForwardedHeaders:Enabled` | `false` | Confía en `X-Forwarded-For` y `X-Forwarded-Proto` de cualquier remitente (se vacían las redes y proxies conocidos porque las direcciones del proxy de Railway no son fijas), antes de la redirección a HTTPS y del limitador. **Activar solo detrás del proxy**: llegando directo al contenedor un cliente podría falsificar la cabecera y evadir el limitador por IP |
| `DataProtection:Provider` (`Default` \| `Database`) | `Default` | `Database` guarda las claves de Data Protection en la tabla `dp_keys` para que cookies y enlaces sobrevivan a un redespliegue (sin cifrar en la base; ver «Redespliegue y Data Protection») |

- **Reglas comunes.** Respuestas uniformes: el registro con confirmación, el reenvío y la solicitud de recuperación muestran el mismo texto y la misma redirección exista o no la cuenta. Los correos van siempre a la dirección **guardada** en la cuenta, nunca a la escrita en la petición (la intercalación por defecto de MySQL ignora los acentos), y los validadores de registro, recuperación y reenvío rechazan las direcciones con caracteres no ASCII. Los enlaces del correo se construyen solo con `Email:PublicBaseUrl`, nunca con la cabecera `Host`. Los tokens son de un solo uso: 24 horas para confirmar y 2 horas para restablecer (el cambio de contraseña rota el sello de seguridad).
- **Inicio de sesión con confirmación.** Identity responde `NotAllowed` antes de revisar el bloqueo y sin contar el fallo, por eso `IdentityAuthService` decide el orden (`SignInOutcome.Resolve`): cuenta bloqueada → `LockedOut`; contraseña correcta → `NotAllowed` («Confirma tu correo antes de iniciar sesión.»); contraseña errónea → se cuenta el fallo (`AccessFailedAsync`) y se responde como credenciales incorrectas, así el aviso solo lo ve quien conoce la contraseña. Un correo desconocido verifica la contraseña contra un hash ficticio para no responder más rápido. Los usuarios sembrados se crean confirmados.
- **Validaciones de arranque.** La aplicación no arranca, nombrando la variable y nunca su valor, si falta una clave o un ajuste de un proveedor activo (`Turnstile:SiteKey`, `Turnstile__SecretKey`, `Smtp:Host`, `Smtp:From`, `Email:PublicBaseUrl`, `Smtp__Password` con usuario), si el puerto SMTP es 465 (TLS implícito no soportado) o está fuera de rango, si un valor de proveedor es desconocido, si `RateLimiting` activo tiene números inválidos o si `Email:RequireConfirmedEmail` es `true` con `Email:Provider` en `None`.
- **Redespliegue y Data Protection.** Los tokens de los enlaces y las cookies de sesión son cargas de Data Protection. Por defecto (`DataProtection:Provider=Default`) las claves viven en el sistema de archivos del contenedor, así que cada redespliegue o reinicio **invalida los enlaces ya enviados y cierra las sesiones**; el fallo es siempre el mensaje uniforme «El enlace no es válido o venció. Pide uno nuevo.», y el reenvío y la recuperación son la salida. Con `DataProtection:Provider=Database` (cambio `railway-prep`) las claves se guardan en la tabla `dp_keys` del mismo `ApplicationDbContext`, con el nombre de aplicación fijo `RutaProducto`, y sobreviven a los redespliegues. Quedan **sin cifrar en la base** (misma confianza que los datos, red privada de Railway); el certificado de cifrado está aplazado (`docs/mejoras-posteriores.md`). Un valor desconocido impide el arranque y lista los válidos. `dp_keys` es la única tabla de plataforma: ningún módulo la usa (prueba de arquitectura).
- **Cookies `Secure`.** La cookie de sesión (`RutaProducto.Auth`) lleva `Secure` siempre fuera de Development (`CookieSecurity.PolicyFor`, RF-89), sin importar el esquema que vea el contenedor: detrás del proxy de Railway llega http plano y el valor por defecto del framework (`SameAsRequest`) la emitiría sin `Secure`. La cookie de antiforgery sigue el esquema de la petición (con `Always` el framework se niega a emitirla por http y toda página con formulario respondería 500), así que sale `Secure` cuando `ForwardedHeaders:Enabled=true` restaura https: **detrás del proxy de Railway hay que activar las cabeceras reenviadas**. En Development se mantiene `SameAsRequest`, así que `dotnet run` sobre http://localhost sigue funcionando. No hay ajuste de configuración. Para probar una imagen en modo Production sobre http plano no hay sesión (el navegador no devuelve una cookie `Secure` por http): usar https o `ASPNETCORE_ENVIRONMENT=Development`.
- Las claves secretas (`Turnstile__SecretKey`, `Smtp__Password`) van solo en user-secrets o variables de entorno, nunca en el repositorio ni en `appsettings*.json`.
- Usuarios iniciales se **siembran al arrancar desde variables de entorno**.
- Dejar puntos de extensión para crecer sin reescribir: `ICurrentUser` e `IAuthService` (futuro SSO o roles).

### 4.7 Adjuntos (`IFileStorage`)
- Interfaz en Application; implementaciones `LocalFileStorage` y `S3FileStorage` en Infrastructure; selección con `Storage:Provider` en configuración.
- **Nunca guardar archivos en MySQL**; solo metadatos y la clave de almacenamiento.
- **Solo existe el almacenamiento local** (`Storage:Provider=Local`, raíz `Storage:LocalRootPath`, por defecto `App_Data/attachments`, ignorada por Git). `S3FileStorage` aún no existe: configurar `S3` u otro valor hace fallar el arranque con un mensaje que nombra la clave, sin repetir el valor. En Railway el disco es efímero, así que los archivos locales se pierden en cada despliegue hasta que exista S3 (decisión pendiente: producción, ver §6.4).
- Adjuntos opcionales y de cualquier tipo de contenido (archivos, enlaces, notas), con estos límites: 10 MB por archivo (`Storage:MaxFileSizeMb`, entero de 1 a 50), 20 elementos y 50 MB de archivos por iniciativa.
- Lista de bloqueo (última extensión, sin distinguir mayúsculas): `.exe .dll .bat .cmd .com .msi .scr .js .vbs .ps1 .sh .jar`. No hay análisis de malware; la mitigación es esa lista y la descarga forzada.
- La descarga se sirve siempre como `application/octet-stream` con `Content-Disposition: attachment`, `nosniff` y `no-store`; solo el dueño de la iniciativa puede descargar, y cualquier otro caso responde 404 sin cuerpo.
- Solo se añaden y se eliminan elementos (no hay edición); eliminar pide confirmación y funciona en cualquier estado de la iniciativa.
- **Lectura de documentos (RF-94).** `IDocumentTextExtractor` (Application, `Abstractions/Documents`) tiene una implementación por formato (`Supports(extension)` + `ExtractAsync`); hoy `DocxMarkdownExtractor` (Infrastructure, `DocumentFormat.OpenXml` 3.5.1, MIT, fijada en `Directory.Packages.props` y referenciada solo desde Infrastructure). Añadir un formato (PDF con PdfPig, Apache-2.0) es una clase y su registro. `AddFileAttachmentCommandHandler` la ejecuta **después** de validar y guardar el archivo, solo para `.docx`, con un plazo de 15 s (`ContextLimits.ExtractionTimeout`), y es de mejor esfuerzo: cualquier fallo deja `ExtractionStatus=Unreadable` y la subida sigue. El texto (Markdown, hasta 200.000 caracteres) se guarda en `ctx_attachments.ExtractedText` (`longtext`) para sobrevivir al disco efímero; el original no cambia. Entrada no confiable: zip con a lo sumo 2.000 entradas y 100 MB descomprimidos (contados al descomprimir), sin DTD, XML de a lo sumo 200 niveles, nunca a disco. Registros: solo estado, número de caracteres y milisegundos (y el tipo de excepción), nunca nombres, texto ni mensajes.

### 4.8 Asistente (`IAssistantService`)
- Interfaz en Application; implementación Gemini en Infrastructure. Primero una **implementación falsa** para desarrollar el flujo y la persistencia.
- **Advertencia:** en la capa gratuita de Gemini, Google puede usar los datos para mejorar sus productos. Usar **solo datos de ejemplo** hasta migrar a un plan de pago antes de manejar iniciativas reales de la empresa. Dejarlo escrito en el README y en la configuración. **Decisión del usuario (2026-10-12):** sin aviso en la interfaz; solo información muy básica hasta conectar un modelo de la compañía (RN-05). **Decisión del usuario (2026-10-16):** no se contrata Gemini de pago en esta fase; se sigue con la capa gratuita y datos de ejemplo hasta que el proyecto sea avalado y se conecte un modelo de la compañía. Mientras tanto no se cargan iniciativas reales de la empresa, tampoco en el despliegue de Railway.
- Solo se envían al modelo los tipos de contenido que este sepa interpretar (notas, archivos de texto y archivos de Word ya leídos, RF-94); el resto se guarda como referencia. Tope del contexto: 10 elementos, 15.000 caracteres por elemento y 60.000 en total (RF-95).
- La clave de API va en user-secrets (`Gemini:ApiKey`) o en la variable de entorno `Gemini__ApiKey`, nunca en el repositorio, en `appsettings*.json` ni en los registros. Si falta con `Gemini` activo, la aplicación no arranca y el mensaje nombra la variable, nunca su valor.
- **Interruptor:** `Assistant:Provider` = `Fake` (por defecto en el repositorio, sin clave ni red) o `Gemini` (RF-68). Con `Gemini` se activan juntos el asistente y el generador de documentos (`IAssistantService` e `IArtifactDrafter`). Las pruebas y la CI nunca usan la clave ni la red; las comprobaciones con la clave real son manuales (`docs/gemini-evaluacion.md`).
- **El modelo es configuración** (`Gemini:Model`), no código. Por defecto `gemini-3.6-flash`, con plazos de 60 s por turno y 120 s por documento. Se eligió con la sonda de latencia de `docs/gemini-sonda.md` y la decisión del usuario del 2026-10-13: `gemini-3.8-flash` tardó 58 s y `gemini-3.5-flash-lite` 72 s en un turno pequeño, mientras que `gemini-3.6-flash` tardó 2,0 s y `gemini-3.1-flash-lite` 1,3 s. Cambiarlo es cambiar la configuración y volver a medir.
- Cada opción de `Gemini:*` tiene un valor por defecto en `appsettings.json` (modelo, plazos, concurrencia, nivel de razonamiento); solo la clave no. Para volver al asistente falso basta con `Assistant:Provider=Fake`.
- La advertencia de la capa gratuita se **mantiene como documentación** (README y esta sección); la capa gratuita queda aceptada por el usuario para el paso 5 (2026-10-13) y la interfaz sigue sin avisos (RN-05, RNF-04).
- Los registros de las llamadas a Gemini no contienen contenido: solo identificadores, tema o tipo de documento, modelo, estado, duración, `finishReason`, tokens y código de motivo (RNF-02).

### 4.9 Artefactos y exportación
- Tipos: **Brief, PRD, Arquitectura, Épicas e historias**. Arquitectura y Épicas/historias se generan como **borradores para revisión del equipo de desarrollo**, no como decisiones técnicas finales.
- **Un solo origen de verdad:** cada artefacto se guarda **como Markdown** en MySQL, con número de versión. Nunca se guarda una copia aparte en PDF.
- Exportación (DRY): `IArtifactExporter` en Application; una implementación por formato en `Infrastructure/Export/`:
  - `MarkdownArtifactExporter`: entrega el `.md` tal cual, para desarrolladores.
  - `PdfArtifactExporter`: **no existe por ahora** (decisión 2026-10-12, RF-21): el PDF es la impresión del navegador sobre la misma vista del documento.
- Librería de PDF: **ninguna por ahora**. Si se vuelve a necesitar un PDF generado en el servidor, verificar licencia vigente y compatibilidad con el servidor antes de decidir; preguntar al usuario.
- Cadena de documentos (nivel Grande): la Arquitectura exige el PRD aprobado y las Épicas exigen la Arquitectura aprobada; ambas se redactan solo desde el texto aprobado del documento anterior (sin respuestas del asistente) y el código numera `AD-n`, `Epic N` y `Story N.M`, filtra toda cita contra el PRD y calcula el mapa de capacidades y el de cobertura (RF-73, RF-74). Si el documento anterior cambia después, la página del dependiente solo muestra un aviso ámbar (RF-54); invalidar en cascada queda en `docs/mejoras-posteriores.md`.
- Fidelidad del borrador (RF-93): una suposición con una cifra que no está en los textos de la persona se sustituye por una pregunta abierta fija (solo se registra cuántas, sin contenido), y lo que la persona dice que puede esperar va solo a «No objetivos» como pospuesto; fuente BMAD 6.12.1, `bmad-prd/references/headless.md` («Do not invent user detail, success metrics, or scope decisions to fill gaps — record them») y `prd-template.md` §6.2 («Mark items deferred to v2/v3 explicitly»).
- Aprobación manual del usuario antes de marcar un artefacto como final.

### 4.10 Idioma
- Interfaz, mensajes del asistente y documentos generados en **español**. Código (nombres de tipos, métodos) en inglés o español de forma consistente; preferir inglés para identificadores técnicos.

### 4.11 Nombre del producto y marcas
- El producto se llama **Ruta de Producto** (decisión del usuario, 2026-10-12). **Ningún nombre propio lleva «BMAD»** (producto, empresa, dominio, cuentas, carpetas o paquetes): BMad y BMAD-METHOD son marcas de BMad Code, LLC, y su política (`TRADEMARK.md`) permite describir compatibilidad ("compatible con BMad Method v6", "usa BMad") pero no usar la marca como nombre propio ni insinuar aval.
- Se puede mencionar el método para describirlo (documentos de conformidad, nombres de sus skills como `bmad-prd` o `bmad-build` en la nota de uso del `.md`), sin logos ni insinuar que BMAD avala el producto.
- Hecho: los nombres internos (espacios de nombres, proyectos, solución, base de datos `rutaproducto`, servicio y contenedor de Docker, cookie) usan `RutaProducto` en lugar del nombre anterior.

## 5. Cómo trabajar

1. Construir **en el orden de `PROPUESTA-MVP.md` §7**, un paso a la vez. Cada paso entrega una funcionalidad completa, de interfaz a base de datos.
2. Al terminar cada paso: compilar, correr pruebas, y resumir qué se hizo y qué queda.
3. Si algo no está decidido (ver §6), **preguntar** en lugar de asumir.
4. Al terminar cada implementación con interfaz, **recorrer el flujo real en Chrome** con Chrome DevTools MCP para comprobar que tiene sentido de principio a fin, además de compilar y correr las pruebas (no hay bUnit, así que el navegador es la única verificación de las pantallas). Levantar MySQL con `docker compose` y credenciales temporales, aplicar migraciones, ejecutar la app con el usuario semilla, revisar `Logs/` y desmontar todo al terminar.
5. No inventar estructura de BMAD: la forma exacta de los artefactos debe validarse contra la documentación oficial (https://docs.bmad-method.org/) y probando BMAD en un repositorio de prueba.

## 6. Decisiones pendientes

1. Versión de .NET (sugerida: última LTS, .NET 10).
2. ~~Librería para generar PDF~~ **Resuelta por ahora:** sin librería; el PDF es «Guardar como PDF» desde la ventana de impresión del navegador (RF-21).
3. Estructura exacta y nombres de archivo/carpeta que BMAD espera para sus artefactos: **validada en una instalación real de BMAD 6.12.1** (ver `docs/bmad-referencia.md`). Identificadores **decididos** (cambio `bmad-alignment`): la plataforma y el PRD conservan el guion (`FR-1`, `NFR-1`, `UJ-1`, `SM-1`; `NFR-n` es una extensión de la plataforma), como la plantilla del PRD de BMAD; el inventario de `epics.md` (paso 6) **conserva el guion y el mismo número** (`FR-3` sigue siendo `FR-3`, no hay conversor), porque `bmad-create-epics-and-stories` leyó los identificadores con guion sin renumerar en la ejecución real del 2026-10-14; la exportación copia el PRD tal cual (REQUISITOS RF-36, RF-44). Las comprobaciones de la Arquitectura y las Épicas con BMAD real están en `docs/bmad-referencia.md`. No existen `bmad-ticket` ni `tickets.toml` en esta versión. Revisar el documento cuando salga una versión nueva.
4. Almacenamiento de adjuntos en producción: hoy existe solo el almacenamiento local (paso 7, cambio `context-attachments`; configurar S3 hace fallar el arranque). En Railway el disco del contenedor es efímero, por lo que los archivos se pierden en cada despliegue; la opción prevista es S3 (Railway Buckets, compatible con S3), diferida a un cambio propio que debe hacerse antes de manejar iniciativas reales.
5. Qué tipos de contenido del adjunto puede interpretar el modelo elegido.
6. ~~Cómo revisa desarrollo Arquitectura e historias~~ **Resuelta:** fuera de la plataforma. Los desarrolladores no tienen rol; quien registra la iniciativa entrega los archivos exportados, con una nota de uso y una sección "Preguntas para desarrollo" (REQUISITOS RF-39).
7. Valores exactos del azul de acento y de los neutros: la maqueta aprobada define el estilo, pero no los códigos hexadecimales. Fijarlos al construir el archivo de tema.
8. Backups de MySQL en Railway: el despliegue y la base de datos van en **Railway** (decidido; reemplazan a IIS local y a Azure) y la versión es **MySQL 8.4**, igual que en local (decidido). La plantilla MySQL de Railway despliega `mysql:9` por defecto (según su página), así que al crear el servicio hay que fijar la imagen en `mysql:8.4` y definir `Database__MySqlServerVersion=8.4.0`. Pendiente: la plantilla arranca con `--disable-log-bin` y la guía de Railway menciona recuperación a un punto en el tiempo; verificar en Railway cómo funcionan realmente las copias de seguridad antes de depender de ellas.
9. Preparación para Railway (**hecha** en el cambio `railway-prep`, sin cambiar el comportamiento local; todo apagado por defecto): Dockerfile multi-etapa para .NET 10 (hecho, usuario no root, `/app/App_Data` y `/app/Logs` escribibles); puerto desde la variable `PORT` (hecho, `ASPNETCORE_URLS` manda si existe); cabeceras reenviadas con `ForwardedHeaders:Enabled` (hecho; activar solo detrás del proxy, porque un cliente que llegue directo podría falsificar la cabecera y evadir el limitador por IP); claves de Data Protection persistentes con `DataProtection:Provider=Database` (hecho); registros por consola además del archivo (hecho, §4.4); migraciones como comando previo al despliegue con `dotnet RutaProducto.Web.dll --migrate` (hecho; no arranca Kestrel ni siembra usuarios; el arranque normal no migra); comprobación de salud `GET /health` (hecha, solo vivacidad, sin base de datos). **Pendiente:** recorrido real en Railway, almacenamiento S3 (§6.4) y copias de seguridad (§6.8).
10. Tipografía: Saans es propietaria. La maqueta usa una sans geométrica; elegir una fuente libre de reemplazo (por ejemplo Inter) hasta contar con licencia.

## 7. Obtener `DESIGN.md` (Intercom)

Repositorio: https://github.com/VoltAgent/awesome-design-md
- Comando: `npx getdesign@latest add intercom` (requiere Node solo para esta descarga), o
- copiar manualmente desde https://getdesign.md/intercom/design-md.

Dejar el archivo `DESIGN.md` en la raíz, junto a este `AGENTS.md`, y seguirlo para toda la interfaz.

## 8. Agentes y skills del proyecto

Viven en `.claude/` y se versionan con el código. Las skills son contratos de ejecución para el agente (en inglés, como el código); cada agente lee las skills que le corresponden por su ruta. Antes de una tarea, cargar la skill que coincida; para trabajo delegado, usar el agente.

### Skills (`.claude/skills/<nombre>/SKILL.md`)

| Skill | Cuándo |
|---|---|
| `rp-build-gate` | Antes de cualquier commit, PR o «terminado»: restaurar, compilar y probar como la CI |
| `rp-local-walkthrough` | Recorrido en Chrome con MySQL descartable (§5.4); incluye receptor SMTP, claves de prueba y trampas conocidas |
| `rp-ship-pr` | Commit convencional sin atribución, PR en inglés, CI, fusión squash |
| `rp-config-switch` | Añadir un ajuste o proveedor apagado por defecto, con validación de arranque |
| `rp-docs-sync` | Mantener README, REQUISITOS, AGENTS y `docs/mejoras-posteriores.md` al día |
| `rp-bmad-conformance` | Validar el formato exportado contra una instalación real de BMAD (§5.5, §6.3) |
| `rp-gemini-live-check` | Comprobación moderada con Gemini real, sin datos reales ni fugas en registros |
| `rp-ui-conventions` | Vistas Blazor: solo clases CSS, JS permitido, textos en español, trampas conocidas |
| `rp-security-privacy` | Autenticación, correo, registros, secretos, adjuntos y descargas |
| `rp-lean-tests` | Pruebas unitarias austeras (§4.1) |
| `rp-artifact-pipeline` | Cambios en la cadena de documentos: borrador, numeración, exportación |
| `rp-railway-deploy` | Desplegar y operar en Railway con el CLI local del proyecto: variables, dominio, verificación, apagar y encender para no gastar crédito |

### Agentes (`.claude/agents/<nombre>.md`)

| Agente | Rol |
|---|---|
| `rp-feature-writer` | Único escritor de un cambio; aplica las skills, pasa la compuerta y hace commit en una rama sin subirla |
| `rp-code-reviewer` | Revisión de solo lectura con cuatro lentes (riesgo, resiliencia, legibilidad, fiabilidad); úsese cuando la revisión nativa no pueda inspeccionar el candidato |
| `rp-walkthrough-verifier` | Levanta el entorno, recorre el flujo en Chrome, informa y desmonta todo; no edita código |
| `rp-bmad-auditor` | Ejecuta las comprobaciones de BMAD en una carpeta desechable e informa con evidencia |
| `rp-artifact-reviewer` | Revisa la fidelidad y la forma de los documentos generados |

Flujo recomendado para un cambio: `rp-feature-writer` → `rp-code-reviewer` → `rp-walkthrough-verifier` (si hay interfaz) → `rp-ship-pr`. Para formatos de documentos, añadir `rp-bmad-auditor` y `rp-artifact-reviewer`. Al añadir o cambiar una skill o un agente, actualizar esta tabla.
