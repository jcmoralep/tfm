# Revisión de experiencia de uso (design-critique y ux-copy)

Fecha: 2026-10-12. Se recorrieron en Chrome el detalle de una iniciativa, el asistente y el asistente de creación, como lo haría una persona de producto sin conocimientos técnicos, aplicando las skills `design-critique` y `ux-copy`. Datos: solo de ejemplo.

## Hallazgos y qué se hizo

| # | Hallazgo | Gravedad | Cambio |
|---|---|---|---|
| 1 | En el detalle, la acción principal (hablar con el asistente) quedaba debajo de los datos de la iniciativa. | Moderada | Botón principal "Abrir asistente" (o "Ver conversación" si ya está lista) junto a Editar y Eliminar. |
| 2 | En el asistente, el chat reservaba media pantalla aunque tuviera un solo mensaje y la caja de respuesta quedaba fuera de la vista en una laptop. | Moderada | La altura del chat se ajusta al contenido (máximo media pantalla). La caja de respuesta pasó de y=787 a y=642 en una pantalla de 674 px. |
| 3 | "Deshacer mi última respuesta" aparecía activo sin respuestas que deshacer. | Moderada | Solo se ofrece cuando hay algo que deshacer. |
| 4 | El avance decía "0 de 8 temas cubiertos" arriba y "0 de 5 preguntas" al lado: dos unidades para lo mismo, y repetido en el encabezado y en el panel lateral. | Menor | Una sola unidad ("preguntas respondidas") y el encabezado solo se muestra en pantallas estrechas. |
| 5 | Crear iniciativa: sin ejemplo ni pista de qué escribir. | Moderada | Ejemplo en el nombre y pista de "una o dos frases" en la descripción (también al editar). |
| 6 | "¿Cómo se define la profundidad?" con "Manual / Automático": jerga. | Moderada | "¿Quién decide qué tan completo será el plan?", con la descripción de cada opción y qué se entrega. |
| 7 | "Brief" y "PRD" sin explicación. | Menor | Una línea en cada documento: "Resumen de la idea…" y "Qué debe hacer el producto…". |

## Detectado y diferido

- En el asistente, "Por ejemplo:" y "Por qué lo pregunto:" salen como párrafos planos; conviene darles formato. Depende del texto que produzca el modelo real: va con el paso 5 (reglas de conducta RF-65).
- El título de la página cambia de "Nueva iniciativa" a "Completar iniciativa" al pasar del paso 1 al 2.
- Al abrir una página por primera vez se ve un instante "Cargando…" (el contenido se pide al iniciar el circuito).
- Sin probar: diseño en móvil de estos cambios.

## Capturas

| Captura | Qué muestra |
|---|---|
| `01-asistente-compacto.png` | Asistente con el chat ajustado al contenido. |
| `02-detalle-con-accion-principal.png` | Detalle con "Abrir asistente" arriba y los documentos con su explicación. |
| `03-crear-paso-1-con-ejemplo.png` | Paso 1 con el ejemplo y la pista. |
| `04-crear-paso-2-quien-decide.png` | Paso 2 con la nueva pregunta y descripciones. |
