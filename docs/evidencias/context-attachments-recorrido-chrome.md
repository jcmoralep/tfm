# Recorrido en Chrome de `context-attachments` (2026-10-15)

Observaciones de las comprobaciones manuales (aplicación con MySQL local, proveedor falso, usuarios temporales). Sin capturas en el repositorio, por decisión del usuario.

| Rebanada | Observado |
|---|---|
| S3 | Tarjeta Contexto vacía: «0 de 20 elementos · 0 KB de 50 MB usados», sin «Disponible próximamente». Subida de un PDF de 2 MB: fila «Archivo · 2 MB» con «Descargar» y línea verde «añadido». Selección de cuatro archivos a la vez: el de 11 MB dio «El archivo supera el máximo de 10 MB.», el vacío «El archivo está vacío.», el `.exe` «Este tipo de archivo no se puede adjuntar por seguridad.» y el `.md` se añadió. Descarga: 200, 2.097.152 bytes, `application/octet-stream`, `Content-Disposition: attachment` con `filename*`, `X-Content-Type-Options: nosniff`, `Cache-Control: no-store`; un identificador al azar y la URL de otra persona respondieron 404. `Logs/` sin nombres de archivo ni errores; el disco usa claves generadas por el servidor. |
| S4 | Enlaces: `javascript:`, `data:`, `ftp://`, `example.com` y `https://` se rechazan con «Escriba un enlace completo que empiece por http:// o https://.»; `HTTP://Example.com` y `https://example.com/a?b=1` se añaden con aviso verde y muestran el dominio en un enlace con `target="_blank"` y `rel="noopener noreferrer"`. Notas: título y texto con asterisco rojo; título vacío, texto en blanco, título de 121 caracteres y texto de 5.001 se rechazan con su mensaje; una nota con `<b>hola</b> <script>x</script>` se guarda y se muestra literal, sin elementos creados. Borrado: el diálogo «¿Eliminar este elemento del contexto?» con «Cancelar» conserva el elemento y «Sí, eliminar» lo quita (archivo, nota y enlace) con el aviso verde «Elemento eliminado del contexto.»; la carpeta de almacenamiento quedó sin archivos. `Logs/`: solo avisos de validación, sin contenido. |

No se ejercitaron en estas rebanadas: el aviso de más de 5 archivos, el tope de 20 elementos y de 50 MB en la interfaz, la tecla Escape del diálogo y la etiqueta de estado por estado de la iniciativa.

## Entorno y desviaciones

- Fecha: 2026-10-15. Aplicación con MySQL local en Docker (credenciales temporales), proveedor `Fake`, usuarios temporales, Chrome con Chrome DevTools MCP. Sin capturas en el repositorio, por decisión del usuario.
- La revisión de S5 con Gemini real se registró aparte en `docs/gemini-evaluacion.md` («Resultados de contexto adjunto»).
- Desviaciones del diseño original, registradas en `tasks.md` («Reconciliation notes»): la lista se ordena del más antiguo al más reciente (R7); el borrado de un archivo cuyo almacenamiento falla elimina igualmente la fila (R10); `Storage:MaxFileSizeMb` en blanco vale 10 (R24, añadida tras la verificación independiente).

## Cobertura de escenarios

Prueba = clase de xUnit (`Application.Tests/Context`, `Infrastructure.Tests`, `Web.Tests`). «Chrome S3/S4» = la tabla de arriba. «Pendiente» = no se ejercitó y queda en `docs/mejoras-posteriores.md`.

| Escenario | Etiqueta | Prueba o evidencia |
|---|---|---|
| Kind-specific fields | UNIT | `AttachmentTests` |
| Table, prefix and no cross-module access | UNIT | `ApplicationDbContextModelTests`, `ModuleBoundaryTests` |
| Nothing requires context | UNIT | `DraftContextTests`, `AssistantContextTests`, pruebas de handlers sin adjuntos |
| Valid file is stored | UNIT | `ContextHandlerTests > A_valid_file_is_stored_under_its_key_and_listed` |
| Zero-byte file is rejected | UNIT | `ContextValidatorTests`, `ContextHandlerTests > A_real_zero_byte_stream...` |
| File over the per-file cap is rejected | UNIT | `ContextValidatorTests`, `ContextHandlerTests > A_stream_longer_than_announced...` |
| Blocklisted extensions are rejected | UNIT | `ContextValidatorTests > Every_blocked_extension_fails` |
| Item and total caps | UNIT | `ContextHandlerTests > The_twenty_first_item...`, `The_total_cap...` |
| Path parts are dropped | UNIT | `ContextHandlerTests > Path_parts_of_the_name_are_dropped` |
| Failed insert removes the stored file | UNIT | `ContextHandlerTests > A_failed_insert_removes_the_stored_file_and_rethrows` |
| Another user's initiative | UNIT | `ContextHandlerTests > Another_users_or_a_deleted_or_an_unknown_initiative...` |
| Valid links / Rejected links | UNIT | `ContextValidatorTests`, `AttachmentTests` |
| Link label is the host | UNIT | `ContextHandlerTests > The_view_lists_the_items...` |
| Limits (notas) | UNIT | `ContextValidatorTests`, `AttachmentTests` |
| Any status | UNIT | Alta: `ContextHandlerTests > Context_can_be_added_in_any_status...`; baja: `ContextAccessTests > Context_can_be_deleted_in_any_status...` |
| Oldest first | UNIT | `ContextHandlerTests > The_view_lists_the_items...` |
| Foreign initiative on every operation | UNIT | `ContextHandlerTests`, `ContextAccessTests` |
| Attachment of another initiative | UNIT | `ContextAccessTests > Everything_that_is_not_an_own_downloadable_file_gives_null` |
| Soft-deleted initiative | UNIT | `ContextHandlerTests`, `ContextAccessTests` |
| Hostile content type and name | UNIT, UI | `AttachmentFileNameTests`, `AttachmentTests`, `ContextEndpointsTests` (tipo constante). UI: pendiente (descarga con nombre hostil) |
| Name sanitising table | UNIT | `AttachmentFileNameTests` |
| Uniform 404 | UNIT, UI | `ContextAccessTests`, `ContextEndpointsTests` (404 con `nosniff` y `no-store`). UI: Chrome S3 (id al azar y URL de otra persona); pendiente: iniciativa eliminada, enlace y cabeceras del 404 |
| Key shape / Traversal rejected / Root confinement | UNIT | `AttachmentTests`, `LocalFileStorageTests` (incluida la raíz con barra final) |
| Round trip / Delete is idempotent | UNIT | `LocalFileStorageTests` |
| Default provider / S3 and unknown values fail / Invalid max size | UNIT | `StorageOptionsTests`, `StorageRegistrationTests` |
| File then row / Storage failure still removes the row / Missing file / Link and note rows / Foreign delete | UNIT | `ContextAccessTests` |
| Distinctive values do not appear | UNIT | `ContextAccessTests > No_log_entry_contains...` |
| Maximum size is explicit | UNIT, UI | Dato: `ContextHandlerTests`; `OpenReadStream(limits.MaxFileBytes)` en `ContextCard.razor`; UI: Chrome S3 (11 MB rechazado) |
| Only text-like items are included / Caps and order / Invalid text is replaced / Counts-only logs | UNIT | `ContextExcerptTests` |
| No ids or URLs / Delimiter injection / No context leaves the request unchanged / Context stays out of the system instruction | UNIT | `GeminiAssistantServiceTests`, `GeminiArtifactDrafterTests` |
| Fake ignores context | UNIT | Pruebas del asistente y del generador falsos |
| Architecture and Epics carry no context | UNIT | `DraftContextTests`, `GeminiArtifactDrafterTests` |
| Initiatives module does not depend on Context | UNIT | `ModuleBoundaryTests` |
| SPEC-AS (8 escenarios) | UNIT | `AssistantContextTests`, `GeminiAssistantServiceTests`, `GeminiArtifactDrafterTests` |
| No edit operation exists / CSS-only views | DOC | `Features/Context` solo tiene altas, baja, lista, descarga y extractos; `rg "style=|<script"` sin resultados |
| Ephemeral disk documented | DOC | README, sección «Archivos adjuntos» |
| Headers on success | UI | Chrome S3 (200, `application/octet-stream`, `attachment` con `filename*`, `nosniff`, `no-store`); automatizado en `ContextEndpointsTests` |
| Original name on download | UI | Pendiente: Chrome S3 registra la descarga, no el nombre guardado |
| Link opens safely / Plain text rendering | UI | Chrome S4 |
| Logs in a real session | UI | Chrome S3 y S4 |
| Upload with feedback | UI | Chrome S3 (fila, tamaño, «Descargar», línea verde); pendiente: estado de carga |
| Over-cap, empty and blocked files | UI | Chrome S3 |
| Limit reached | UI | Segunda pasada: 20 elementos (deshabilitado con motivo y rehabilitado al borrar). Pendiente: 50 MB |
| Link and note forms / Delete confirmation | UI | Chrome S4 |
| Contexto is a live card | UI | Chrome S3 |
| Context in any status (UI) | UI | Pendiente (etiqueta de estado en Borrador, Planificando y Lista para construir) |
| An embedded instruction is ignored | MANUAL | Parcial: dos turnos en `docs/gemini-evaluacion.md`; sin borrador con contexto (pendiente) |
| Regresión de Fake con contexto (5.6) | UI | Pendiente |

## No verificado

Lo marcado «pendiente» arriba; además, el borrador de Resumen o PRD con Gemini real y con contexto, y que el ejemplo de un turno cite con claridad un dato de la nota.

## Segunda pasada (2026-10-15, tras la primera verificación)

Rama de cierre, proveedor falso, usuario `otro@example.com`, la misma iniciativa Estándar.

- **404 por la tubería real**: un identificador al azar respondió 404 con `X-Content-Type-Options: nosniff` y `Cache-Control: no-cache, no-store, max-age=0` (la página de «no encontrado» conserva `no-store`).
- **Más de 5 archivos**: al seleccionar 6 a la vez apareció «Seleccione como máximo 5 archivos a la vez.» y no se subió ninguno (el contador siguió en 4).
- **Nombre con tildes, paréntesis y almohadilla** («Presentación v2 (final)#1.txt»): se guardó y se lista con ese nombre; la descarga respondió 200 con `Content-Disposition: attachment; filename="Presentaci_n v2 (final)#1.txt"; filename*=UTF-8''Presentaci%C3%B3n%20v2%20%28final%29#1.txt`.
- **Tope de 20 elementos**: tras añadir notas hasta «20 de 20 elementos», «Añadir enlace», «Añadir nota» y la selección de archivos quedaron deshabilitados con «Se alcanzó el máximo de 20 elementos. No se pueden añadir más.»; al eliminar uno volvieron a habilitarse («19 de 20 elementos»).
- **Estado de la iniciativa**: se mantuvo «Aclarando» durante todas las altas y bajas. No se repitió en Planificando ni en Lista para construir en esta pasada (cubierto por pruebas unitarias).
- **Registros**: sin títulos, nombres ni textos de las notas; ningún error.

Sigue sin ejercitarse en el navegador: el 404 de una iniciativa borrada o de un identificador de enlace, el estado de carga durante la subida, la tecla Escape del diálogo, el tope de 50 MB, la etiqueta de estado en Planificando y Lista para construir, el nombre que guarda el navegador al descargar, la regresión con el proveedor falso de una conversación con contexto, y los borradores y documentos posteriores con contexto en vivo.

**Brechas aceptadas de forma explícita (decisión del orquestador, 2026-10-15, con el criterio del usuario de «decidir con lo recomendado»):** la verificación independiente dejó cinco escenarios parciales (nombre hostil en el navegador, nombre guardado por el navegador, estado de carga de la subida, el contexto en cualquier estado de la iniciativa en la interfaz, y la instrucción incrustada en un borrador con contexto en vivo). Todos tienen cobertura unitaria o evidencia parcial y se registran como seguimiento en `docs/mejoras-posteriores.md`; no bloquean el cierre.
