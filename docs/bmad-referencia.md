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
- Los requisitos no funcionales **no tienen formato de identificador** en el PRD; el paso de épicas espera `FR1:` y `NFR1:` sin guion al extraerlos, y el PRD usa `FR-1`. Hay que reconciliar el formato al generar los artefactos.
- No hay carpetas por "iniciativa" en BMAD: la palabra solo aparece como altura de la arquitectura y en un menú del PRD.
- La salida usa nombres inconsistentes entre artefactos (carpetas con fecha, `spec-<slug>` sin fecha, `epics.md` plano).

## 5. No confirmado

Muestras de salida (la carpeta de salida está vacía), el detalle de `bmad-ux` y `bmad-prfaq`, las referencias de modo sin interfaz del PRD y la arquitectura, el esquema de planificación de sprints, el formato de `.memlog.md` y los scripts `render_skill.py` y `memlog.py` completos.

## 6. Seguridad de la instalación

Todo corre con `uv run` y scripts de Python locales; no se encontró código de red, `eval` ni descargas en esos scripts. No hay hooks ni archivos de permisos en `.claude`. Los accesos a la red vienen de **instrucciones** de las habilidades (investigación en la web) y no de código. `bmad-build` hace una confirmación de git con mensaje convencional cuando hay un repositorio. Se leyó con búsquedas, no línea por línea.
