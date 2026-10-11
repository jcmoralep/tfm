# PRD: Consulta de pedidos

## 0 Propósito del documento

Este documento describe lo que se debe construir en la iniciativa «Consulta de pedidos» para que el equipo de desarrollo pueda planificarlo y construirlo.

Contexto: Consultar el estado de los pedidos sin llamar a bodega.

## 1 Visión

- Que el equipo de ventas consulte el estado de un pedido sin llamar a bodega.

## 2 Usuario objetivo

- Las vendedoras de las tiendas
- El personal de bodega

### 2.1 Trabajos por hacer

- Hoy cada consulta toma diez minutos por teléfono y los clientes se quejan de la demora.

### 2.2 No usuarios

- [ASSUMPTION: A-1] No se indicó qué personas no usarán la solución; se confirma con quien propone la iniciativa.

### 2.3 Recorridos clave

- [ASSUMPTION: A-2] Aún no se describió un recorrido concreto de uso; se completa con un caso real (una persona con nombre) al revisar el documento.

## 3 Glosario

- [ASSUMPTION: A-3] No se definieron términos propios de la iniciativa; se completa el glosario al revisar el documento con el equipo.

## 4 Funcionalidades

### Capacidades principales

Lo que una persona debe poder hacer, en palabras de quien propuso la iniciativa.

- **FR-1**: Buscar un pedido por su número
- **FR-2**: Ver en qué estado está
- **FR-3**: Recibir un aviso cuando cambie

### Requisitos no funcionales

Límites, reglas y condiciones de calidad que debe cumplir la solución.

- **NFR-1**: Debe funcionar desde el celular
- **NFR-2**: No puede mostrar datos de otros clientes
- Pendiente: ver Q-1.

### Integraciones

Sistemas y equipos con los que debe coordinarse la solución.

- **FR-4**: Debe integrarse con El sistema de inventario
- **FR-5**: Debe integrarse con El equipo de logística

## 5 No objetivos

- No incluye pagos ni devoluciones.

## 6 Alcance del MVP

### 6.1 Dentro

- Todas las funcionalidades de la sección 4.
- **Prioridad**: Primero consultar el estado y, después, recibir avisos.

### 6.2 Fuera

- Lo indicado en No objetivos (sección 5).

## 7 Métricas de éxito

- **SM-1**: Que ninguna vendedora tenga que llamar a bodega
- **SM-2**: Que bajen las quejas por demoras

## 8 Preguntas abiertas

- **Q-1**: Falta saber las condiciones de velocidad o seguridad: «¿Necesita algo especial de velocidad o seguridad?»

## 9 Índice de suposiciones

- **A-1**: No se indicó qué personas no usarán la solución; se confirma con quien propone la iniciativa.
- **A-2**: Aún no se describió un recorrido concreto de uso; se completa con un caso real (una persona con nombre) al revisar el documento.
- **A-3**: No se definieron términos propios de la iniciativa; se completa el glosario al revisar el documento con el equipo.
