# REQUISITOS — Plataforma BMAD para producto

Cada requisito es verificable. Etiquetas: **[CONFIRMADO]** decidido por el equipo; **[SUPUESTO]** propuesto, pendiente de validar; **[VALIDADO EN BMAD 6.12.1]** contrastado con una instalación real de BMAD (ver `docs/bmad-referencia.md`).

## 1. Visión y usuarios

- Plataforma interna para que personas de producto no técnicas (product owners, analistas de negocio) trabajen sus iniciativas con el método BMAD mediante un asistente guiado y entreguen artefactos listos para desarrollo. [CONFIRMADO]
- Usuarios: equipo interno de producto, pocos usuarios conocidos. Los desarrolladores consumen los artefactos exportados fuera de la plataforma. [CONFIRMADO]
- Sin roles diferenciados en el MVP: todos los usuarios autenticados tienen los mismos permisos. [CONFIRMADO]

## 2. Requerimientos funcionales

### Acceso
- **RF-01** El sistema permite iniciar sesión con correo y contraseña (ASP.NET Core Identity). [CONFIRMADO]
- **RF-02** Los usuarios iniciales se crean al arrancar desde variables de entorno. [CONFIRMADO]
- **RF-03** No existen pantallas de administración de usuarios ni de configuración. [CONFIRMADO]
- **RF-04** Existen `ICurrentUser` e `IAuthService` como puntos de extensión para SSO o roles futuros. [CONFIRMADO]

### Iniciativas
- **RF-05** El usuario puede crear una iniciativa con nombre y descripción corta. [CONFIRMADO]
- **RF-06** Al crearla, el usuario elige si la profundidad la define él o la sugiere el asistente. [CONFIRMADO]
- **RF-07** Una iniciativa pasa a *Lista para construir* cuando sus artefactos están aprobados. [SUPUESTO]
- **RF-08** El usuario puede listar, ver y editar iniciativas; los estados son *Borrador*, *Aclarando*, *Planificando* y *Lista para construir*. [CONFIRMADO]
- **RF-09** El detalle de la iniciativa muestra Conversación, Artefactos y Contexto. [CONFIRMADO]
- **RF-26** Cada usuario ve, edita y elimina únicamente sus propias iniciativas. Abrir la de otro usuario responde como "no encontrada", sin revelar que existe. La iniciativa guarda el identificador de quien la creó. [CONFIRMADO]
- **RF-27** La profundidad tiene tres niveles y el selector indica con claridad qué se obtiene en cada uno: *Pequeña* (una especificación corta), *Estándar* (Brief y PRD) y *Grande* (PRD, Arquitectura y Épicas e historias). Contraste con BMAD: Pequeña es `SPEC.md` (aproximada, ver RF-11); Estándar usa un Brief que en BMAD es opcional; Grande coincide con la secuencia PRD, arquitectura y épicas, y deja fuera el diseño de UX, que es opcional. [CONFIRMADO]
- **RF-28** En modo automático la profundidad queda vacía ("Pendiente de sugerencia") hasta que el asistente la sugiera. [CONFIRMADO]
- **RF-29** El modo y el nivel de profundidad se pueden cambiar solo mientras la iniciativa está en *Borrador* o *Aclarando*; desde *Planificando* quedan fijos. [CONFIRMADO]
- **RF-30** La creación es un asistente paso a paso con borrador: cada paso guarda lo que el usuario ya eligió, y al retomar la iniciativa vuelve al mismo paso con sus selecciones. Para guardar un borrador basta el nombre. Al terminar el último paso la iniciativa pasa a *Aclarando*. [CONFIRMADO]
- **RF-31** La lista permite buscar por nombre y filtrar por estado y por nivel de profundidad, y se ordena por última modificación, la más reciente primero. Sin paginación en el MVP. [CONFIRMADO]
- **RF-32** Eliminar una iniciativa es un borrado lógico: el registro permanece en la base de datos marcado como eliminado y deja de mostrarse. No hay papelera ni pantalla para ver lo eliminado. [CONFIRMADO]
- **RF-33** El nombre es obligatorio (hasta 120 caracteres) y puede repetirse; la descripción es opcional (hasta 1000 caracteres). [CONFIRMADO]
- **RF-34** El estado de la iniciativa no se cambia a mano: avanza con los pasos de creación, el asistente y la aprobación de artefactos. [CONFIRMADO]
- **RF-35** El guion del asistente es propio de la plataforma: BMAD no define una lista fija de preguntas, sino reglas de conducta. El asistente hace **una pregunta por vez**, en orden de dependencia, sugiere su mejor respuesta cuando ayuda, no elogia y pide más cuando una respuesta es débil. [VALIDADO EN BMAD 6.12.1]
- **RF-36** Los artefactos generados siguen las convenciones de BMAD 6.12.1: carpeta `<tipo>-<proyecto>-<fecha>`, frontmatter con título, estado, creación y actualización, requisitos funcionales numerados globalmente (`FR-1`), recorridos `UJ-N`, y suposiciones en línea como `[ASSUMPTION: ...]` con un índice al final del PRD. Los requisitos no funcionales no tienen formato de identificador en BMAD; se define uno propio y se reconcilia con el que espera el paso de épicas (`NFR1:`). [VALIDADO EN BMAD 6.12.1]
- **RF-37** La versión de BMAD de la que se tomó el formato queda registrada en `docs/bmad-referencia.md` y se revisa cuando salga una versión nueva. [CONFIRMADO]

### Asistente guiado
- **RF-10** El asistente conversa con el usuario y guía el recorrido por las fases Aclarar y Planificar, mostrándolo de forma visible. [CONFIRMADO]
- **RF-11** Un cambio pequeño genera solo una especificación corta, sin planificación completa. La especificación es la de cinco partes de `bmad-spec` (Por qué, Capacidades con intención y condición de éxito, Restricciones, No objetivos, Señal de éxito). Nota: en BMAD un cambio pequeño también puede construirse directamente con un plan escrito, sin especificación formal; la plataforma ofrece la especificación porque su fin es entregar a desarrollo algo verificable. [VALIDADO EN BMAD 6.12.1, mapeo aproximado]
- **RF-12** El asistente ofrece modo rápido (agrupa los vacíos en una o dos preguntas y redacta con suposiciones marcadas para que la persona las corrija) y modo guiado (sección por sección), tras un volcado libre de la idea y una calibración de lo que está en juego (aficionado, interno o lanzamiento). BMAD los llama Fast y Coaching. [VALIDADO EN BMAD 6.12.1]
- **RF-13** El historial de la conversación se guarda por iniciativa. [CONFIRMADO]
- **RF-14** El asistente usa el contexto adjunto cuando el modelo pueda interpretarlo. [CONFIRMADO]
- **RF-15** El proveedor de IA es Gemini (capa gratuita) detrás de `IAssistantService`, reemplazable. [CONFIRMADO]

### Artefactos
- **RF-16** La plataforma genera Brief, PRD, Arquitectura y Épicas e historias. Los dos últimos son borradores para revisión de desarrollo. Con el formato de BMAD: Brief (`brief.md`), PRD (`prd.md`), Arquitectura (`ARCHITECTURE-SPINE.md`) y Épicas e historias (un solo `epics.md`), con las secciones de `docs/bmad-referencia.md`. [CONFIRMADO; formato VALIDADO EN BMAD 6.12.1]
- **RF-17** El usuario puede editar un artefacto y pedir al asistente que lo regenere. [CONFIRMADO]
- **RF-18** Cada artefacto tiene número de versión; el historial completo y la comparación entre versiones quedan fuera del MVP. [SUPUESTO]
- **RF-19** El usuario aprueba manualmente cada artefacto antes de marcarlo como final. [CONFIRMADO]
- **RF-20** El contenido se guarda como Markdown (fuente única de verdad). [CONFIRMADO]
- **RF-21** Exportación a `.md` (para developers) y `.pdf` (para el usuario), con descarga; el `.md` también se puede copiar. [CONFIRMADO]

### Contexto adjunto
- **RF-22** El adjunto es opcional y admite cualquier tipo de contenido (archivos, enlaces, notas); los tipos que proponga el usuario son prioritarios. [CONFIRMADO]
- **RF-23** Los archivos se guardan mediante `IFileStorage` (local o S3, por configuración), nunca en MySQL. [CONFIRMADO]
- **RF-24** Solo los tipos que el modelo interpreta se envían al asistente; el resto se conserva como referencia. [SUPUESTO]
- **RF-25** Videos y prototipos se almacenan como evidencia; su análisis con IA queda para una segunda versión. [SUPUESTO]

## 3. Reglas de negocio

- **RN-01** Ningún artefacto es final sin aprobación manual del usuario.
- **RN-02** Arquitectura y Épicas/historias son borradores sujetos a revisión del equipo de desarrollo.
- **RN-03** El Markdown es la única fuente de verdad; el PDF se genera bajo demanda.
- **RN-04** El adjunto de contexto nunca es obligatorio.
- **RN-05** Mientras se use la capa gratuita de Gemini, solo se usan datos de ejemplo o no sensibles.

## 4. Requerimientos no funcionales

- **RNF-01 Mantenibilidad:** Clean Architecture, CQRS con MediatR, SOLID y DRY; módulos independientes. [CONFIRMADO]
- **RNF-02 Observabilidad:** Serilog en archivos `.log`, rotación diaria, retención máxima de 7 días, sin contenido completo de comandos. [CONFIRMADO]
- **RNF-03 Seguridad:** contraseñas gestionadas por Identity; secretos (clave de Gemini, cadena de conexión, usuarios semilla) fuera del repositorio. [CONFIRMADO]
- **RNF-04 Privacidad:** advertencia visible de que la capa gratuita de Gemini puede usar los datos; migrar a plan de pago antes de usar iniciativas reales. [CONFIRMADO]
- **RNF-05 Persistencia:** MySQL con EF Core (Pomelo) e `IDbContextFactory` en Blazor Server. [CONFIRMADO]
- **RNF-06 Portabilidad:** almacenamiento intercambiable local/S3 sin cambiar código de negocio. [CONFIRMADO]
- **RNF-07 Extensibilidad:** nuevos formatos de exportación como una clase nueva (`IArtifactExporter`). [CONFIRMADO]
- **RNF-08 Idioma e interfaz:** interfaz y contenido en español; diseño según `DESIGN.md` (Intercom), usable por personas no técnicas. [CONFIRMADO]
- **RNF-09 Escala:** equipo pequeño de usuarios; sin requisitos de alta disponibilidad en el MVP. [SUPUESTO]

## 5. Restricciones técnicas

- C# / ASP.NET Core, Blazor Web App (Interactive Server), Tailwind CLI independiente (sin Node), JavaScript vanilla solo vía `IJSRuntime`.
- MySQL; no guardar archivos en la base.
- La construcción se hará con Gentle AI / Claude Code en el equipo del usuario.

## 6. Criterios de aceptación del MVP

1. Un usuario inicia sesión con una cuenta sembrada.
2. Crea una iniciativa y elige cómo se define la profundidad.
3. Conversa con el asistente y obtiene un Brief y un PRD.
4. Obtiene Arquitectura y Épicas/historias como borradores.
5. Edita, regenera y aprueba artefactos; la versión se incrementa.
6. Adjunta contexto opcional (local) y el asistente lo usa cuando puede.
7. Exporta `.md` y `.pdf` de un artefacto.
8. Los logs se escriben en `Logs/` y no se conservan más de 7 días.

## 7. Fuera del MVP

Fase "Aprender y ajustar"; roles, administración y SSO; comentarios y notificaciones; integración directa con herramientas de desarrollo; comparación de versiones; otros formatos de exportación; análisis de video con IA; catálogo de agentes BMAD.

## 8. Puntos por definir

Ver `AGENTS.md` §6 y `PROPUESTA-MVP.md` §9.
