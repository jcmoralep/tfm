# Valoración de alineación con BMAD (pasos 1 a 4)

Fecha: 2026-10-11. Código valorado: `main` tras el PR #7. Fuentes: `docs/bmad-referencia.md`, `docs/hallazgos-simulaciones-bmad.md`, `REQUISITOS.md`, el guion del asistente, el generador de documentos y sus goldens. Valoración de solo lectura hecha por un agente; las conclusiones son suyas y se resumen aquí.

## Veredicto: PARCIALMENTE ALINEADO (sin deriva grave)

La estructura (niveles, secciones, identificadores, suposiciones, una pregunta por vez) está bien anclada en BMAD 6.12.1 y casi todas las desviaciones importantes están decididas y escritas. El riesgo es de **forma sin fondo**: los documentos copian las plantillas, pero varias secciones salen como suposición fija, sin coaching ni verificación independiente. El esfuerzo se fue a la mecánica del producto (estados, deshacer, regenerar) más que a la calidad BMAD del contenido.

## Lo fiel a BMAD

- Niveles: Pequeña = `SPEC.md`, Estándar = Brief + PRD, Grande = PRD + Arquitectura + Épicas (RF-27).
- Una pregunta por vez con ejemplo, motivo y "No sé"; no se entrevista sobre lo que la persona no sabe.
- "No sé" se convierte en pregunta pendiente `Q-n`; las suposiciones se marcan `[ASSUMPTION: A-n]` con índice.
- Encabezados y orden: Brief (8 secciones), PRD 0 a 9, SPEC de 5 partes.

## Desviaciones deliberadas (documentadas)

Brief generado y no preguntado (RF-38); sin modo rápido ni coaching hasta Gemini (RF-48); sin calibrar "lo que está en juego" (RF-41); pregunta de tamaño y propuesta de nivel propias (RF-46); identificadores con guion (RF-44).

## Carencias no registradas como decisión

1. Relleno fijo: Brief con 3 de 8 secciones siempre suposición; SPEC "Restricciones", PRD "No usuarios" y "Glosario" también.
2. Los recorridos `UJ-n` son marcadores de posición, no un caso real con una persona con nombre (lo que BMAD y los hallazgos §1.5 valoran más).
3. Sin coaching: el asistente falso solo dice "Anotado." (RF-35 prometía pedir más ante respuestas débiles).
4. Las preguntas abiertas no tienen responsable ni bloquean la aprobación (hallazgos §1.3); RF-56 y RF-59 lo difieren.
5. Pequeña: RF-42 habla de 3 preguntas básicas para ideas pobres y el guion fija 6.

## Entrega a desarrollo

- Sin frontmatter ni nombres de carpeta de BMAD todavía (paso 8). Hay que producir carpeta por iniciativa, `<tipo>-<slug>-<fecha>/`, `title/status/created/updated`.
- `FR-n`/`NFR-n` (con guion) frente a `FR1:`/`NFR1:` que lee el paso de épicas de BMAD: sin plan de reconciliación.
- Las integraciones salen como `FR-n` ("Debe integrarse con …"): conviene una sección propia.
- La nota de uso que manda pasar `SPEC.md` a `bmad-build` hay que verificarla: los hallazgos indican que no lo descubre sin `stories.yaml`.
- Secciones en español frente a plantillas BMAD en inglés: no probado con `bmad-create-epics-and-stories`.

## Acciones propuestas

1. Antes de Gemini, escribir como requisitos las reglas de conducta BMAD del asistente y un criterio de suficiencia por sección, con pruebas de conversación.
2. Quitar el relleno fijo: preguntar (o suponer con responsable) diferenciador, no usuarios, glosario, restricciones y un recorrido real; dejar de generar `UJ-n` de marcador.
3. Validación independiente antes de aprobar y responsable en las preguntas abiertas (revisar RF-56 y RF-59).
4. Decidir ya la reconciliación `FR-n` a `FRn:`/`NFRn:`/`AD-n` y el formato de `epics.md` y `ARCHITECTURE-SPINE.md`, y probarlo en una instalación real de BMAD.
5. Diseñar el paso 8 desde BMAD (carpeta por iniciativa, frontmatter, nota de uso verificada).
6. Paso 9: prueba con personas reales; las simulaciones fueron el mismo modelo haciendo de agente y de persona.
