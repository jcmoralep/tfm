# Arquitectura: Consulta de pedidos

## Paradigma de diseño

- [ASSUMPTION: A-1] El paradigma de diseño lo propone el equipo de desarrollo al revisar este borrador.

## Invariantes heredadas

- Requisitos del PRD que esta arquitectura debe cumplir: FR-1, FR-2, FR-3, FR-4, FR-5, NFR-1, NFR-2.
- Lo que el PRD deja fuera sigue fuera.
- Las preguntas abiertas del PRD siguen vigentes.

## Invariantes y reglas

### AD-1 — Debe funcionar desde el celular

- **Vincula (binds):** NFR-1
- **Previene (prevents):** [ASSUMPTION: A-2] Que NFR-1 se incumpla sin que nadie lo note al construir.
- **Regla (rule):** [ASSUMPTION: A-3] Toda decisión de diseño se revisa contra NFR-1 antes de construir.

### AD-2 — No puede mostrar datos de otros clientes

- **Vincula (binds):** NFR-2
- **Previene (prevents):** [ASSUMPTION: A-4] Que NFR-2 se incumpla sin que nadie lo note al construir.
- **Regla (rule):** [ASSUMPTION: A-5] Toda decisión de diseño se revisa contra NFR-2 antes de construir.

## Mapa de capacidades

| Requisito | Qué pide | Decisiones |
| --- | --- | --- |
| FR-1 | Buscar un pedido por su número | Ninguna todavía |
| FR-2 | Ver en qué estado está | Ninguna todavía |
| FR-3 | Recibir un aviso cuando cambie | Ninguna todavía |
| FR-4 | Debe integrarse con El sistema de inventario | Ninguna todavía |
| FR-5 | Debe integrarse con El equipo de logística | Ninguna todavía |

## Diferido

- **Tecnología (pila):** Los lenguajes, marcos y versiones no se deciden en este borrador: esta plataforma no conoce la pila de la empresa y la decide el equipo de desarrollo (ver Q-1).
- **Esqueleto y convenciones:** La estructura del código y las convenciones de consistencia dependen de la pila elegida; el equipo de desarrollo las define después de decidirla.

## Preguntas para desarrollo

- **Q-1**: ¿Qué tecnología (lenguajes, marcos y versiones) usará la solución? (Responsable: Equipo de desarrollo; Revisar: antes de construir)
- **Q-2**: ¿Qué decisión de arquitectura cubre FR-1? (Responsable: Equipo de desarrollo; Revisar: antes de construir)
- **Q-3**: ¿Qué decisión de arquitectura cubre FR-2? (Responsable: Equipo de desarrollo; Revisar: antes de construir)
- **Q-4**: ¿Qué decisión de arquitectura cubre FR-3? (Responsable: Equipo de desarrollo; Revisar: antes de construir)
- **Q-5**: ¿Qué decisión de arquitectura cubre FR-4? (Responsable: Equipo de desarrollo; Revisar: antes de construir)
- **Q-6**: ¿Qué decisión de arquitectura cubre FR-5? (Responsable: Equipo de desarrollo; Revisar: antes de construir)

## Índice de suposiciones

- **A-1**: El paradigma de diseño lo propone el equipo de desarrollo al revisar este borrador.
- **A-2**: Que NFR-1 se incumpla sin que nadie lo note al construir.
- **A-3**: Toda decisión de diseño se revisa contra NFR-1 antes de construir.
- **A-4**: Que NFR-2 se incumpla sin que nadie lo note al construir.
- **A-5**: Toda decisión de diseño se revisa contra NFR-2 antes de construir.
