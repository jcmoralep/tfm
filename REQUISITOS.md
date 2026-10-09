# REQUISITOS — Plataforma BMAD para producto

Cada requisito es verificable. Etiquetas: **[CONFIRMADO]** decidido por el equipo; **[SUPUESTO]** propuesto, pendiente de validar; **[VALIDADO EN BMAD 6.12.1]** contrastado con una instalación real de BMAD (ver `docs/bmad-referencia.md`).

## 1. Visión y usuarios

- Plataforma interna para que personas de producto no técnicas (product owners, analistas de negocio) trabajen sus iniciativas con el método BMAD mediante un asistente guiado y entreguen artefactos listos para desarrollo. [CONFIRMADO]
- Usuarios: equipo interno de producto, pocos usuarios conocidos, con perfiles que pueden ser junior (ver RF-41). Los desarrolladores consumen los artefactos exportados fuera de la plataforma. [CONFIRMADO]
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
- **RF-36** Los artefactos generados siguen las convenciones de BMAD 6.12.1: carpeta `<tipo>-<proyecto>-<fecha>`, frontmatter con título, estado, creación y actualización, requisitos funcionales numerados globalmente (`FR-1`), recorridos `UJ-N`, y suposiciones en línea como `[ASSUMPTION: ...]` con un índice al final del PRD. Los requisitos no funcionales no tienen formato de identificador en BMAD; la plataforma usa el suyo (RF-44). [VALIDADO EN BMAD 6.12.1]
- **RF-37** La versión de BMAD de la que se tomó el formato queda registrada en `docs/bmad-referencia.md` y se revisa cuando salga una versión nueva. [CONFIRMADO]
- **RF-38** En el nivel Estándar el Brief se **genera automáticamente** a partir del volcado inicial de la idea, sin una ronda de preguntas propia: el asistente lo muestra ("Esto es lo que entendí") y la persona lo corrige o lo aprueba (RF-19). Las preguntas del PRD parten de lo aprobado en el Brief y no repiten lo ya respondido. Las suposiciones que el asistente añada al Brief se marcan como tales. Motivo: en las simulaciones con BMAD el Brief y el PRD compartían entre el 60 y el 70% de su contenido. [CONFIRMADO]
- **RF-39** Los desarrolladores **no tienen rol ni acceso** en la plataforma. Quien registra la iniciativa es quien entrega los artefactos a desarrollo, por el medio que use (descarga, correo, chat o repositorio). La exportación debe poder entregarse sola: los archivos `.md` llevan al inicio una nota de uso ("Para construir: abra su repositorio y pase este archivo a `bmad-build`" o la habilidad que corresponda) y, cuando se generan borradores de Arquitectura y Épicas, una sección **Preguntas para desarrollo** con lo que la persona de producto no pudo responder. No hay revisión dentro de la plataforma. [CONFIRMADO]
- **RF-40** En el nivel Pequeña se entrega **solo la especificación** (`SPEC.md`); no se genera el plan técnico de `bmad-build`, porque requiere conocer el código del producto, que la plataforma no tiene. Los casos límite que se puedan deducir se incluyen como criterios de éxito dentro de la especificación. [CONFIRMADO]
- **RF-41** El asistente **guía**; no examina. Los usuarios incluirán perfiles junior, así que debe: usar lenguaje de negocio sin jerga (sin "spine", "épica", "invariantes" ni "slug" sin explicarlos), dar un **ejemplo** o una opción sugerida en cada pregunta, ofrecer respuestas rápidas concretas, aceptar "no sé" sin presionar (queda como pregunta pendiente con un responsable propuesto, RF-35), explicar brevemente por qué pregunta y cuánto falta, y decidir por la persona lo que no necesita decidir (modo, punto de entrada, nombre interno de archivos, idioma). [CONFIRMADO]
- **RF-42** En el nivel Pequeña, cuando la idea llega con muy poca información (por ejemplo "mejorar el reporte de ventas, está feo"), el asistente **nunca manda a la persona a otro nivel**: hace tres preguntas básicas con ejemplos (qué parte cambia, quién lo usa, por qué ahora) y continúa con la especificación. Lo que siga sin saberse queda como suposición o pregunta pendiente con responsable. [CONFIRMADO]
- **RF-43** Cada artefacto tiene un **índice estructurado** (modelo híbrido) con los elementos que la plataforma debe seguir: requisitos, suposiciones y preguntas pendientes, y, cuando existan, decisiones de arquitectura, épicas e historias. El índice se **reconstruye leyendo el Markdown** con un formato estricto y se actualiza en cada guardado. Los datos que no viven en el texto (responsable y estado de una pregunta pendiente, estado de una suposición) se guardan en el índice y la exportación los muestra. Si una edición manual rompe el formato, la plataforma **avisa** y no pierde el texto. [CONFIRMADO]
- **RF-44** Los elementos de un artefacto se numeran con un esquema propio, **con guion y con número para todos**: requisitos funcionales `FR-n`, no funcionales `NFR-n`, recorridos de usuario `UJ-n`, métricas de éxito `SM-n`, suposiciones `A-n` (en el texto, `[ASSUMPTION: A-n]`), preguntas pendientes `Q-n` y decisiones de arquitectura `AD-n`; las épicas e historias conservan el formato de BMAD (`Epic N`, `Story N.M`) y cada historia cita los `FR-n`, `NFR-n` y `AD-n` que implementa. Un número **nunca se reutiliza ni se reordena**: lo que se quita queda marcado como retirado y lo nuevo recibe el siguiente número libre. [CONFIRMADO]

### Asistente guiado
- **RF-10** El asistente conversa con el usuario y guía el recorrido por las fases Aclarar y Planificar, mostrándolo de forma visible. [CONFIRMADO]
- **RF-11** Un cambio pequeño genera solo una especificación corta, sin planificación completa. La especificación es la de cinco partes de `bmad-spec` (Por qué, Capacidades con intención y condición de éxito, Restricciones, No objetivos, Señal de éxito). Nota: en BMAD un cambio pequeño también puede construirse directamente con un plan escrito, sin especificación formal; la plataforma ofrece la especificación porque su fin es entregar a desarrollo algo verificable. [VALIDADO EN BMAD 6.12.1, mapeo aproximado]
- **RF-12** El asistente ofrece modo rápido (agrupa los vacíos en una o dos preguntas y redacta con suposiciones marcadas para que la persona las corrija) y modo guiado (sección por sección), tras un volcado libre de la idea y una calibración de lo que está en juego (aficionado, interno o lanzamiento). BMAD los llama Fast y Coaching. [VALIDADO EN BMAD 6.12.1]
- **RF-13** El historial de la conversación se guarda por iniciativa. [CONFIRMADO]
- **RF-14** El asistente usa el contexto adjunto cuando el modelo pueda interpretarlo. [CONFIRMADO]
- **RF-15** El proveedor de IA es Gemini (capa gratuita) detrás de `IAssistantService`, reemplazable. [CONFIRMADO]
- **RF-45** La iniciativa pasa de *Aclarando* a *Planificando* **solo cuando la persona lo confirma** de forma explícita (por ejemplo "Sí, pasar a Planificar"), nunca de forma automática, porque desde *Planificando* el nivel de profundidad queda bloqueado (RF-29). El asistente ofrece la confirmación cuando las preguntas de aclarar están cubiertas, y la persona puede elegir "Quiero añadir algo" y seguir conversando. **La confirmación no vence:** la iniciativa permanece en *Aclarando* el tiempo que haga falta y, al volver, la conversación retoma en ese punto. [CONFIRMADO]
- **RF-46** El guion inicial del asistente es una lista propia de temas por nivel, inspirada en BMAD y basada en las cinco partes de su especificación, una pregunta por vez con ejemplo y respuestas rápidas. **Aclarar** (Estándar): la idea con sus palabras, quién la usa, qué problema resuelve, cómo se sabrá que funcionó, qué queda fuera, y, solo en modo Automático sin nivel, qué tan grande es (para sugerir el nivel); cierra con la confirmación de RF-45. **Planificar:** qué debe poder hacer, qué límites o reglas hay y qué dos cosas son lo más importante. Pequeña usa unas 6 preguntas y no tiene planificar; Estándar unas 9 a 10; Grande añade otros sistemas o equipos involucrados y cualidades como velocidad o seguridad (unas 11). Es un punto de partida que se ajusta con el uso real. [CONFIRMADO]
- **RF-47** En modo Automático, cuando la persona acepta el nivel que sugiere el asistente, la iniciativa **pasa a modo Manual con ese nivel** (la ficha muestra "Manual" y el nivel elegido). No se conserva el origen "sugerido por el asistente". Mientras la iniciativa esté en *Borrador* o *Aclarando* el nivel se puede seguir cambiando desde la edición (RF-29). [CONFIRMADO]
- **RF-48** Hasta que se integre la IA real (Gemini), el asistente ofrece **solo el modo guiado**, paso a paso. El modo rápido (RF-12) se habilita con esa integración, porque requiere redactar un borrador con suposiciones, algo que el asistente de demostración no puede hacer. No se muestra una opción de modo rápido que no cambie nada. [CONFIRMADO]
- **RF-49** En la conversación la persona puede escribir siempre con sus palabras, aunque haya respuestas rápidas (hasta 2.000 caracteres por mensaje), y dispone de un botón **"Deshacer mi última respuesta"**: la respuesta y la reacción del asistente dejan de mostrarse, la pregunta vuelve a aparecer y el avance se recalcula. Solo se puede deshacer la última respuesta, y no una que ya cambió el estado de la iniciativa (por ejemplo la confirmación de pasar a *Planificando*). Si lo deshecho se conserva oculto en el historial o se borra se decide en el diseño. [CONFIRMADO]

### Artefactos
- **RF-16** La plataforma genera Brief, PRD, Arquitectura y Épicas e historias. Los dos últimos son borradores para revisión de desarrollo. Con el formato de BMAD: Brief (`brief.md`), PRD (`prd.md`), Arquitectura (`ARCHITECTURE-SPINE.md`) y Épicas e historias (un solo `epics.md`), con las secciones de `docs/bmad-referencia.md`. [CONFIRMADO; formato VALIDADO EN BMAD 6.12.1]
- **RF-17** El usuario puede editar un artefacto y pedir al asistente que lo regenere. [CONFIRMADO]
- **RF-18** Cada artefacto tiene número de versión; el historial completo y la comparación entre versiones quedan fuera del MVP. [SUPUESTO]
- **RF-19** El usuario aprueba manualmente cada artefacto antes de marcarlo como final. [CONFIRMADO]
- **RF-20** El contenido se guarda como Markdown, que es la fuente única de verdad **del texto** de cada documento. La plataforma mantiene además un índice estructurado, derivado del Markdown, para lo que necesita seguir (ver RF-43). [CONFIRMADO]
- **RF-21** Exportación a `.md` (para developers) y `.pdf` (para el usuario), con descarga; el `.md` también se puede copiar. [CONFIRMADO]

### Contexto adjunto
- **RF-22** El adjunto es opcional y admite cualquier tipo de contenido (archivos, enlaces, notas); los tipos que proponga el usuario son prioritarios. [CONFIRMADO]
- **RF-23** Los archivos se guardan mediante `IFileStorage` (local o S3, por configuración), nunca en MySQL. [CONFIRMADO]
- **RF-24** Solo los tipos que el modelo interpreta se envían al asistente; el resto se conserva como referencia. [SUPUESTO]
- **RF-25** Videos y prototipos se almacenan como evidencia; su análisis con IA queda para una segunda versión. [SUPUESTO]

## 3. Reglas de negocio

- **RN-01** Ningún artefacto es final sin aprobación manual del usuario.
- **RN-02** Arquitectura y Épicas/historias son borradores sujetos a revisión del equipo de desarrollo.
- **RN-03** El Markdown es la única fuente de verdad del texto; el índice estructurado se reconstruye a partir de él y el PDF se genera bajo demanda. Nunca se guarda una copia del texto en otro formato.
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
