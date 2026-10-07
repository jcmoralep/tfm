# Propuesta de alcance del MVP — Plataforma BMAD para producto

Documento de trabajo, versión 2 (incorpora las decisiones del 5 de octubre de 2026). Los puntos marcados como **supuesto** requieren confirmación.

## 1. Contexto

- **BMAD** (método de desarrollo asistido por IA, de código abierto) cubre "todo el esfuerzo, no solo el código": qué construir, cómo encaja y cómo cambia con lo que se aprende. Sus artefactos clásicos siguen existiendo: briefs y especificaciones, PRD, arquitectura, épicas e historias.
- El equipo de desarrollo de la empresa ya usa **Gentle AI**. El área de producto aún no usa IA.
- Hoy los insumos que inician un desarrollo son, a veces, un video y prototipos hechos con IA; las historias de usuario casi no se usan.
- La plataforma es el **puente**: producto aclara y especifica con ayuda del asistente; desarrollo recibe artefactos listos.
- Fuentes: https://docs.bmad-method.org/ y https://github.com/bmad-code-org/BMAD-METHOD

## 2. Objetivo

Permitir que personas de producto **no técnicas** lleven una iniciativa desde una idea hasta dejarla **lista para que desarrollo la construya**, guiadas por un asistente conversacional que aplica BMAD.

| Fase BMAD | En la plataforma |
|---|---|
| Aclarar | Sí. Conversación guiada que produce el Brief. |
| Planificar | Sí. El asistente convierte el Brief en PRD y demás artefactos. |
| Construir y verificar | No. Ocurre en las herramientas de desarrollo (Gentle AI), con los artefactos exportados. |
| Aprender y ajustar | Fuera del MVP. Posible fase 2. |

## 3. Flujo del usuario

1. Crea una **iniciativa** (nombre y descripción corta) y **elige cómo se define la profundidad**: la define el propio usuario, o la sugiere el asistente.
2. Opcionalmente adjunta **contexto** (video, prototipo, enlaces, documentos, notas).
3. Entra al **asistente guiado**:
   - Idea vaga: aclara con preguntas y genera el **Brief**.
   - Idea clara y grande: pasa a planificar y genera el **PRD**, y luego Arquitectura y Épicas/historias como borradores.
   - Cambio pequeño: especificación corta, sin planificación completa (**supuesto**).
   - Estilo de aclaración según BMAD: volcado libre de la idea, y luego **modo rápido** (la IA redacta con supuestos y pregunta solo lo que falta) o **modo guiado** (sección por sección).
4. El usuario revisa, pide ajustes y aprueba cada artefacto.
5. La iniciativa queda **Lista para construir** y se **exportan** los artefactos: `.md` para desarrolladores y `.pdf` para el usuario.

La especificación sigue la estructura de cinco partes de BMAD: por qué, capacidades, restricciones, fuera de alcance y señal de éxito (validar nombres exactos de secciones en un repositorio de prueba).

## 4. Módulos del MVP

### 4.1 Iniciativas
- Crear, listar, ver y editar.
- Estados: *Aclarando*, *Planificando*, *Lista para construir*.
- Detalle con tres secciones: Conversación, Artefactos y Contexto.

### 4.2 Asistente guiado
- Conversación (burbujas, respuestas rápidas) con recorrido visible por las fases.
- Profundidad: definida por el usuario o sugerida por el asistente, según lo elegido al crear la iniciativa.
- Usa el contexto adjunto cuando el modelo pueda interpretarlo.
- Historial guardado por iniciativa.
- Detrás de `IAssistantService` (Gemini, capa gratuita; solo datos de ejemplo mientras tanto).

### 4.3 Artefactos
- **Brief, PRD, Arquitectura y Épicas e historias** (todos los que BMAD contempla).
- Arquitectura y Épicas/historias: **borradores para revisión de desarrollo**.
- Edición por el usuario y regeneración con ayuda del asistente.
- Versionado simple (número de versión; sin comparación entre versiones).
- Aprobación manual antes de marcar como final.
- Exportación: **Markdown** (fuente única de verdad, para developers) y **PDF** (para el usuario, generado desde el Markdown).

### 4.4 Contexto adjunto
- Opcional y de **cualquier tipo de contenido** (archivos y enlaces); los tipos que proponga el usuario son prioritarios.
- Almacenamiento detrás de `IFileStorage` (local o S3, por configuración).
- Solo los tipos que el modelo sabe interpretar se envían al asistente; el resto se guarda como referencia (**supuesto**).
- Analizar o transcribir videos con IA queda para una segunda versión.

### 4.5 Acceso
- Login básico (correo y contraseña), modular y extensible. Sin roles ni pantallas de configuración.

## 5. Fuera del MVP

- Fase "Aprender y ajustar".
- Roles y permisos, administración, configuración, SSO.
- Comentarios entre usuarios, notificaciones, integración directa con herramientas de desarrollo.
- Comparación visual entre versiones de un artefacto.
- Otros formatos de exportación (Word, etc.).
- Catálogo de agentes BMAD (los usuarios de producto no invocan agentes por nombre).

## 6. Modelo de datos inicial (borrador)

- **Usuario:** tablas de Identity.
- **Iniciativa:** id, nombre, descripción, modo de profundidad (automático o manual), profundidad, estado, creada por, fechas.
- **Mensaje:** id, iniciativa, autor (usuario o asistente), contenido, fecha.
- **Artefacto:** id, iniciativa, tipo (Brief, PRD, Arquitectura, Épicas e historias), versión, contenido en Markdown, aprobado, fechas.
- **Adjunto:** id, iniciativa, tipo o enlace, nombre original, tipo de contenido, tamaño, clave de almacenamiento.

## 7. Orden de construcción

1. **Base:** solución con las cuatro capas, MySQL, Serilog, inicio de sesión básico y estilo visual (Intercom).
2. **Iniciativas:** crear, listar y detalle, con el selector de profundidad.
3. **Asistente guiado** con implementación falsa de `IAssistantService` y conversación persistida.
4. **Artefactos:** Brief y PRD con edición y aprobación.
5. **Gemini** real conectado al asistente.
6. **Arquitectura y Épicas e historias** como borradores para revisión.
7. **Contexto adjunto** con almacenamiento local.
8. **Exportación:** Markdown y PDF.
9. Pulido y prueba con datos de ejemplo.

Cada paso entrega una funcionalidad completa, de interfaz a base de datos.

## 8. Criterio de éxito

Un product owner lleva una idea hasta una especificación aprobada **sin ayuda técnica**, y desarrollo la recibe y construye **sin pedir aclaraciones**.

## 9. Decisiones pendientes

1. Librería para generar el PDF (licencia vigente y compatibilidad con el servidor).
2. Estructura exacta de los artefactos y nombres de archivo/carpeta que BMAD espera (validar probando BMAD en un repositorio de prueba).
3. Cómo revisa desarrollo la Arquitectura y las historias: ¿dentro de la plataforma o fuera?
4. Qué tipos de contenido del adjunto puede interpretar Gemini.
5. Almacenamiento en producción: local o S3.
6. Versión de .NET.
