# Referencia de BMAD (versión 6.12.1)

Hechos tomados de una instalación real de BMAD Method 6.12.1 (módulo `bmm`) en una carpeta desechable, fuera de este repositorio, el 2026-10-09. Se leyeron los archivos instalados; no se ejecutó BMAD. Esta es la fuente para el guion del asistente (paso 3) y para el formato de los artefactos (pasos 4 y 6). Al actualizar BMAD hay que volver a contrastar este documento.

## 1. Cómo conversa BMAD (no hay lista fija de preguntas)

BMAD no define una lista de preguntas para el Brief ni para el PRD: define **reglas de conducta** para el agente. La plataforma debe escribir su propio guion siguiendo esas reglas.

- **Una pregunta por vez**, en orden de dependencia, y se insiste en los puntos débiles (`bmad-forge-idea`).
- **Sugerir la mejor respuesta**: "incluye tu mejor respuesta o hipótesis cuando ayude a responder".
- **Sin elogios** ("el elogio es ruido"), y **empujar cuando una respuesta es pobre** (`bmad-product-brief`: "Coach, do not quiz").
- **Abrir con un volcado libre**: se invita a contar todo y a aportar material, y luego "¿algo más?". Después se calibra qué está en juego (en el PRD: aficionado, interno o lanzamiento).
- **Dos caminos**, tras el volcado: **rápido** (agrupa los vacíos en una o dos preguntas y redacta con etiquetas de suposición para que la persona corrija) o **coaching** (sección por sección). Para la arquitectura el camino por defecto es el de coaching.
- `bmad-forge-idea` termina en tres salidas: endurecida, descartada o aclarada. Solo la primera escribe `forged-idea.md`.

## 2. Artefactos: nombres, carpetas y secciones

Carpeta de salida configurable (`_bmad-output` por defecto). Casi todo usa `<tipo>/<tipo>-<proyecto>-<fecha>/`.

| Artefacto | Archivo | Secciones |
|---|---|---|
| Brief (`bmad-product-brief`) | `planning-artifacts/briefs/brief-<proyecto>-<fecha>/brief.md` (+ `addendum.md`) | Resumen ejecutivo, El problema, La solución, Qué lo hace distinto, A quién sirve, Criterios de éxito, Alcance, Visión. Es una estructura inicial; se pueden quitar o añadir secciones. 1 a 2 páginas; el exceso va al addendum. |
| PRD (`bmad-prd`) | `planning-artifacts/prds/prd-<proyecto>-<fecha>/prd.md` (+ `addendum.md`) | 0 Propósito del documento, 1 Visión, 2 Usuario objetivo (2.1 trabajos por hacer, 2.2 no usuarios, 2.3 recorridos clave), 3 Glosario, 4 Funcionalidades (cada una con descripción y requisitos funcionales), 5 No objetivos, 6 Alcance del MVP (6.1 dentro, 6.2 fuera), 7 Métricas de éxito, 8 Preguntas abiertas, 9 Índice de suposiciones. |
| Especificación (`bmad-spec`) | `specs/spec-<slug>/SPEC.md` (sin fecha) | Núcleo de cinco partes: **Por qué**, **Capacidades** (`CAP-N`, cada una con intención y condición de éxito), **Restricciones**, **No objetivos** (al menos uno), **Señal de éxito**; opcionales: suposiciones y preguntas abiertas. |
| Arquitectura (`bmad-architecture`) | `planning-artifacts/architecture/architecture-<proyecto>-<fecha>/ARCHITECTURE-SPINE.md` | Paradigma de diseño, invariantes heredadas, invariantes y reglas (`AD-n` con Une / Previene / Regla), convenciones de consistencia, pila, esqueleto estructural, mapa capacidad a arquitectura, diferido. |
| Épicas e historias (`bmad-create-epics-and-stories`) | `planning-artifacts/epics.md` (un solo archivo plano) | Resumen, inventario de requisitos (funcionales, no funcionales, adicionales, de UX), mapa de cobertura de requisitos, lista de épicas, `## Epic N: título` con objetivo, `### Story N.M: título` ("Como / quiero / para") y **Criterios de aceptación** en Dado / Cuando / Entonces. |

- Frontmatter típico: `title`, `status` (borrador a final), `created`, `updated`.
- Identificadores: `FR-1` (requisitos funcionales, numerados globalmente), `UJ-N` (recorridos), `SM-1` (métricas), `CAP-N`, `AD-n`, épicas `N`, historias `N.M`.
- Etiqueta de suposición: `[ASSUMPTION: ...]` en línea, más un índice de suposiciones al final del PRD. Otras: `[NOTE FOR PM]`, `[NON-GOAL for MVP]`.
- Regla de orden de las historias: no pueden depender de historias futuras de la misma épica; las épicas se organizan por valor para el usuario.
- Cada ejecución conserva una memoria de solo añadir (`.memlog.md`) de la que se deriva el documento; `SPEC.md` "no se edita a mano".

## 3. Tamaño y ruta (mapeo de nuestros niveles)

BMAD no tiene una taxonomía explícita pequeño / épica / proyecto en los archivos instalados. Solo expresa el tamaño así: un cambio pequeño de `bmad-build` se resuelve con un plan mínimo, una especificación para trabajo con huecos de intención, el PRD escala con lo que está en juego, y la arquitectura tiene tres alturas (iniciativa, funcionalidad, épica).

| Nuestro nivel | Qué entrega la plataforma | Fidelidad a BMAD |
|---|---|---|
| Pequeña | Una especificación de cinco partes (`SPEC.md`) | Aproximada. En BMAD un cambio pequeño puede construirse directo con un plan escrito, sin especificación. La especificación es su herramienta para "fijar qué construir". |
| Estándar | Brief y PRD | Razonable. El Brief es opcional en BMAD y el PRD no lo exige; no incluye arquitectura ni épicas. |
| Grande | PRD, Arquitectura y Épicas e historias | Coincide con la secuencia de `module-help`: la arquitectura va antes de las épicas, y las épicas requieren PRD y arquitectura. El diseño de UX (`bmad-ux`) es un paso opcional entre el PRD y la arquitectura; queda fuera del MVP. |

## 4. Diferencias con lo que se había supuesto

- **No existen** `bmad-ticket` ni `tickets.toml` en esta versión. Las "Épicas e historias" corresponden a `bmad-create-epics-and-stories` y a `epics.md`.
- Los requisitos no funcionales **no tienen formato de identificador** en el PRD; el paso de épicas espera `FR1:` y `NFR1:` sin guion al extraerlos, y el PRD usa `FR-1`. La propia plantilla del PRD (`bmad-prd/assets/prd-template.md`) usa `FR-1`, `UJ-1` y `SM-1` con guion; solo el paso 1 de `bmad-create-epics-and-stories` escribe `FR1:`/`NFR1:`, en el inventario que arma dentro de `epics.md`, y su extracción es flexible ("FR1:", "Functional Requirement 1:", "or similar").
- **Decisión (cambio `bmad-alignment`, 2026-10-11):** la plataforma y el PRD conservan el guion; el generador de épicas (paso 6) escribe el inventario de `epics.md` como `FR1:`/`NFR1:` **con el mismo número** (`FR-3` pasa a `FR3:`); la exportación (paso 8) copia el PRD tal cual. `NFR-n` es una extensión de la plataforma. Base: lectura del código de BMAD, no una ejecución; se comprueba en la instalación real antes del paso 6.
- No hay carpetas por "iniciativa" en BMAD: la palabra solo aparece como altura de la arquitectura y en un menú del PRD.
- La salida usa nombres inconsistentes entre artefactos (carpetas con fecha, `spec-<slug>` sin fecha, `epics.md` plano).

## 5. No confirmado

Muestras de salida (la carpeta de salida está vacía), el detalle de `bmad-ux` y `bmad-prfaq`, las referencias de modo sin interfaz del PRD y la arquitectura, el esquema de planificación de sprints, el formato de `.memlog.md` y los scripts `render_skill.py` y `memlog.py` completos.

## 6. Seguridad de la instalación

Todo corre con `uv run` y scripts de Python locales; no se encontró código de red, `eval` ni descargas en esos scripts. No hay hooks ni archivos de permisos en `.claude`. Los accesos a la red vienen de **instrucciones** de las habilidades (investigación en la web) y no de código. `bmad-build` hace una confirmación de git con mensaje convencional cuando hay un repositorio. Se leyó con búsquedas, no línea por línea.

## 7. Verificación pendiente en una instalación real (antes del paso 6)

Estas tres comprobaciones necesitan ejecutar BMAD de verdad. **No se han ejecutado**: el cambio `bmad-alignment` solo dejó escrita la lista. Cómo prepararlas: instalar BMAD 6.12.1 (módulo `bmm`) en una carpeta desechable **fuera de este repositorio**, inicializar un repositorio git vacío allí y copiar los archivos de ejemplo desde `tests/BmadPlatform.Infrastructure.Tests/Artifacts/Goldens/`. Si una comprobación contradice este documento, corregir el §4 y el punto 3 de la sección 6 de `AGENTS.md` antes de construir el paso 6.

1. **`bmad-build` acepta una especificación de nivel Pequeña.**
   - Cómo: copiar `spec-small.md` como `specs/spec-demo/SPEC.md` y pedir a `bmad-build` que construya a partir de esa carpeta (`{spec_folder}`), o pasarle la ruta del archivo.
   - Esperado: la lee como contexto de planificación sin quejarse de la estructura (Por qué, Capacidades con `FR-n`, Restricciones, No objetivos, Señal de éxito) y sin exigir `CAP-N` ni el frontmatter de su plantilla. Anotar qué pide si se queja.
2. **El paso de épicas lee los `FR-n` de un PRD exportado.**
   - Cómo: copiar `prd-large.md` como `planning-artifacts/prds/prd-demo/prd.md` y ejecutar el paso 1 de `bmad-create-epics-and-stories` (validar requisitos).
   - Esperado: extrae los requisitos funcionales y no funcionales del PRD y escribe en el inventario `FR1:`, `FR2:`… y `NFR1:`… con **los mismos números** que `FR-1`, `NFR-1`… del PRD. Anotar si pierde alguno o los renumera.
3. **BMAD tolera los encabezados en español y el sufijo de las preguntas.**
   - Cómo: con el mismo `prd-large.md`, editar una línea de la sección 8 para que lea `- **Q-1**: … (Responsable: Legal; Revisar: antes de la arquitectura)` y pedir a `bmad-prd` (o al paso de épicas) que lea el documento.
   - Esperado: los encabezados en español (`## 8 Preguntas abiertas`, etc.) y la línea `Q-n` con su sufijo se leen sin pérdida ni reescritura; el sufijo se conserva como texto de la pregunta.

| Comprobación | Fecha | Versión | Resultado | Evidencia |
|---|---|---|---|---|
| 1. `bmad-build` con `SPEC.md` | 2026-10-14 | 6.12.1 | cumple | Construcción completa: tomó `SPEC.md` como intención inicial (sin `status`, no lo reanuda), usó FR-1..3 y respondió Q-1 con A-2; 10 pruebas pasan y creó su propio `spec-order-lookup.md`. Sin quejas de estructura; sí de que Q-1 y A-2 se contradicen, de FR sin criterios de aceptación y de que el comando de verificación del ejemplo falla en Node 24. |
| 2. Épicas: `FR-n` a `FR1:` con el mismo número | 2026-10-14 | 6.12.1 | cumple en la lectura; la regla `FR1:` no se confirmó | Extrajo 5 FR y 2 NFR sin perder ninguno ni renumerar, pero escribió `FR-1` con guion (decisión del agente, para trazar con el PRD). El paso no obliga a `FR1:`. |
| 3. Encabezados en español y sufijo `Q-n` | 2026-10-14 | 6.12.1 | cumple | Cumple también con `bmad-prd` en modo sin interfaz (validar): `prd.md` quedó idéntico byte a byte; leyó los encabezados en español, `FR/NFR/SM/A-n` y la línea `Q-1` con su sufijo como responsable y condición de revisión. Nota: la evaluación fue "Fair" por contenido del ejemplo. |

Notas de la ejecución del 2026-10-14: `bmad-build` necesita `uv` en el PATH (sin él se detiene al activarse) y, en modo sin interfaz, permitir `uv run`. El inventario de `epics.md` aceptó `FR-n` con guion, así que escribir `FR3:` (decisión del cambio `bmad-alignment`) es una elección de la plataforma y no un requisito de BMAD. La falta de frontmatter que señaló `bmad-prd` no aplica: la exportación lo añade (RF-36) y el golden es el Markdown guardado. La línea `- Pendiente: ver Q-1.` del golden `prd-large.md` es la marca intencional de un dato que falta (la plataforma la escribe a propósito y las pruebas la exigen); BMAD la conservó como una nota dentro de la lista, sin dañar nada. No requiere ajuste.

## 8. Cierre de conformidad del cambio `bmad-alignment`

Resultado: **cumple con notas** (comparación por lectura de los cuatro documentos de ejemplo `Goldens/` contra las secciones 2 y 4 de este documento y contra la regla de cierre de `bmad-prd`, paso Finalize 4: lo que no bloquea se difiere con responsable y condición de revisión). No se ejecutó BMAD.

| Aspecto | Resultado |
|---|---|
| Encabezados y orden (Brief, PRD, Especificación) | Coinciden con la sección 2; los títulos van en español. |
| Esquema de identificadores | Con guion (`FR-n`, `NFR-n`, `UJ-n`, `A-n`, `Q-n`); decisión y excepción del paso de épicas en la sección 4. |
| Recorridos clave (PRD 2.3) | Una sola suposición `[ASSUMPTION: A-n]` con su entrada en el índice; no se inventan `UJ-n`. |
| Línea de pregunta abierta | `- **Q-n**: texto (Responsable: …; Revisar: …)`: lleva las dos piezas que pide Finalize 4. La aprobación se bloquea mientras falte alguna. |
| Índice de suposiciones | Cada `A-n` del documento aparece una vez en el índice. |

Desviaciones conocidas y aceptadas: `FR-n` en lugar de `CAP-n` en la Especificación, `NFR-n` como extensión de la plataforma, encabezados en español y el sufijo como portador de responsable y revisión. No se encontró ninguna brecha real, así que ni los documentos de ejemplo ni el generador cambiaron. Lo que va más allá de BMAD (marca de fase bloqueante, tabla de elementos, validación independiente) está en `docs/mejoras-posteriores.md`. Las tres comprobaciones con una instalación real siguen pendientes en la sección 7.
