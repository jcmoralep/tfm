# Evidencia del paso 4 (artefactos): recorrido en Chrome

Fecha: 2026-10-11. Código: `main` tras fusionar el PR #7. Datos: solo de ejemplo. Entorno: app local contra el MySQL del proyecto, dos usuarios de prueba (`polish@example.com` y `ajena@example.com`).

## Flujo A: acceso e iniciativa nueva (escritorio)

| # | Captura | Qué demuestra |
|---|---|---|
| 01 | `01-login.png` | Pantalla de acceso. |
| 02 | `02-login-error.png` | Credenciales incorrectas: aviso en rojo; asteriscos rojos en los campos obligatorios. |
| 03 | `03-lista-iniciativas.png` | Lista de iniciativas. |
| 04 | `04-nueva-iniciativa-validacion.png` | "El nombre es obligatorio." |
| 05 | `05-borrador-guardado-verde.png` | Aviso de guardado en verde. |
| 06 | `06-profundidad-automatico.png` | Paso 2: modo Automático. |
| 07 | `07-revision.png` | Paso 3: revisión. |
| 08 | `08-detalle-iniciativa.png` | Detalle de la iniciativa recién creada. |

## Flujo B: asistente en modo Automático

| # | Captura | Qué demuestra |
|---|---|---|
| 09 | `09-asistente-primera-pregunta.png` | Primera pregunta con ejemplo y motivo; aviso de modo de demostración. |
| 10 | `10-asistente-cargando.png` | "Enviando…" y puntos animados con "El asistente está escribiendo…". |
| 11 | `11-pregunta-tamano-respuestas-rapidas.png` | Pregunta de tamaño con respuestas rápidas. |
| 12 | `12-propuesta-de-nivel.png` | Propuesta del nivel Estándar con "Sí, usar el nivel Estándar" y "Elegir otro nivel". |
| 13 | `13-nivel-aceptado-confirmacion.png` | Nivel aceptado, progreso "5 de 8 temas cubiertos", panel lateral. |
| 14 | `14-confirmacion-y-enlace-al-brief.png` | Confirmación de paso a Planificar y enlace "Ir al Brief". |

## Flujo C: documentos (Brief y PRD)

| # | Captura | Qué demuestra |
|---|---|---|
| 15 | `15-tarjeta-artefactos-brief-disponible.png` | Tarjeta Artefactos: Brief disponible, PRD aún no. |
| 16 | `16-generando-documento.png` | Indicador "Generando el documento…". |
| 17 | `17-brief-generado.png` | Brief generado: aviso verde, Borrador, versión 1. |
| 18 | `18-modo-edicion.png` | Modo de edición con el Markdown. |
| 19 | `19-guardado-con-aviso-de-formato.png` | Guardado en verde y aviso ámbar de formato que no bloquea. |
| 20 | `20-confirmar-regenerar.png` | Confirmación de regenerar (pierde los cambios manuales). |
| 21 | `21-confirmar-aprobar.png` | Confirmación de aprobar con los avisos de formato. |
| 22 | `22-brief-aprobado.png` | Brief aprobado. |
| 23 | `23-cierre-enlace-a-documentos.png` | Cierre de la conversación con "Ir a los documentos". |
| 24 | `24-prd-generado.png` | PRD generado. |
| 25 | `25-aprobar-prd-completa-el-conjunto.png` | Aprobar el último documento avisa que la iniciativa quedará Lista para construir. |
| 26 | `26-iniciativa-lista-para-construir.png` | Iniciativa "Lista para construir" (insignia verde). |

## Flujo D: propiedad y móvil

| # | Captura | Qué demuestra |
|---|---|---|
| 27 | `27-usuario-ajeno-no-existe.png` | Otro usuario abre el documento ajeno: "La iniciativa no existe." |
| 28 | `28-movil-lista-vacia-otro-usuario.png` | Lista de otro usuario en móvil (390 px). |
| 29 | `29-movil-login.png` | Login en móvil. |
| 30 | `30-movil-detalle.png` | Detalle en móvil. |
| 31 | `31-movil-documento-prd.png` | Documento PRD en móvil. |
| 32 | `32-movil-asistente.png` | Asistente en móvil. |

## Comprobaciones sin imagen

- Sin desbordamiento horizontal en móvil (detalle, documento, asistente): `scrollWidth` igual al ancho de la ventana.
- Consola del navegador: solo mensajes informativos de Blazor, sin errores.
- Registros de la app: 0 errores; avisos esperados (puerto HTTPS, validación de nombre vacío, conflictos de versión de pruebas anteriores).
- Lighthouse (`lighthouse-prd/` escritorio, `lighthouse-lista-movil/` móvil): Accesibilidad 100, Buenas prácticas 100, SEO 90, Navegación con agentes 100. Único fallo: falta `meta description`.

## Hallazgos del recorrido

1. **Enlace "Ir al Brief" parece una respuesta rápida** (captura 14): usa el mismo estilo de píldora que las respuestas, aunque es navegación.
2. **Avisos de formato citan "Línea N"** (captura 19), pero la vista de lectura no muestra números de línea.
3. **Modo "Manual" tras aceptar el nivel sugerido** (captura 26): iniciativa creada en Automático aparece como Manual. Es la decisión RF-47 pendiente del paso 3.
4. **Falta `meta description`** (Lighthouse SEO 90): arreglo trivial.
5. **Título repetido** (captura 17): la página muestra "Brief" y luego el título del documento "Brief: …".
