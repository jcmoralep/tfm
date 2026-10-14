# Sonda de Gemini `generateContent` (cambio `gemini-assistant`, tarea 1b.1)

Registro de la sonda manual contra la API real. No se ejecuta en CI ni en ninguna prueba. No contiene la clave ni datos reales: solo frases sintéticas.

- Fecha: 2026-10-13 (hora del servidor 14 oct 01:05-01:12 UTC).
- Modelo: `gemini-3.8-flash`. Endpoint `POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`, cabecera `x-goog-api-key`.
- Peticiones enviadas: 8 en total (límite acordado). Una de ellas, la primera, salió con una clave mal extraída por un fallo del script y devolvió 400 `API_KEY_INVALID`; no cuenta como medición.

## Resultados

| # | Prueba | Resultado |
|---|---|---|
| 1 | Forma de salida estructurada primaria: `generationConfig.responseMimeType: "application/json"` + `responseJsonSchema` (con `propertyOrdering`) | **Aceptada** (200). No hizo falta probar `responseFormat` ni `responseSchema`; el diseño no cambia |
| 2 | Un solo contenido `user` con una transcripción delimitada por etiquetas, más `systemInstruction` aparte | **Aceptado** (200) |
| 3 | `generationConfig.thinkingConfig.thinkingLevel: "low"` (camelCase, minúsculas) | **Aceptado** (200). `medium` viajó en las peticiones de borrador, que no llegaron a completarse (503 y tiempo agotado), así que su aceptación no está comprobada; `high` no se probó. Los valores low/medium/high de la configuración se mantienen |
| 4 | Turno pequeño: 125 tokens de entrada, 117 de salida (`candidatesTokenCount`), 242 en total; `thoughtsTokenCount` **ausente** (con `low` no se informa cuando es 0, por lo que el DTO debe aceptar null) | Latencia **38,1 s** (el servidor informó `Server-Timing dur=37832`) |
| 5 | Borrador de tamaño de documento (8 secciones, `medium`) | **No medido.** 503 a los 10,6 s, 503 a los 2,0 s y, en la tercera petición, sin respuesta tras 90 s (cliente cortó) |
| 6 | Cabeceras de límite de tasa | **Ninguna.** Solo `X-Gemini-Service-Tier: standard` y `Server-Timing`. No hay `Retry-After` ni `X-RateLimit-*` |
| 7 | Límites de la capa gratuita | Solo se ven en AI Studio, no en la API: revisar allí el cupo diario antes de la evaluación en vivo |

### Lectura de la latencia

Durante la sonda el modelo respondió 503 `UNAVAILABLE` ("high demand") en 3 de 5 intentos válidos y tardó 38 s en el único 200. Es una muestra de un momento de alta demanda, no una medida estable. Con los plazos por defecto de entonces (turno 30 s, borrador 90 s; hoy son 60 y 120 s) ese 200 habría terminado en Timeout. Se **conservan** 30 s y 90 s (son configurables con `Gemini:TurnTimeoutSeconds` y `Gemini:DraftTimeoutSeconds`) y queda pendiente medir de nuevo en la evaluación en vivo (tarea 5.6) antes de decidir si se suben.

### Comparación de modelos y decisión (2026-10-13)

Medición posterior, un turno pequeño por modelo, mismos parámetros:

| Modelo | Latencia de un turno |
|---|---|
| `gemini-3.8-flash` | 58 s |
| `gemini-3.5-flash-lite` | 72 s |
| `gemini-3.6-flash` | 2,0 s |
| `gemini-3.1-flash-lite` | 1,3 s |

Decisión del usuario (2026-10-13): el modelo por defecto es `gemini-3.6-flash` y los plazos por defecto suben a 60 s por turno y 120 s por documento (`Gemini:TurnTimeoutSeconds`, `Gemini:DraftTimeoutSeconds`). Las cifras de arriba (38 s con `gemini-3.8-flash`) fueron una muestra en un momento de alta demanda; el modelo es configuración (`Gemini:Model`).

## Forma de la respuesta 2xx (campos que se mapean)

```json
{
  "candidates": [
    {
      "content": { "parts": [ { "text": "{...JSON...}", "thoughtSignature": "..." } ], "role": "model" },
      "finishReason": "STOP",
      "index": 0
    }
  ],
  "usageMetadata": {
    "promptTokenCount": 125,
    "candidatesTokenCount": 117,
    "totalTokenCount": 242,
    "promptTokensDetails": [ { "modality": "TEXT", "tokenCount": 125 } ],
    "serviceTier": "standard"
  },
  "modelVersion": "gemini-3.8-flash",
  "responseId": "..."
}
```

- Texto: `candidates[0].content.parts[0].text` (JSON como cadena). La parte puede traer `thoughtSignature` (miembro desconocido, se ignora).
- `promptFeedback` no apareció en el 200 correcto; el cliente lo trata como opcional.
- `thoughtsTokenCount` puede faltar.

## Formas de error (cuerpo no se lee en producción; solo se registra el estado)

| Provocado con | Estado | Cuerpo (resumen) |
|---|---|---|
| Modelo inexistente | 404 | `error.code 404`, `status NOT_FOUND`, "models/... is not found for API version v1beta..." |
| Esquema mal formado (`type: "banana"`) | 400 | `error.code 400`, `status INVALID_ARGUMENT`, "unrecognized type 'banana' at top-level" |
| Clave no válida | 400 | `status INVALID_ARGUMENT`, `details[].reason API_KEY_INVALID`. **Es 400, no 401/403**: el cliente lo clasifica como Model, cuyo texto al usuario es el mismo que el de Auth (configuración) |
| Alta demanda | 503 | `error.code 503`, `status UNAVAILABLE`, "This model is currently experiencing high demand..." |

No se intentó provocar un 429.

## Consecuencias para el cliente

- DTOs con la forma primaria del diseño; sin cambios respecto de la tabla de alternativas.
- `UsageMetadata` con campos opcionales; `Part` ignora miembros desconocidos.
- El reintento único ante 500/503/504 está justificado: los 503 son reales.
