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
