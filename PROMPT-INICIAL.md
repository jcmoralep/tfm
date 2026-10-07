# Prompt inicial para Claude Code

## Preparación (una sola vez)

1. Crear la carpeta del proyecto, por ejemplo `BmadPlatform/`, y abrir Claude Code en ella.
2. Copiar en la raíz: `AGENTS.md`, `REQUISITOS.md` y `PROPUESTA-MVP.md`.
3. Descargar el diseño: `npx getdesign@latest add intercom` (deja `DESIGN.md` en la raíz).
4. Tener instalados: SDK de .NET (última LTS), MySQL en local (o Docker) y Git.

## Prompt (pegar tal cual)

```
Lee en este orden: AGENTS.md, REQUISITOS.md, PROPUESTA-MVP.md y DESIGN.md.

Vamos a construir la Plataforma BMAD para producto. Trabaja SOLO el paso 1 del orden de construcción (PROPUESTA-MVP.md §7): la base del proyecto.

Alcance del paso 1:
- Solución .NET con las cuatro capas (Domain, Application, Infrastructure, Web) según AGENTS.md §3.
- Blazor Web App con Interactive Server, Tailwind con el CLI independiente integrado al build (sin Node) y estilo según DESIGN.md.
- EF Core con MySQL (Pomelo), IDbContextFactory y una migración inicial con las tablas de Identity.
- MediatR con LoggingBehavior y validación (FluentValidation), en el orden Logging → Validación → Handler.
- Serilog según la configuración de AGENTS.md §4.4 (archivos .log, retención de 7 días, Logs/ en .gitignore).
- Inicio de sesión con ASP.NET Core Identity, sin roles, con usuarios sembrados desde variables de entorno, y las interfaces ICurrentUser e IAuthService.
- Una pantalla de inicio protegida que confirme el acceso, con el estilo visual definido.
- README breve con cómo configurar variables de entorno, base de datos y ejecutar.

Antes de empezar:
1. Confirma la versión de .NET a usar (sugerida: la última LTS) y pregúntame si hay algo bloqueante.
2. Muéstrame un plan corto de lo que vas a crear.

Al terminar: compila, corre las pruebas y resume qué quedó hecho y qué sigue. No avances al paso 2 sin mi confirmación. No inventes estructura de BMAD ni elijas librería de PDF: son decisiones pendientes.
```

## Siguientes prompts (uno por paso)

Usar el mismo patrón: "Trabaja SOLO el paso N de PROPUESTA-MVP.md §7; confirma el plan antes de empezar; al terminar compila, prueba y resume."

- Paso 2: Iniciativas (crear, listar, detalle, selector de profundidad).
- Paso 3: Asistente guiado con `IAssistantService` falso y conversación persistida.
- Paso 4: Artefactos Brief y PRD con edición, versión y aprobación.
- Paso 5: Gemini real (clave en variable de entorno; solo datos de ejemplo).
- Paso 6: Arquitectura y Épicas/historias como borradores.
- Paso 7: Contexto adjunto con almacenamiento local (`IFileStorage`).
- Paso 8: Exportación Markdown y PDF (antes, decidir la librería de PDF).
- Paso 9: Pulido y prueba con datos de ejemplo.

## Recomendación previa a los pasos 3 y 6

Probar BMAD en un repositorio de prueba para validar los nombres de secciones del Brief y la estructura/carpetas de artefactos que BMAD espera, y ajustar `PROPUESTA-MVP.md` y `AGENTS.md` con lo que se encuentre.
