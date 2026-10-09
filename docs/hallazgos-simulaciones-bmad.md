# Hallazgos de las simulaciones con BMAD 6.12.1

Tres iniciativas ficticias, una por nivel, recorridas con las instrucciones reales de las habilidades de BMAD instaladas en una carpeta desechable (2026-10-09). Un modelo de IA hizo de agente y de persona de producto no técnica; no se ejecutó código de BMAD (los scripts se emularon a mano). Sirve para ver estructura, longitud y fricciones; **no sustituye una prueba con personas reales**. Complementa `docs/bmad-referencia.md`.

| Nivel | Iniciativa ficticia | Qué se recorrió | Preguntas al usuario |
|---|---|---|---|
| Pequeña | Filtro por fechas en un reporte de ventas | `bmad-spec` (camino rápido) y lectura de `bmad-build` | unas 5 (2 sin responder) |
| Estándar | Portal de facturas para clientes | Brief (rápido) y PRD (coaching) | unas 30 (10 de Brief, 20 de PRD); unas 10 imposibles de responder para alguien no técnico |
| Grande | Onboarding digital de 800 empleados | PRD, arquitectura y épicas e historias | unas 59, de las cuales unas 20 requieren de verdad su conocimiento |

## 1. Lo que cambia el diseño de la plataforma

1. **Entrevistar solo para lo que la persona sabe.** Pequeña: la especificación. Estándar y Grande: el PRD. La arquitectura y las épicas e historias se **generan como borrador para desarrollo** sin preguntar (en la simulación, la persona respondió "eso lo ve el equipo de desarrollo" y aprobó 21 historias con "sí" sin leerlas).
2. **El Brief casi se repite en el PRD** (60 a 70% de sus datos reaparecen). Para Estándar conviene no hacerlo como paso aparte: generarlo del volcado inicial o dejarlo opcional.
3. **"No sé" es la respuesta dominante** (sistema de facturación, validez legal, volumen, métricas, plazos). Cada "no sé" debe convertirse en una **pregunta pendiente con responsable** (Legal, TI, el jefe...), sin bloquear la conversación. Un artefacto no pasa a *final* mientras haya un bloqueo sin responsable. Todas las preguntas llevan la respuesta rápida "No sé, anótalo como pregunta pendiente".
4. **Hay que ocultar la jerga y decidir por la persona:** modo rápido o coaching, punto de entrada, nivel de lo que está en juego, `slug`, nombre y idioma. Lo que sí se pregunta se redacta como negocio ("una venta a las 11 pm en México, ¿a qué día cuenta?" en vez de "zona horaria").
5. **Lo mejor de BMAD para replicar:** una pregunta por vez con la mejor respuesta sugerida, volcado libre al inicio, contar un caso real con una persona con nombre (recorrido de usuario), detectar contradicciones ("desde el día uno" frente a "firmar antes de entrar"), suposiciones marcadas, preguntas abiertas con responsable, no inventar respuestas.
6. **La fatiga es real:** unas 22 intervenciones del agente en dos sesiones solo para Brief y PRD. Guardar y retomar es imprescindible, y el cierre de 8 pasos del PRD debe reducirse a "revisar lo pendiente".
7. **No dejar que el modelo que genera califique su propio documento:** la validación del PRD salió "Regular" con sesgo optimista.

## 2. Datos que la plataforma debe guardar (para retomar y regenerar)

Estado de la conversación (paso, modo, nivel de lo que está en juego), temas cubiertos, registro de entradas con tipo, autor y hora, glosario (término, definición, sinónimos rechazados), recorridos de usuario, funcionalidades, requisitos funcionales y no funcionales con **identificador estable que no se reutiliza**, métricas y contramétrica, no objetivos, suposiciones (texto, estado sin confirmar / confirmada / rechazada, quién confirmó), preguntas abiertas (responsable, fase que bloquea, estado), decisiones de arquitectura con su estado (propuesta / adoptada / confirmada por desarrollo), jerarquía de épicas e historias, criterios de aceptación como filas Dado / Cuando / Entonces, mapa de cobertura de requisitos, versión y estado del artefacto.

## 3. Fallos e inconsistencias de BMAD que nos afectan

- **Identificadores:** el PRD usa `FR-1`, el paso de épicas busca `FR1:` (el modelo quita el guion sin avisar). Los requisitos no funcionales no tienen formato en el PRD, y el paso de épicas los numera por posición (`NFR1..N`), así que no son trazables. Los `AD-n` de la arquitectura nunca llegan a las épicas y las historias no tienen un campo para citar requisitos.
- **Descubrimiento de archivos:** los patrones del paso de épicas (`*prd*.md`, `*architecture*/index.md`) no coinciden con las rutas reales (`prds/prd-<proyecto>-<fecha>/prd.md`, `ARCHITECTURE-SPINE.md`). Un modelo lo resuelve buscando; un programa no. `epics.md` es un único archivo plano, así que dos iniciativas chocan.
- **Nombre de carpeta:** sale de `project_name` de la configuración de instalación, no de la iniciativa. Hay que forzarlo por iniciativa.
- **Plantillas:** el Brief no trae frontmatter aunque la habilidad lo exige; el PRD no trae `status`; la etiqueta `[ASSUMPTION]` no tiene identificador y `bmad-spec` ni siquiera la usa; no hay regla de estados "n/a" en la validación; el vocabulario de lo que está en juego difiere entre Brief (pasión, pitch, inversión, lanzamiento) y PRD (aficionado, interno, lanzamiento).
- **Dos documentos llamados "spec":** `SPEC.md` (cinco partes) y el `spec-<slug>.md` de `bmad-build`, sin mapeo escrito entre ambos. `bmad-build` no descubre un `SPEC.md` sin un `stories.yaml`.
- **Idioma y nombre:** saludo con "BMad" e inglés por la configuración de instalación.
- **Para un programa que lea estos Markdown:** ids con y sin guion, requisitos no funcionales sin número, listas libres de `Binds:`, criterios de aceptación en líneas en negrita y no en lista, etiquetas de suposición dentro de la prosa, encabezados duplicados en dos niveles, estado sin estructura por decisión.

## 4. Decisiones que quedan para el equipo

1. **Fuente de verdad:** hoy RF-20 y RN-03 dicen que el Markdown es la única fuente. Para identificadores estables, suposiciones y preguntas abiertas con responsable hace falta algo estructurado. Opciones: Markdown generado desde nuestra propia plantilla estricta y parseable, o filas estructuradas que generan el Markdown.
2. **Esquema de identificadores propio con guion:** `FR-n`, `NFR-n`, `AD-n`, `UJ-n`, `SM-n`, `A-n` (suposiciones), incluyendo identificadores para los no funcionales en el PRD.
3. **Brief para Estándar:** generarlo, hacerlo opcional o quitarlo.
4. **Pequeña:** ¿se entrega solo `SPEC.md` o también una especificación lista para construir? Con poca información, ¿se redirige a PRD, se pregunta algo más o se promueve de nivel? ¿Cómo se registra una respuesta "pregúntale a otra persona"?
5. **Arquitectura y épicas como borrador generado:** quién en desarrollo las revisa y qué preguntas se les hacen después (nube, pila, contrato con el sistema de RR. HH., plazos de cuentas, copias de seguridad, umbrales no funcionales, tamaño de historias). Sin datos de la empresa (nube, pila aprobada, reglas legales) los borradores salen con la pila **sin verificar**; guardar esos datos choca con "sin pantallas de configuración" (RF-03).
6. **Regla para lo que está en juego cuando es mixto** ("interno, pero lo usan clientes reales"): tomar el mayor rigor.
7. **Privacidad:** el ejemplo de RR. HH. muestra datos de empleados; con la capa gratuita de Gemini es crítico. Mantener la advertencia y valorar bloquear iniciativas con datos sensibles hasta pasar a un plan de pago.
8. **Idioma:** habilidades y plantillas están en inglés; la plataforma entrega en español con identificadores neutros.

## 5. Límites de esta simulación

La persona y el agente son el mismo modelo, así que el comportamiento del usuario es verosímil pero no real; no se probaron los revisores independientes ni la verificación web de versiones; los scripts de BMAD se emularon leyendo su formato; el contenido de las salidas de ejemplo vive en la carpeta desechable de la sesión (`_bmad-output/_simulation/`) y no se conserva en este repositorio.
