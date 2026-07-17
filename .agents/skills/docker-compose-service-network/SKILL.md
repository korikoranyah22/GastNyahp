---
name: docker-compose-service-network
description: Diseñar o extender el docker-compose de una app simple (frontend + backend + Postgres) — red interna, healthchecks con depends_on condition, Dockerfiles multi-stage, y el proxy nginx del frontend hacia el backend (REST + WebSocket). Usar al agregar un servicio nuevo al stack o al conectar frontend/backend a través de la red de contenedores.
---

# docker-compose-service-network

Arquitectura de referencia para un stack simple de 3 piezas: **Postgres + backend (API) + frontend (React
servido por nginx)**, todo en una red Docker interna. Extensible a un cuarto servicio (el servidor MCP, ver
[[mcp-tool-server]]) que comparte la misma base de datos.

En este repo (GastNyahp) la raíz del proyecto (`gastnyahp/`, donde vive `docker-compose.yml`) contiene el frontend
existente en `app/` (React + Vite + Zustand, ver [[react-component-patterns]]) y el backend nuevo en
`backend/` (solución .NET, ver estructura de proyectos en [[csharp-conventions-and-patterns]]). Los ejemplos de
abajo usan esos paths reales, no `frontend/`/`src/` genéricos.

## Principio: red interna, host expuesto solo donde hace falta

- Postgres NO publica puerto al host (`expose`, no `ports`) — solo el backend le habla, vía el hostname del
  servicio (`postgres:5432`). Si necesitás inspeccionarlo desde el host, usá
  `docker compose exec postgres psql -U <user> -d <db>` o un `docker-compose.override.yml` local (gitignored)
  que agregue `ports: ["127.0.0.1:5432:5432"]` — nunca lo publiques por default.
- El backend publica su puerto (`5050:5050`) porque el frontend en dev standalone (`npm run dev`, fuera de
  Docker) necesita pegarle directo; en producción dentro de Docker, el frontend igual le habla por hostname
  interno (`backend:5050`), no por el puerto publicado.
- El frontend publica `3000:80` — es el único punto de entrada pensado para el usuario final.

```yaml
services:
  postgres:
    image: postgres:16-alpine
    restart: unless-stopped
    environment:
      POSTGRES_DB:       "${POSTGRES_DB:-gastnyahpdb}"
      POSTGRES_USER:     "${POSTGRES_USER:-gastnyahp}"
      POSTGRES_PASSWORD: "${POSTGRES_PASSWORD:-change-me-in-prod}"
    expose: ["5432"]
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5

  backend:
    build:
      context: .
      dockerfile: Dockerfile.backend
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    ports: ["5050:5050"]
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Projections: "Host=postgres;Port=5432;Database=${POSTGRES_DB:-gastnyahpdb};Username=${POSTGRES_USER:-gastnyahp};Password=${POSTGRES_PASSWORD:-change-me-in-prod}"
    healthcheck:
      test: ["CMD-SHELL", "curl -fsS http://localhost:5050/health/live || exit 1"]
      interval: 10s
      timeout: 5s
      start_period: 30s
      retries: 5

  frontend:
    build:
      context: .
      dockerfile: Dockerfile.frontend
    restart: unless-stopped
    depends_on:
      backend:
        condition: service_healthy
    ports: ["3000:80"]

volumes:
  postgres_data:
```

## Reglas del patrón

1. **`depends_on` con `condition: service_healthy`**, no solo `service_started` — así el frontend no arranca
   antes de que el backend responda `/health`, y el backend no arranca antes de que Postgres acepte
   conexiones. Todo servicio con un healthcheck propio debe ser depended-on por condición, no por orden ciego.
2. **`healthcheck` obligatorio en cada servicio con estado o con un endpoint HTTP** — Postgres usa `pg_isready`;
   el backend, un endpoint liviano tipo `/health/live` que NO toque la base de datos (así el healthcheck no
   falla en cascada si Postgres está lento pero el proceso sigue vivo).
   ⚠️ Si la imagen base es slim/alpine, confirmá que el binario que usás en el `test` (`curl`, `wget`) esté
   instalado en el Dockerfile — un healthcheck que falla porque falta el binario deja el `depends_on` de otros
   servicios colgado para siempre.
3. **Variables de entorno con default inline** (`${POSTGRES_PASSWORD:-change-me-in-prod}`) — el compose
   funciona sin `.env` en dev, pero cualquier password real se pasa por `.env` (gitignored), nunca hardcodeada
   en el yml versionado.
4. **Un volumen nombrado por dato que debe sobrevivir un rebuild** (`postgres_data`). Datos generados por la
   app que el usuario quiere poder inspeccionar desde el host (uploads, imágenes) van como bind mount a una
   carpeta del repo (`./data/...:/app/data/...`), no como volumen anónimo.
5. **`docker-compose.override.yml` para overrides locales/gitignored** — Docker Compose lo auto-carga cuando
   corrés `docker compose up` sin `-f` explícito. Usalo para necesidades de la máquina local (montar un socket,
   exponer un puerto extra) sin tocar el `docker-compose.yml` versionado. Si necesitás un segundo archivo de
   override versionado (ej. para un modo especial), cargalo con `include:` desde el override local, no lo
   mezcles en el principal.

## Multi-stage Dockerfile — backend (.NET)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copiar SOLO los .csproj primero — cachea la capa de `restore` mientras no
# cambien las dependencias, aunque cambie el código fuente.
COPY backend/GastNyahp.sln ./
COPY backend/src/GastNyahp.Domain/GastNyahp.Domain.csproj         ./src/GastNyahp.Domain/
COPY backend/src/GastNyahp.Infrastructure/GastNyahp.Infrastructure.csproj ./src/GastNyahp.Infrastructure/
COPY backend/src/GastNyahp.Api/GastNyahp.Api.csproj               ./src/GastNyahp.Api/
RUN dotnet restore src/GastNyahp.Api/GastNyahp.Api.csproj

COPY backend/. .
RUN dotnet publish src/GastNyahp.Api/GastNyahp.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# curl para el healthcheck del compose — la imagen slim NO lo trae por default.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN useradd --no-create-home --shell /bin/false appuser
COPY --from=build /app/publish ./
USER appuser
EXPOSE 5050
ENTRYPOINT ["dotnet", "GastNyahp.Api.dll"]
```

## Multi-stage Dockerfile — frontend (React + nginx)

```dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY app/package*.json ./
RUN npm ci
COPY app/ ./
RUN npm run build

FROM nginx:stable-alpine AS serve
RUN rm /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
COPY app/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

(`context: .` en el compose apunta a la raíz del repo `gastnyahp/`, por eso el Dockerfile de frontend copia desde
`app/` — el nombre real de la carpeta del frontend en este repo, no `frontend/`.)

## nginx.conf — el frontend proxea `/api` y los hubs de realtime al backend

```nginx
map $http_upgrade $connection_upgrade { default upgrade; '' close; }

server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    location /api/ {
        proxy_pass         http://backend:5050;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 120s;   # subir solo si un endpoint concreto lo necesita — preferí async+polling
        proxy_send_timeout 120s;
        client_max_body_size 10m;
    }

    # WebSocket (ej. SignalR) — upgrade explícito + timeout largo porque la
    # conexión queda abierta, no es un request-response corto.
    location /chatHub {
        proxy_pass         http://backend:5050;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection $connection_upgrade;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }

    location / {
        try_files $uri $uri/ /index.html;  # SPA fallback: todas las rutas de React Router resuelven a index.html
    }
}
```

⚠️ No definas dos `location` que compartan el mismo prefijo con y sin barra final apuntando a servicios
distintos (nginx puede redirigir 301 entre ellos y romper CORS) — un solo `location /api/` que cubra todo el
árbol de rutas del backend es más seguro que fragmentar por sub-recurso.

## Procedimiento para agregar un servicio nuevo al stack

1. ¿Necesita persistir datos? Agregá un volumen nombrado o un bind mount explícito.
2. ¿Otro servicio depende de que esté listo? Agregale un `healthcheck` y hacé que el dependiente use
   `depends_on: condition: service_healthy`.
3. ¿Necesita estar accesible desde el host (además de la red interna)? Publicá el puerto SOLO si es realmente
   necesario — todo lo demás usa `expose` y se habla por hostname de servicio.
4. Si el nuevo servicio es standalone (como el caso de [[mcp-tool-server]]), documentá en un comentario del
   compose qué archivos/carpetas hay que copiar si algún día se extrae a su propio `docker-compose.yml`.

## Verificación

- `docker compose up -d` levanta los 3(+) servicios sin reinicios en loop.
- `docker compose ps` muestra todos los healthchecks en `healthy` antes de que el frontend/backend arranquen
  (confirmalo mirando el orden de logs con `docker compose logs -f`).
- El frontend en el browser puede pegarle a `/api/...` y ver la respuesta del backend (confirma el proxy).

## Anti-patrones

- ❌ Publicar el puerto de Postgres al host por default — riesgo de colisión con un Postgres nativo y
  superficie de ataque innecesaria.
- ❌ `depends_on` sin `condition` (solo espera que el proceso arranque, no que esté listo) cuando el servicio
  tiene un healthcheck disponible.
- ❌ Healthcheck que depende de un binario no instalado en la imagen — cuelga el `depends_on` de otros
  servicios para siempre.
- ❌ Hardcodear passwords reales en el `docker-compose.yml` versionado — siempre `${VAR:-default-de-dev}` +
  `.env` gitignored.
- ❌ Subir `proxy_read_timeout` a un valor enorme "por las dudas" en vez de convertir la operación lenta a
  async + polling/push (ver [[aspnet-rest-endpoint]]).
- ❌ Copiar todo el contexto de build ANTES del `dotnet restore`/`npm ci` — invalida el cache de esa capa en
  cada cambio de código, aunque no haya cambiado ninguna dependencia.
