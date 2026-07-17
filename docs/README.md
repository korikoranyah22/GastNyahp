# Documentación de GastNyahp

Este directorio es el punto único de entrada para la documentación humana del proyecto.

## Documentos vigentes

| Documento | Alcance | Autoridad |
| --- | --- | --- |
| [Especificación funcional](FUNCTIONAL_SPEC.md) | Visión, módulos, flujos y UX | Referencia funcional; sus secciones históricas se interpretan junto al modelo actual |
| [Modelo de dominio](DOMAIN_MODEL.md) | Aggregates, eventos, invariantes, familias, MCP y borradores | Fuente principal del backend |
| [Cuentas y login](ACCOUNTS_AND_LOGIN.md) | Email, contraseña, sesiones y recuperación | Reemplaza las decisiones antiguas de acceso |
| [Despliegue en Railway](DEPLOY_RAILWAY.md) | PostgreSQL, backend, frontend, variables y diagnóstico | Guía operativa |

## Orden de precedencia

1. Código y pruebas automatizadas actuales.
2. `ACCOUNTS_AND_LOGIN.md` para autenticación y sesiones.
3. `DOMAIN_MODEL.md` para reglas e invariantes del backend.
4. `FUNCTIONAL_SPEC.md` para intención funcional y UX.

## Planes históricos

Se conservan como registro de decisiones, no como trabajo pendiente vigente:

- [Plan histórico de gastos diarios](HISTORICAL_DAILY_EXPENSES_PLAN.md)
- [Roadmap histórico](HISTORICAL_ROADMAP.md)

## Documentación operativa junto al código

Los `SKILL.md` bajo `backend/src/GastNyahp.Api/SkillPackages/` forman parte del paquete descargable de la API.
Los archivos bajo `.claude/skills/` son instrucciones internas de desarrollo. Su ubicación tiene significado
operativo y por eso no se trasladan a este directorio.
