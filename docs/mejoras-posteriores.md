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
| La IA propone un responsable y una condición de revisión para cada pregunta pendiente; la persona confirma antes de guardar | BMAD deja esas decisiones a la persona (triaje de `bmad-prd`: aplazar con responsable y condición de revisión). Con Gemini (paso 5) sería una sugerencia, nunca una asignación automática, como la regla RF-65 de sugerir una hipótesis | decisión del usuario, 2026-10-12 |
| Una llamada al modelo solo para juzgar la última pregunta de cada fase (Pequeña: éxito; Estándar: fuera de alcance y prioridades; Grande: calidades), que hoy no recibe insistencia | El cambio `gemini-assistant` no llama al modelo en turnos que no son de pregunta, y el cierre de fase lo es; una llamada extra por esas cuatro transiciones consume cuota gratuita | decisión del usuario, 2026-10-13 |
| Regla de estructuras mixtas: cuándo una decisión de alto riesgo no puede quedar como pregunta pendiente | BMAD la deja al criterio de la persona y del revisor; automatizarla es un extra | `bmad-alignment` |
| Preguntas de relleno que quedan en el guion fijo y preguntas nuevas del guion | Cambiar el guion del asistente es otro alcance; se revisa con el paso 5 (Gemini real) | `bmad-alignment` |
| Acompañamiento (coaching) para quien no sabe responder | BMAD lo hace en la conversación; la plataforma solo guarda "No sé" como pregunta pendiente | `bmad-alignment` |
| Nombres de archivo de exportación, frontmatter y nota de uso | Pertenecen a los pasos 8 y 9 (exportación) | `bmad-alignment` |
| `Q1:` sin guion no cuenta como pregunta y no activa el bloqueo de aprobación; `(Revisar: X)` solo no se reconoce y el siguiente guardado agrega el sufijo completo | Se valida con la revisión del paso 5; reconocerlos sería más permisivo que BMAD | `bmad-alignment` |
| Modo rápido y calibración de lo que está en juego (aficionado, interno, lanzamiento), con la regla de estructuras mixtas (qué decisión de alto riesgo no puede quedar como pregunta pendiente) | Es el cambio posterior al paso 5: RF-48 ya no ata el modo guiado a Gemini, sino a este cambio. Agrupar vacíos y redactar con suposiciones es otro alcance (RF-12) | decisión del usuario, 2026-10-13 |
| Temas nuevos del guion, empezando por el recorrido clave real de una persona con nombre (RF-64, hoy una suposición) | Cambiar el guion cambia el conteo y el orden de las preguntas (RF-46); se decide con el uso real del paso 5 | decisión del usuario, 2026-10-13 |
| Que el modelo sugiera el nivel de profundidad cuando la respuesta de tamaño es "No sé" | El nivel lo decide el guion y nunca el modelo en el paso 5 (`SuggestedDepth` siempre vacío) | `gemini-assistant`, 2026-10-13 |
| Modelos distintos para los turnos de conversación y para los borradores de documentos (`Gemini:Model` por tipo de llamada) | Hoy un solo modelo configurable (RF-68) basta; separar solo si la medición muestra que conviene uno rápido para turnos y otro más capaz para documentos | decisión del usuario, 2026-10-13 |
| Pasar a la Interactions API de Gemini si `generateContent` se retira | `generateContent` sigue vigente; el cliente está aislado en `Infrastructure/Gemini/`, así que el cambio queda en una carpeta | `gemini-assistant`, 2026-10-13 |
| Archivar `assistant-conversation` y `artifact-generation` en `openspec/specs/` | Es una práctica de la herramienta SDD, no de BMAD: BMAD deja la especificación de cada cambio con `status: done` y su `baseline_commit`, sin fusionarla en una especificación global. El review nativo no puede completarse en Claude Code, así que el archivado oficial queda bloqueado | `bmad-alignment` |
