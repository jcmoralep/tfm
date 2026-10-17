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
- Los requisitos no funcionales **no tienen formato de identificador** en el PRD. La propia plantilla del PRD (`bmad-prd/assets/prd-template.md`) usa `FR-1`, `UJ-1` y `SM-1` con guion; el paso 1 de `bmad-create-epics-and-stories` describe su extracción como flexible ("FR1:", "Functional Requirement 1:", "or similar"), y la ejecución real del 2026-10-14 (sección 7, comprobación 2) leyó `FR-n` con guion sin renumerar. Lo que se creyó antes (que el paso exigía `FR1:` y `NFR1:` sin guion) quedó desmentido; la decisión vigente está en el punto siguiente.
- **Decisión (cambio `bmad-alignment`, 2026-10-11; revisada en el cambio `architecture-epics`, 2026-10-14):** la plataforma y el PRD conservan el guion, y el inventario de `epics.md` (paso 6) **también conserva el guion y el mismo número** (`FR-3` sigue siendo `FR-3`; no hay conversor). La decisión original de escribir `FR1:`/`NFR1:` se revirtió porque la ejecución real de la comprobación 2 (sección 7) mostró que `bmad-create-epics-and-stories` acepta `FR-n` con guion sin renumerar; `FR3:` era una elección de la plataforma. La exportación (paso 8) copia el PRD tal cual. `NFR-n` es una extensión de la plataforma.
- **Arquitectura (paso 6):** sigue la forma de `ARCHITECTURE-SPINE.md` con siete secciones y bloques `AD-n` con *Vincula (binds)*, *Previene (prevents)* y *Regla (rule)* (contienen las palabras en minúscula que pide `lint_spine.py`). No propone tecnologías: el stack y el esqueleto del proyecto van en **Diferido** y en una pregunta para desarrollo. La recomendación de stack verificada en la web (un paso propio de BMAD) no se implementa; está en `docs/mejoras-posteriores.md`. Comprobación 1 de `lint_spine.py` (2026-10-14): `ok: true`, sin hallazgos; el control de marcadores es sensible a mayúsculas.
- **Épicas e historias (paso 6):** mantienen en inglés las palabras estructurales de BMAD (`Epic`, `Story`, `As a / I want / So that`, `Acceptance Criteria`, `Given / When / Then`) y el contenido en español; la línea de cita es `**Cubre:** FR-n, NFR-n`. El inventario, el mapa de cobertura y las preguntas por requisito sin historia los calcula el código. Comprobado con BMAD real el 2026-10-14 (sección 7 bis): `bmad-create-epics-and-stories` y `bmad-build` aceptan el documento exportado; el descubrimiento de archivos del paso de épicas no es recursivo, por eso la nota de uso de la exportación pide copiar el PRD y la arquitectura sueltos a `planning-artifacts/` o dar sus rutas.
- No hay carpetas por "iniciativa" en BMAD: la palabra solo aparece como altura de la arquitectura y en un menú del PRD.
- La salida usa nombres inconsistentes entre artefactos (carpetas con fecha, `spec-<slug>` sin fecha, `epics.md` plano).

## 5. No confirmado

Muestras de salida (la carpeta de salida está vacía), el detalle de `bmad-ux` y `bmad-prfaq`, las referencias de modo sin interfaz del PRD y la arquitectura, el esquema de planificación de sprints, el formato de `.memlog.md` y los scripts `render_skill.py` y `memlog.py` completos.

## 6. Seguridad de la instalación

Todo corre con `uv run` y scripts de Python locales; no se encontró código de red, `eval` ni descargas en esos scripts. No hay hooks ni archivos de permisos en `.claude`. Los accesos a la red vienen de **instrucciones** de las habilidades (investigación en la web) y no de código. `bmad-build` hace una confirmación de git con mensaje convencional cuando hay un repositorio. Se leyó con búsquedas, no línea por línea.

## 7. Verificación en una instalación real (lista del cambio `bmad-alignment`, ejecutada el 2026-10-14)

Estas tres comprobaciones necesitan ejecutar BMAD de verdad. El cambio `bmad-alignment` solo dejó escrita la lista; **se ejecutaron el 2026-10-14** y la tabla siguiente registra el resultado. Para repetirlas con una versión nueva de BMAD: Cómo prepararlas: instalar BMAD 6.12.1 (módulo `bmm`) en una carpeta desechable **fuera de este repositorio**, inicializar un repositorio git vacío allí y copiar los archivos de ejemplo desde `tests/RutaProducto.Infrastructure.Tests/Artifacts/Goldens/`. Si una comprobación contradice este documento, corregir el §4 y el punto 3 de la sección 6 de `AGENTS.md`.

1. **`bmad-build` acepta una especificación de nivel Pequeña.**
   - Cómo: copiar `spec-small.md` como `specs/spec-demo/SPEC.md` y pedir a `bmad-build` que construya a partir de esa carpeta (`{spec_folder}`), o pasarle la ruta del archivo.
   - Esperado: la lee como contexto de planificación sin quejarse de la estructura (Por qué, Capacidades con `FR-n`, Restricciones, No objetivos, Señal de éxito) y sin exigir `CAP-N` ni el frontmatter de su plantilla. Anotar qué pide si se queja.
2. **El paso de épicas lee los `FR-n` de un PRD exportado.**
   - Cómo: copiar `prd-large.md` como `planning-artifacts/prds/prd-demo/prd.md` y ejecutar el paso 1 de `bmad-create-epics-and-stories` (validar requisitos).
   - Esperado: extrae los requisitos funcionales y no funcionales del PRD y escribe en el inventario los mismos números que `FR-1`, `NFR-1`… del PRD (el plan original era `FR1:`, `NFR1:`; el cambio `architecture-epics` lo sustituyó por el guion tras esta comprobación). Anotar si pierde alguno o los renumera.
3. **BMAD tolera los encabezados en español y el sufijo de las preguntas.**
   - Cómo: con el mismo `prd-large.md`, editar una línea de la sección 8 para que lea `- **Q-1**: … (Responsable: Legal; Revisar: antes de la arquitectura)` y pedir a `bmad-prd` (o al paso de épicas) que lea el documento.
   - Esperado: los encabezados en español (`## 8 Preguntas abiertas`, etc.) y la línea `Q-n` con su sufijo se leen sin pérdida ni reescritura; el sufijo se conserva como texto de la pregunta.

| Comprobación | Fecha | Versión | Resultado | Evidencia |
|---|---|---|---|---|
| 1. `bmad-build` con `SPEC.md` | 2026-10-14 | 6.12.1 | cumple | Construcción completa: tomó `SPEC.md` como intención inicial (sin `status`, no lo reanuda), usó FR-1..3 y respondió Q-1 con A-2; 10 pruebas pasan y creó su propio `spec-order-lookup.md`. Sin quejas de estructura; sí de que Q-1 y A-2 se contradicen, de FR sin criterios de aceptación y de que el comando de verificación del ejemplo falla en Node 24. |
| 2. Épicas: lectura de `FR-n` con el mismo número (el plan original esperaba `FR1:`) | 2026-10-14 | 6.12.1 | cumple en la lectura; la regla `FR1:` no se confirmó y se descartó | Extrajo 5 FR y 2 NFR sin perder ninguno ni renumerar, pero escribió `FR-1` con guion (decisión del agente, para trazar con el PRD). El paso no obliga a `FR1:`. |
| 3. Encabezados en español y sufijo `Q-n` | 2026-10-14 | 6.12.1 | cumple | Cumple también con `bmad-prd` en modo sin interfaz (validar): `prd.md` quedó idéntico byte a byte; leyó los encabezados en español, `FR/NFR/SM/A-n` y la línea `Q-1` con su sufijo como responsable y condición de revisión. Nota: la evaluación fue "Fair" por contenido del ejemplo. |

Notas de la ejecución del 2026-10-14: `bmad-build` necesita `uv` en el PATH (sin él se detiene al activarse) y, en modo sin interfaz, permitir `uv run`. El inventario de `epics.md` aceptó `FR-n` con guion, así que escribir `FR3:` (decisión del cambio `bmad-alignment`) es una elección de la plataforma y no un requisito de BMAD. La falta de frontmatter que señaló `bmad-prd` no aplica: la exportación lo añade (RF-36) y el golden es el Markdown guardado. La línea `- Pendiente: ver Q-1.` del golden `prd-large.md` es la marca intencional de un dato que falta (la plataforma la escribe a propósito y las pruebas la exigen); BMAD la conservó como una nota dentro de la lista, sin dañar nada. No requiere ajuste.

## 7 bis. Comprobaciones del cambio `architecture-epics` (2026-10-14, BMAD 6.12.1)

| Comprobación | Resultado | Evidencia |
|---|---|---|
| `lint_spine.py` sobre la Arquitectura exportada | cumple | `{"ok": true, "total_findings": 0}`. La detección de TBD/TODO/FIXME/XXX distingue mayúsculas: «todo» en minúscula no se marca; «TODO» en mayúsculas dentro del texto sí (falso positivo posible). Las etiquetas `Vincula (binds)`, `Previene (prevents)` y `Regla (rule)` pasan porque el script busca esas palabras en inglés sin distinguir mayúsculas. |
| Paso 1 de épicas sobre el PRD y la Arquitectura exportados | contenido bien, **descubrimiento falla** | Los patrones del paso (`*prd*.md`, `*prd*/index.md`, `*architecture*.md`, `*architecture*/index.md`) no son recursivos: no encuentran `prds/prd-x/prd.md` ni `architecture/architecture-x/ARCHITECTURE-SPINE.md`. Con las rutas a mano mantuvo `FR-n`, `NFR-n` y `AD-n` con guion y los mismos números, y aceptó el español. La nota de uso de la Arquitectura ahora indica copiar los archivos sueltos a `planning-artifacts/` o dar las rutas. |
| `epics.md` exportado frente a `bmad-build` y a la lista de validación del paso 4 | cumple, con notas | `bmad-build` lo toma como intención inicial; `status: approved` no está en su lista (`draft`, `ready-for-dev`, `in-progress`, `in-review`, `done`) y se ignora; `stepsCompleted` e `inputDocuments` no se leen. BMAD escribe `inputDocuments` con rutas relativas completas y `status: draft`; nuestra exportación usa nombres de archivo y `approved`. La lista de validación marcó cosas de contenido de la muestra (una épica técnica, criterios débiles), no de formato. |

## 7 ter. Reglas de BMAD sobre cifras inventadas y alcance diferido (6.12.1, cambio `fix/draft-fidelity`)

| Regla de BMAD | Fuente | Cómo se aplica aquí |
|---|---|---|
| «Do not invent user detail, success metrics, or scope decisions to fill gaps — record them»; `assumptions[]` son valores inferidos sin confirmación directa y `open_questions[]` cada hueco que necesita una decisión humana | `bmad-prd/references/headless.md` | Una suposición con una cifra ajena a los textos de la persona se convierte en una pregunta abierta fija sobre la sección (RF-93), no se descarta en silencio |
| «Mark items deferred to v2/v3 explicitly» (6.2 «Out of Scope for MVP») y «De-scoping proposed honestly, not done silently» (lista de validación, sección 5) | `prd-template.md`, `prd-validation-checklist.md` | Solo lo que la persona dice que puede esperar se registra como pospuesto en «No objetivos»/«Fuera»; el modelo no recorta alcance por su cuenta (RF-93) |

## 8. Cierre de conformidad del cambio `bmad-alignment`

Resultado: **cumple con notas** (comparación por lectura de los cuatro documentos de ejemplo `Goldens/` contra las secciones 2 y 4 de este documento y contra la regla de cierre de `bmad-prd`, paso Finalize 4: lo que no bloquea se difiere con responsable y condición de revisión). No se ejecutó BMAD.

| Aspecto | Resultado |
|---|---|
| Encabezados y orden (Brief, PRD, Especificación) | Coinciden con la sección 2; los títulos van en español. |
| Esquema de identificadores | Con guion (`FR-n`, `NFR-n`, `UJ-n`, `A-n`, `Q-n`); decisión en la sección 4 (el inventario de épicas también conserva el guion). |
| Recorridos clave (PRD 2.3) | Una sola suposición `[ASSUMPTION: A-n]` con su entrada en el índice; no se inventan `UJ-n`. |
| Línea de pregunta abierta | `- **Q-n**: texto (Responsable: …; Revisar: …)`: lleva las dos piezas que pide Finalize 4. La aprobación se bloquea mientras falte alguna. |
| Índice de suposiciones | Cada `A-n` del documento aparece una vez en el índice. |

Desviaciones conocidas y aceptadas: `FR-n` en lugar de `CAP-n` en la Especificación, `NFR-n` como extensión de la plataforma, encabezados en español y el sufijo como portador de responsable y revisión. No se encontró ninguna brecha real, así que ni los documentos de ejemplo ni el generador cambiaron. Lo que va más allá de BMAD (marca de fase bloqueante, tabla de elementos, validación independiente) está en `docs/mejoras-posteriores.md`. Las tres comprobaciones con una instalación real siguen pendientes en la sección 7.
