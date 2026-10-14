# Evaluación manual con personas (paso 5, Gemini real)

Guion para probar a mano el asistente y los documentos con Gemini (`Assistant:Provider=Gemini`, RF-68 a RF-72). No se ejecuta en CI ni en ninguna prueba. Use **solo datos de ejemplo** (RN-05) y no pegue la clave en ninguna parte de este documento.

## Preparación

1. Levante MySQL con `docker compose` y credenciales temporales, aplique migraciones y arranque la aplicación con el usuario semilla (AGENTS §5, punto 4).
2. Configure la clave por user-secrets o `Gemini__ApiKey` y `Assistant__Provider=Gemini`.
3. Cree una iniciativa por persona. Anote la fecha y el modelo (`Gemini:Model`).
4. Al terminar, revise `Logs/`: ni el texto escrito, ni los documentos, ni la clave deben aparecer (RNF-02).

## Comprobaciones comunes (en cada turno y cada documento)

- El ejemplo ("Por ejemplo") está hecho a la medida de la iniciativa y la menciona.
- Trato de «usted», sin elogios («¡Excelente respuesta!»), sin jerga (spine, épica, invariantes, slug).
- Una sola pregunta por turno, con "Por qué lo pregunto" y respuestas rápidas que incluyen "No sé".
- Insiste solo ante respuestas flojas y como máximo una vez por tema; nunca ante "No sé" ni respuestas rápidas. La segunda respuesta se acepta sea cual sea.
- El avance baja una pregunta mientras hay un seguimiento pendiente y se recupera al responderlo.
- **Deshacer** la respuesta original quita también su seguimiento; recargar no añade mensajes ni llamadas.
- Los documentos no inventan: toda cifra o dato no dicho aparece como `[ASSUMPTION: A-n]` o `Q-n`; cero avisos de formato.
- El PRD (Estándar) es coherente con el Brief aprobado: las suposiciones del Brief siguen siendo suposiciones y sus preguntas siguen abiertas.

## Persona 1: dueña de producto precisa

Perfil: sabe lo que quiere y da datos concretos. Se espera **sin insistencia** y documentos ricos.

Qué escribir (nivel Estándar):

- Idea: «Quiero que el equipo comercial pueda generar desde el portal una cotización en PDF con el logotipo del cliente, en lugar de armarla a mano en Excel.»
- Usuarios: «Unos 25 ejecutivos comerciales; la aprueba la jefa de ventas regional.»
- Problema: «Hoy cada cotización tarda unos 40 minutos y a veces sale con precios desactualizados.»
- Éxito: «Que el 80 % de las cotizaciones salgan en menos de 10 minutos, medido en el portal, y que los errores de precio bajen a menos de 2 por mes.»
- Fuera de alcance: «Facturación y descuentos especiales por volumen.»

Qué comprobar: ninguna insistencia (las respuestas son suficientes); ejemplos que mencionan cotizaciones; el Brief y el PRD usan las cifras dichas como hechos (`answer`) y marcan como suposición lo que no dijo (por ejemplo el sistema de precios); el PRD coincide con el Brief aprobado.

## Persona 2: junior vaga

Perfil: respuestas escasas. Se espera **una insistencia** por tema flojo y suposiciones en el documento.

Qué escribir (nivel Pequeña):

- Idea: «mejorar el reporte de ventas, está feo»
- Después del seguimiento, responda también de forma escasa: «que se vea mejor y más rápido»
- Usuarios: «el equipo»
- Problema: «no se entiende»
- Capacidades: «ver las ventas»
- Fuera de alcance: «no sé qué más»  (use el botón o escríbalo)
- Éxito: «que los usuarios estén contentos»

Qué comprobar: exactamente un seguimiento sobre la idea; la segunda respuesta, aunque sea floja, se acepta; el avance baja una pregunta mientras el seguimiento está pendiente y se recupera; «que los usuarios estén contentos» recibe el seguimiento si la fase lo permite (la última pregunta de cada fase no recibe insistencia, RF-69; anótelo si no la recibe); la especificación marca como suposición o `Q-n` lo que no se dijo y no inventa métricas; cero avisos de formato; el nivel no se cambia («nunca manda a la persona a otro nivel», RF-42). Deshaga la primera respuesta y compruebe que el seguimiento desaparece con ella.

## Persona 3: responde «No sé» a menudo

Perfil: desconoce muchos datos. Se espera **cero presión** y preguntas pendientes en el documento.

Qué escribir (nivel Estándar), alternando botón y frase escrita:

- Idea: «Una forma de que las sucursales pidan insumos sin llamar por teléfono.»
- Usuarios: **No sé** (botón)
- Problema: «Se pierden pedidos.» y luego, si el asistente insiste, «no sé cuántos»
- Éxito: **No sé** (frase escrita: «no sé»)
- Fuera de alcance: **No sé**
- Pruebe también un mensaje más largo que contenga la frase («no sé si son 10 o 20 sucursales, creo que 12») y compruebe que se trata como respuesta normal.

Qué comprobar: nunca hay insistencia ante «No sé»; el asistente no presiona ni repite la pregunta; el siguiente turno reconoce sin elogiar; cada «No sé» queda como `Q-n` en el documento aunque el modelo hubiera propuesto otra cosa; el panel «Preguntas pendientes» permite asignar responsable y condición; aprobar exige asignarlas (RF-62).

## Fallos provocados (una vez, con cualquier persona)

- Modelo equivocado en `Gemini:Model`: la respuesta escrita queda en el historial, aparece el mensaje de configuración y el botón **Reintentar**; enviar otra respuesta queda bloqueado; con el modelo corregido, Reintentar produce exactamente una respuesta. Al generar un documento con el modelo equivocado, el mensaje aparece junto al botón, nada se guarda y un reintento posterior funciona.
- Si se logra provocar una cuota agotada (429), anote el texto mostrado.

## Tokens y cuota

Anote por turno y por borrador `promptTokenCount`, `candidatesTokenCount` y `thoughtsTokenCount` (de los registros, sin contenido) y compárelos con `docs/gemini-sonda.md`. Anote el cupo diario visible en AI Studio.

## Tabla de resultados

| Fecha | Modelo | Persona | Comprobación | Hallazgo | Resultado (OK / falla) |
|---|---|---|---|---|---|
|  |  | 1. Dueña precisa |  |  |  |
|  |  | 1. Dueña precisa |  |  |  |
|  |  | 2. Junior vaga |  |  |  |
|  |  | 2. Junior vaga |  |  |  |
|  |  | 3. «No sé» frecuente |  |  |  |
|  |  | 3. «No sé» frecuente |  |  |  |
|  |  | Fallos provocados |  |  |  |
|  |  | Tokens y cuota |  |  |  |

## Corrida del 2026-10-13: una iniciativa Estándar con Gemini real

Una sola iniciativa, mezclando las tres personas (respuesta precisa, respuestas escuetas y «No sé»), con `gemini-3.6-flash`, nivel de pensamiento `low` y la capa gratuita. Datos de ejemplo; no se corrieron las tres personas completas para cuidar la cuota gratuita.

| Comprobación | Hallazgo | Resultado |
|---|---|---|
| Apertura y primer turno (idea precisa) | 2,0 s; pregunta y ejemplo adaptados a la idea («asesores de ventas… coordinadores de atención»), en español y de usted, sin elogios | OK |
| Insistencia ante una respuesta escueta («gente de ventas») | Una sola insistencia pidiendo precisar el rol; el avance bajó de 2 a 1 pregunta mientras estaba pendiente (7,6 s) | OK |
| Tope de una insistencia («vendedores») | No volvió a insistir; avanzó al tema siguiente (2,0 s) | OK |
| «No sé» | Nunca insiste; el código reconoce con «No pasa nada: lo dejo anotado como pregunta pendiente y seguimos» y el tema queda como `Q-n` | OK |
| Confirmación y cierre | Sin llamada al modelo, respuesta inmediata (0,5 a 0,9 s); avance 8 de 8 | OK |
| Brief con Gemini | 3,5 a 4 s; fiel a las respuestas; lo no respondido (diferenciador y criterios de éxito) quedó como `Q-1` y `Q-2`; una suposición marcada (visión); sin avisos de formato | OK |
| PRD con Gemini partiendo del Brief aprobado | 8,6 s en el segundo intento (el primero devolvió 503 por alta demanda, reintento automático también 503: «El asistente tardó demasiado… no se generó el documento», sin guardar nada); hereda las preguntas del Brief con numeración nueva y sin responsables; marca como suposición los no usuarios, el glosario y el recorrido | OK |
| Límite de uso (429) | Al sexto turno en unos 45 s el servicio devolvió 429 (se registró como Quota): el mensaje quedó guardado, apareció «El asistente alcanzó su límite de uso por ahora…» y **Reintentar** recuperó la conversación en 2 s al pasar un minuto | OK |
| Registros sin contenido | Ninguna palabra de las respuestas ni rastro de la clave en el registro de la app ni en `Logs/`; solo tema, modelo, estado, duración, `finishReason` y fichas | OK |
| Calidad del PRD | Los requisitos no funcionales recogieron frases de prioridad («la búsqueda por número debe ser la prioritaria») y la respuesta de restricciones (celular, mismas credenciales) quedó en «Alcance, dentro» como «Prioridad:»: el modelo mezcla los temas de restricciones y prioridades | **Falla leve: ajustar el prompt o la descripción de las secciones en un cambio posterior** |
| Registro de llamadas | Las líneas dicen `for initiative null`: el asistente y los borradores no pasan el id de la iniciativa al cliente | **Falla leve: pasar el id para poder correlacionar** |

### Segunda corrida: Pequeña, y el tope diario de la capa gratuita

Una iniciativa Pequeña, con respuestas precisas, usando `gemini-3.6-flash` y después `gemini-3.1-flash-lite` (cambiando solo `Gemini__Model`).

| Comprobación | Hallazgo | Resultado |
|---|---|---|
| Conversación Pequeña (6 preguntas) | Cada turno trae pregunta, ejemplo adaptado y motivo, en español y de usted; avance «N de 6 preguntas respondidas»; cierre y confirmación sin llamada al modelo | OK |
| **Cuota diaria** | Después de unas 30 llamadas en el día, `gemini-3.6-flash` devolvió 429 de forma sostenida. La respuesta de Google nombra la cuota `GenerateRequestsPerDayPerProjectPerModel-FreeTier` y un `retryDelay` de unas 21 horas: es un **tope diario por proyecto y por modelo**, no por minuto. Antes de agotarse hubo 429 sueltos (a los 45 s y a los 3 y 5 turnos), que se recuperaban esperando; con el tope diario «Reintentar» no sirve hasta que se reinicie la cuota | Hallazgo clave |
| Otro modelo | `gemini-3.1-flash-lite` sigue respondiendo (1,3 s) porque cada modelo tiene su propia cuota; con él terminó la conversación | OK |
| Spec con `gemini-3.1-flash-lite` | 10,6 s; las cinco secciones, `FR-1` a `FR-4`, `SM-1` y `SM-2`, una suposición y una pregunta abierta, sin avisos de formato | OK |
| Calidad del Spec | La restricción «solo las salas de esta sede» (dicha en «fuera de alcance») apareció además como suposición en Restricciones; y el modelo añadió una pregunta abierta propia (plazo para ver la reducción de conflictos) que nadie respondió como «No sé» | **Falla leve: el modelo duplica o añade elementos** |
| Registros sin contenido (ambas corridas) | Ninguna palabra de las respuestas ni rastro de la clave en el registro de la app ni en `Logs/` | OK |
| Arranque sin clave | Con `Assistant:Provider=Gemini` y la clave vacía la aplicación sale con código 1 y el mensaje nombra `Gemini__ApiKey`, sin ningún valor | OK |

Consecuencia para el producto: con la capa gratuita el tope diario por modelo es muy bajo (un Estándar usa unas 12 llamadas). Opciones ya previstas: otro modelo con otra cuota (`Gemini:Model`), modelos distintos para turnos y borradores (anotado en `docs/mejoras-posteriores.md`) o un plan de pago.

Fichas por llamada (de los registros): turnos de 976 a 1.482 de entrada y 77 a 120 de salida, con pensamiento 210 solo en uno de seis; Brief 1.127 de entrada y 603 de salida. Cuota: en la capa gratuita el modelo `gemini-3.6-flash` aceptó cinco peticiones en unos 45 s y devolvió 429 a la sexta; un usuario que escribe con calma (más de 12 s por respuesta) queda por debajo. No se vieron los límites diarios (solo se muestran en AI Studio). Pendiente: medir los niveles de pensamiento `medium` y `high`, que no se probaron.

## Arquitectura con Gemini (architecture-epics S5, 2026-10-14)

- Iniciativa Grande con el PRD aprobado del recorrido de prueba. `gemini-3.6-flash` respondió 503 dos veces seguidas (saturación, 8,6 s y 4,5 s): la aplicación mostró «El asistente tardó demasiado en responder…», no guardó nada y el documento anterior quedó intacto.
- Con `Gemini__Model=gemini-3.1-flash-lite`: estado 200, 5,1 s, `finishReason` STOP, tokens: prompt 1276, respuesta 420, razonamiento 1315.
- Resultado: dos decisiones (AD-1, AD-2) con referencias válidas a FR/NFR del PRD, mapa de capacidades cubierto, ninguna tecnología ni versión nombrada, la pila en Diferido, cinco preguntas para desarrollo ya asignadas, cero avisos de formato. `Logs/` sin contenido.

## Épicas e historias con Gemini (architecture-epics S6, 2026-10-14)

- Iniciativa Grande con PRD y Arquitectura aprobados. Modelo `gemini-3.1-flash-lite`: estado 200, 2,4 s, `finishReason` STOP, tokens: prompt 2242, respuesta 554.
- Resultado: dos épicas y dos historias numeradas por el código, `Cubre:` con referencias válidas, los dos requisitos funcionales cubiertos (mapa de cobertura completo), inventario con los números del PRD, cinco preguntas para desarrollo ya asignadas, cero avisos de formato. Aprobar quedó habilitado, y con los tres documentos aprobados la iniciativa pasó a «Lista para construir»; las tres descargas respondieron 200.
- `Logs/` sin contenido de la iniciativa. El modelo por defecto (`gemini-3.6-flash`) no se probó en esta ocasión por su saturación del mismo día.
