# Recorrido en Chrome de `architecture-epics` (2026-10-14)

Registro de lo observado en las comprobaciones manuales de S1, S3, S4 y S5 (aplicación con MySQL local, usuario temporal, iniciativa Grande «Consulta de pedidos»). No hay capturas en el repositorio, por decisión del usuario.

| Rebanada | Observado |
|---|---|
| S1 | Con el recorrido de la conversación terminado y el PRD sin aprobar, la tarjeta de Artefactos mostró Arquitectura como «Aún no disponible: Primero apruebe el PRD…»; el PRD ofreció «Generar». Sin errores en `Logs/` salvo un aviso inofensivo al abandonar la página durante una carga. |
| S3 | Con el PRD aprobado, Arquitectura ofreció «Generar»; el documento se generó con decisiones `AD-n`, mapa de capacidades, la pila en «Diferido» y preguntas para desarrollo con «Responsable: Equipo de desarrollo; Revisar: antes de construir»; la confirmación de aprobar habló de la revisión de desarrollo; al editar el PRD apareció el aviso ámbar en la Arquitectura; la descarga se llamó `architecture-…md` con `type: architecture-spine`. |
| S4 | Con PRD y Arquitectura aprobados se generaron las Épicas (inventario, mapa de cobertura, `Epic N` / `Story N.M`); al aprobarlas la iniciativa pasó a «Lista para construir»; con el PRD reaprobado siguió lista y las dependientes mostraron el aviso ámbar; las tres descargas respondieron 200 con nombres `prd-`, `architecture-`, `epics-`. |
| S5 | Regenerar la Arquitectura con Gemini: `gemini-3.6-flash` devolvió 503 (mensaje de demora, nada guardado); con `gemini-3.1-flash-lite` respondió 200 en 5,1 s y el documento quedó sin avisos de formato. |
| S6 | Regenerar las Épicas con Gemini: 200 en 2,4 s, cobertura completa, aprobación y «Lista para construir»; la confirmación de regenerar ya no menciona «por asignar». |

Nota: el texto «sigue mostrando "no soportado"» de la tarea 1.10 de S1 quedó obsoleto cuando S3 y S4 activaron los tipos.
