# Mejoras para una fase posterior del MVP

Regla del proyecto (decisión del 2026-10-11): primero se sigue lo que BMAD ya hace; lo que va más allá de BMAD se anota aquí y se aplica en una fase posterior. Antes de agregar algo, comprobar si BMAD ya lo resuelve (copia del código de BMAD, `plan/bmad-prd/`).

| Mejora | Por qué no ahora | Origen |
|---|---|---|
| Tabla de elementos con responsable, estado (abierta / respondida) y fase que bloquea (RF-43) | BMAD solo registra "responsable + condición de revisión" en el texto; la tabla es extra | `bmad-alignment` |
| Lista cerrada o directorio de responsables | BMAD no impone quiénes pueden ser responsables | `bmad-alignment` |
| Marca de "bloquea la fase X" por pregunta | BMAD resuelve los bloqueantes antes de seguir; no hay un campo | `bmad-alignment` |
| Medida de densidad de preguntas abiertas frente a lo que está en juego como bloqueo | Lo hace la validación independiente (paso 5) | hallazgos BMAD |
| Validación independiente antes de aprobar y revisión tipo "party mode" | Paso 5 en adelante | valoración BMAD |
| Directorio de personas y roles | Fuera del MVP (RF-03) | `bmad-alignment` |
| Archivar `assistant-conversation` y `artifact-generation` en `openspec/specs/` | Es una práctica de la herramienta SDD, no de BMAD: BMAD deja la especificación de cada cambio con `status: done` y su `baseline_commit`, sin fusionarla en una especificación global. El review nativo no puede completarse en Claude Code, así que el archivado oficial queda bloqueado | `bmad-alignment` |
