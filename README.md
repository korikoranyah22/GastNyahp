# GastNyahp

GastNyahp es una aplicación familiar para registrar gastos diarios, tickets, compras en cuotas, tarjetas,
bancos, préstamos, servicios, reservas y presupuestos. Incluye una API .NET, un frontend React, persistencia en
PostgreSQL y un servidor MCP embebido para operar la misma información mediante agentes.

## Inicio rápido

Requisitos: Docker con Compose.

1. Copiá `.env.example` como `.env` y completá las credenciales locales.
2. Levantá el stack con `docker compose up --build`.
3. Abrí la interfaz en <http://localhost:3001>.

El backend queda disponible en <http://localhost:5055>. La base de datos permanece dentro de la red de Compose.

## Documentación

La documentación del proyecto está centralizada en [`docs/`](docs/README.md):

- [Especificación funcional y UX](docs/FUNCTIONAL_SPEC.md)
- [Modelo de dominio](docs/DOMAIN_MODEL.md)
- [Cuentas, sesiones y recuperación](docs/ACCOUNTS_AND_LOGIN.md)
- [Despliegue en Railway](docs/DEPLOY_RAILWAY.md)
- [Planes históricos](docs/README.md#planes-históricos)

## Seguridad

Las credenciales reales pertenecen exclusivamente a `.env` o al proveedor de despliegue. `.env` está ignorado
y `.env.example` contiene solamente nombres y valores de ejemplo. No publiques claves de administrador, tokens
de familia, claves MCP ni contraseñas de PostgreSQL.

## Skills para agentes

Las skills descargables se empaquetan desde `backend/src/GastNyahp.Api/SkillPackages/gastnyahp/`. Son artefactos
operativos de la API y por eso permanecen junto al código en vez de trasladarse a `docs/`.
