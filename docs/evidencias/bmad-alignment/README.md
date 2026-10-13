# Evidencia de `bmad-alignment`: preguntas pendientes con responsable

Fecha: 2026-10-11. Código: rama `feat/bmad-alignment` (commit `40f76ae` más la corrección del texto del aviso). Datos: solo de ejemplo. Iniciativa de prueba: Estándar, Manual, con dos respuestas "no sé" (genera `Q-1` y `Q-2` en el Brief).

| # | Captura | Qué demuestra |
|---|---|---|
| 01 | `01-brief-con-preguntas-y-aprobar-bloqueado.png` | Brief generado con dos preguntas pendientes. Panel "Preguntas pendientes" con la insignia "Sin asignar", asteriscos rojos, aviso ámbar que lista Q-1 y Q-2, y "Aprobar" desactivado. |
| 02 | `02-validacion-responsable-invalido.png` | `Legal (TI)` como responsable: mensaje rojo "El responsable no puede contener paréntesis…"; los valores escritos se conservan. |
| 03 | `03-guardado-parcial-q1-asignada-q2-pendiente.png` | Guardado parcial: aviso verde "Preguntas guardadas. Versión 2."; Q-1 queda asignada, Q-2 sigue sin asignar y "Aprobar" sigue bloqueado. |
| 04 | `04-todas-asignadas-aprobar-habilitado.png` | Todas asignadas (versión 3): desaparece el aviso y "Aprobar" se habilita. |
| 05 | `05-brief-aprobado-con-preguntas-diferidas.png` | Brief aprobado; las líneas muestran `(Responsable: TI; Revisar: antes de la arquitectura)` y `(Responsable: Quien propone la iniciativa; Revisar: al definir las métricas)`. La versión no cambia al aprobar. |
| 06 | `06-regenerar-avisa-que-se-pierden-los-responsables.png` | Confirmación de regenerar con la frase "Los responsables de las preguntas vuelven a «por asignar»." |
| 07 | `07-conflicto-entre-pestanas-conserva-lo-escrito.png` | Dos pestañas: la pestaña desfasada recibe el aviso de conflicto y conserva `Finanzas` y `al cerrar el presupuesto`. |
| 08 | `08-movil-panel-preguntas-pendientes.png` | Panel en móvil (390 px): sin desbordamiento horizontal; controles de al menos 36 px. |

## Comprobaciones sin imagen

- Regenerar (confirmado): versión 4, Borrador, las líneas vuelven a `(Responsable: por asignar; Revisar: por definir)` y "Aprobar" se bloquea otra vez.
- Lighthouse de la página del documento (`lighthouse-brief/`): Accesibilidad 100, Buenas prácticas 100, SEO 100, Navegación con agentes 100.
- Consola del navegador: sin errores ni avisos.
- Registros de la app: 0 errores; avisos esperados de validación y de conflicto provocados a propósito.
- Vistas: sin `style=` ni `<script>` en los archivos nuevos.

## Hallazgo del recorrido (corregido)

- El texto de ayuda del panel mostraba el literal `ArtifactLabels.EditHint(view.Status, view.InitiativeStatus)`: faltaba el `@` al pasar el parámetro desde `ArtifactDocument.razor`. Corregido y comprobado: el literal ya no aparece.

## No observado en el navegador

Pequeña y Grande con preguntas, editar un documento aprobado con el editor, aprobar el PRD de un Estándar, la ruta de "Reintentar", el usuario ajeno y un responsable de 61 caracteres. Todo ello está cubierto por pruebas unitarias; el usuario ajeno y el comportamiento de edición y conflicto ya se observaron en el paso 4.
