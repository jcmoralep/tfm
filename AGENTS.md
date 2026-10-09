# AGENTS.md — Plataforma BMAD para producto

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
| JavaScript | Vanilla, solo con módulos ES e `IJSRuntime` para lo que Blazor no cubre (portapapeles, descargas, arrastrar y soltar) |
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
BmadPlatform/
├── src/
│   ├── BmadPlatform.Domain/          (entidades, reglas; sin dependencias externas)
│   ├── BmadPlatform.Application/     (comandos, consultas, handlers, behaviors, interfaces)
│   ├── BmadPlatform.Infrastructure/  (EF Core/MySQL, almacenamiento, Gemini, exportadores)
│   └── BmadPlatform.Web/             (Blazor, Tailwind, wwwroot/js)
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
- Configuración desde `appsettings.json`. Consola en desarrollo; archivo en servidor.
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

### 4.5 Tailwind
- Usar el **CLI independiente** de Tailwind (ejecutable, sin Node). Integrarlo al build de .NET (target de MSBuild) para escanear los `.razor` y generar el CSS final.
- El estilo visual de referencia es **Intercom**. El `DESIGN.md` de la raíz (obtenido de VoltAgent/awesome-design-md, carpeta `design-md/intercom`) es la fuente de verdad para toda la interfaz: tokens, tipografía, componentes y patrones conversacionales.
- La **maqueta aprobada** es `docs/design/opcion-c-intercom-azul.png` ("Opción C · estilo Intercom, conversacional, azul"): acento azul, fondo gris azulado muy claro, tarjetas blancas con bordes redondeados, navegación en píldora, burbujas de conversación, respuestas rápidas con contorno azul y recorrido por pasos en un panel lateral. Donde la maqueta y `DESIGN.md` difieran (paleta y acento), **prevalece la maqueta**; `DESIGN.md` aporta estructura, espaciados y patrones.
- No copiar logotipos ni elementos de marca. El naranja `#ff5600` es el color de marca de Fin AI, por lo que no se usa como acento.
- Los tokens visuales viven en un único archivo de tema de Tailwind, de modo que cualquier ajuste (acento, tipografía) sea un cambio de un solo archivo.

### 4.6 Autenticación
- Inicio de sesión básico con ASP.NET Core Identity (correo + contraseña). **Sin roles, sin administración de usuarios, sin pantallas de configuración.**
- Usuarios iniciales se **siembran al arrancar desde variables de entorno**.
- Dejar puntos de extensión para crecer sin reescribir: `ICurrentUser` e `IAuthService` (futuro SSO o roles).

### 4.7 Adjuntos (`IFileStorage`)
- Interfaz en Application; implementaciones `LocalFileStorage` y `S3FileStorage` en Infrastructure; selección con `Storage:Provider` en configuración.
- **Nunca guardar archivos en MySQL**; solo metadatos y la clave de almacenamiento.
- Implementar primero el almacenamiento local. (Decisión pendiente: cuál se usará en producción.)
- Adjuntos opcionales y de cualquier tipo de contenido (archivos, enlaces, notas).

### 4.8 Asistente (`IAssistantService`)
- Interfaz en Application; implementación Gemini en Infrastructure. Primero una **implementación falsa** para desarrollar el flujo y la persistencia.
- **Advertencia:** en la capa gratuita de Gemini, Google puede usar los datos para mejorar sus productos. Usar **solo datos de ejemplo** hasta migrar a un plan de pago antes de manejar iniciativas reales de la empresa. Dejarlo escrito en el README y en la configuración.
- Solo se envían al modelo los tipos de contenido que este sepa interpretar; el resto se guarda como referencia.
- La clave de API va en variable de entorno / user-secrets, nunca en el repositorio.

### 4.9 Artefactos y exportación
- Tipos: **Brief, PRD, Arquitectura, Épicas e historias**. Arquitectura y Épicas/historias se generan como **borradores para revisión del equipo de desarrollo**, no como decisiones técnicas finales.
- **Un solo origen de verdad:** cada artefacto se guarda **como Markdown** en MySQL, con número de versión. Nunca se guarda una copia aparte en PDF.
- Exportación (DRY): `IArtifactExporter` en Application; una implementación por formato en `Infrastructure/Export/`:
  - `MarkdownArtifactExporter`: entrega el `.md` tal cual, para desarrolladores.
  - `PdfArtifactExporter`: genera el PDF para el usuario a partir del mismo Markdown.
- Librería de PDF: **no elegida**. Verificar licencia vigente y compatibilidad con el servidor antes de decidir; preguntar al usuario.
- Aprobación manual del usuario antes de marcar un artefacto como final.

### 4.10 Idioma
- Interfaz, mensajes del asistente y documentos generados en **español**. Código (nombres de tipos, métodos) en inglés o español de forma consistente; preferir inglés para identificadores técnicos.

## 5. Cómo trabajar

1. Construir **en el orden de `PROPUESTA-MVP.md` §7**, un paso a la vez. Cada paso entrega una funcionalidad completa, de interfaz a base de datos.
2. Al terminar cada paso: compilar, correr pruebas, y resumir qué se hizo y qué queda.
3. Si algo no está decidido (ver §6), **preguntar** en lugar de asumir.
4. Al terminar cada implementación con interfaz, **recorrer el flujo real en Chrome** con Chrome DevTools MCP para comprobar que tiene sentido de principio a fin, además de compilar y correr las pruebas (no hay bUnit, así que el navegador es la única verificación de las pantallas). Levantar MySQL con `docker compose` y credenciales temporales, aplicar migraciones, ejecutar la app con el usuario semilla, revisar `Logs/` y desmontar todo al terminar.
5. No inventar estructura de BMAD: la forma exacta de los artefactos debe validarse contra la documentación oficial (https://docs.bmad-method.org/) y probando BMAD en un repositorio de prueba.

## 6. Decisiones pendientes

1. Versión de .NET (sugerida: última LTS, .NET 10).
2. Librería para generar PDF.
3. Estructura exacta y nombres de archivo/carpeta que BMAD espera para sus artefactos: **validada en una instalación real de BMAD 6.12.1** (ver `docs/bmad-referencia.md`). Pendiente: el formato de los requisitos no funcionales (el PRD no define identificador y el paso de épicas espera `NFR1:`), y no existen `bmad-ticket` ni `tickets.toml` en esta versión. Revisar el documento cuando salga una versión nueva.
4. Almacenamiento de adjuntos en producción: en Railway el disco del contenedor es efímero, por lo que apunta a S3 (Railway Buckets, compatible con S3). Confirmar al llegar al paso 7.
5. Qué tipos de contenido del adjunto puede interpretar el modelo elegido.
6. ~~Cómo revisa desarrollo Arquitectura e historias~~ **Resuelta:** fuera de la plataforma. Los desarrolladores no tienen rol; quien registra la iniciativa entrega los archivos exportados, con una nota de uso y una sección "Preguntas para desarrollo" (REQUISITOS RF-39).
7. Valores exactos del azul de acento y de los neutros: la maqueta aprobada define el estilo, pero no los códigos hexadecimales. Fijarlos al construir el archivo de tema.
8. Backups de MySQL en Railway: el despliegue y la base de datos van en **Railway** (decidido; reemplazan a IIS local y a Azure) y la versión es **MySQL 8.4**, igual que en local (decidido). La plantilla MySQL de Railway despliega `mysql:9` por defecto (según su página), así que al crear el servicio hay que fijar la imagen en `mysql:8.4` y definir `Database__MySqlServerVersion=8.4.0`. Pendiente: la plantilla arranca con `--disable-log-bin` y la guía de Railway menciona recuperación a un punto en el tiempo; verificar en Railway cómo funcionan realmente las copias de seguridad antes de depender de ellas.
9. Preparación para Railway (por hacer en un cambio propio): Dockerfile multi-etapa para .NET 10; puerto desde la variable `PORT`; cabeceras reenviadas porque el HTTPS termina en el proxy; claves de Data Protection persistentes (el sistema de archivos del contenedor es efímero); registros por consola además del archivo; migraciones como comando previo al despliegue (`pre-deploy command`, que corre en un contenedor aparte y sin volúmenes).
10. Tipografía: Saans es propietaria. La maqueta usa una sans geométrica; elegir una fuente libre de reemplazo (por ejemplo Inter) hasta contar con licencia.

## 7. Obtener `DESIGN.md` (Intercom)

Repositorio: https://github.com/VoltAgent/awesome-design-md
- Comando: `npx getdesign@latest add intercom` (requiere Node solo para esta descarga), o
- copiar manualmente desde https://getdesign.md/intercom/design-md.

Dejar el archivo `DESIGN.md` en la raíz, junto a este `AGENTS.md`, y seguirlo para toda la interfaz.
