# Desplegar GastNyahp en Railway

Esta es la única guía vigente. Seguí los pasos en orden y no mezcles configuraciones anteriores.

## Resultado esperado

El proyecto de Railway tendrá exactamente tres servicios:

| Nombre exacto | Tipo | Directorio raíz | Puerto | Público |
|---|---|---|---:|---|
| `Postgres` | PostgreSQL administrado | — | Railway | No |
| `gastnyahp-backend` | Repo GitHub | `/backend` | `5050` | No |
| `gastnyahp-frontend` | Repo GitHub | `/app` | `80` | Sí |

Los nombres importan porque las variables de referencia los usan literalmente.

## Antes de empezar

1. Confirmá que todos los cambios estén en la rama `main` de GitHub.
2. En Railway, cada servicio debe apuntar a `main`.
3. Eliminá de ambos servicios cualquier configuración histórica:
   - variable `RAILWAY_DOCKERFILE_PATH`;
   - Build Command personalizado;
   - Start Command personalizado;
   - Root Directory distinto del indicado en esta guía;
   - Config File distinto del indicado en esta guía.
4. No publiques el backend ni PostgreSQL. El único dominio público pertenece al frontend.

Cada servicio tiene una sola configuración:

```text
app/Dockerfile
app/railway.json
app/railway.env.example

backend/Dockerfile
backend/railway.json
backend/railway.env.example
```

No existen Dockerfiles alternativos en la raíz del repositorio.

## Paso 1: crear PostgreSQL

En el canvas del proyecto:

1. Seleccioná **New → Database → Add PostgreSQL**.
2. Renombrá el servicio exactamente a `Postgres`.
3. No generes un dominio público.
4. Verificá que su pestaña **Variables** contenga `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER` y `PGPASSWORD`.

No copies manualmente las credenciales: el backend las consumirá mediante referencias de Railway.

## Paso 2: crear el backend

Creá un servicio desde el repositorio de GitHub.

### Settings

Configurá exactamente:

```text
Service name:   gastnyahp-backend
Branch:         main
Root Directory: /backend
Config File:    /backend/railway.json
```

No generes un dominio público. No configures Build Command ni Start Command.

Railway debe detectar `backend/Dockerfile`. En el build correcto aparecen líneas como:

```text
load build definition from Dockerfile
COPY src/GastNyahp.Api/GastNyahp.Api.csproj
```

Si aparece `Dockerfile.backend` o `COPY backend/src/...`, Railway está usando una configuración antigua.

### Variables obligatorias

Abrí **Variables → RAW Editor** y cargá estas variables:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
PORT=5050
ASPNETCORE_HTTP_PORTS=5050
ASPNETCORE_URLS=http://[::]:5050
Database__Provider=Postgres
EventStore__Provider=Postgres
EventStore__Schema=eventuous
ConnectionStrings__Projections=Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};SSL Mode=Disable
Admin__ApiKey=REEMPLAZAR_POR_UN_SECRETO_ALEATORIO_DE_64_CARACTERES
Admin__AllowKeyAsCode=true
BusinessDay__Enabled=true
BusinessDay__OpenTime=06:00
BusinessDay__TimeZone=America/Argentina/Buenos_Aires
```

Las tres variables de puerto son obligatorias en Railway, aunque el Dockerfile también tenga valores por defecto:

```dotenv
PORT=5050
ASPNETCORE_HTTP_PORTS=5050
ASPNETCORE_URLS=http://[::]:5050
```

Sin las tres, la API puede iniciar en `5050` mientras el healthcheck intenta consultar `8080`, dejando el deploy
atascado aunque los logs digan `Application started`.

### Clave administrativa

Antes de desplegar, reemplazá el placeholder de `Admin__ApiKey` por una cadena aleatoria de al menos 64 caracteres.
No uses una frase memorable, no reutilices una contraseña y no guardes el valor en Git.

Después de crear la variable:

1. Abrí su menú de tres puntos.
2. Elegí **Seal**.
3. Guardá una copia en tu gestor de contraseñas.

Con `Admin__AllowKeyAsCode=true`, esa clave se puede escribir directamente en el campo “Código del administrador”
para crear una familia. Por eso debe tratarse como una contraseña.

### Desplegar y verificar el backend

Desplegá el backend antes que el frontend. El deploy correcto muestra:

```text
Database schema initialized
Now listening on: http://[::]:5050
Application started
```

El healthcheck `/health/live` debe quedar verde. No hace falta un dominio público para comprobarlo: Railway ejecuta
el healthcheck dentro de su red.

## Paso 3: crear el frontend

Creá un segundo servicio usando el mismo repositorio.

### Settings

Configurá exactamente:

```text
Service name:   gastnyahp-frontend
Branch:         main
Root Directory: /app
Config File:    /app/railway.json
```

No configures Build Command ni Start Command.

Railway debe detectar `app/Dockerfile`. El arranque correcto incluye:

```text
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
```

Si los logs muestran intentos de abrir `/usr/share/nginx/html/api/...`, Railway está usando un servidor estático
sin nuestra configuración nginx.

### Variables obligatorias

En **Variables → RAW Editor** cargá:

```dotenv
PORT=80
BACKEND_HOST=${{gastnyahp-backend.RAILWAY_PRIVATE_DOMAIN}}
BACKEND_PORT=5050
```

`BACKEND_HOST` debe ser una referencia al dominio privado del backend. No escribas el dominio público del frontend
ni inventes el hostname.

### Dominio público

En **Settings → Networking → Public Networking**:

1. Generá un dominio.
2. Configurá **Target Port = `80`**.
3. No agregues TCP Proxy.

Cuando Railway genere el dominio, la referencia usada por `OAuth__Issuer` se resolverá automáticamente. Si el
backend ya estaba desplegado antes de crear el dominio:

1. Volvé a **Variables** del backend.
2. Agregá:

```dotenv
OAuth__Issuer=https://${{gastnyahp-frontend.RAILWAY_PUBLIC_DOMAIN}}
```

3. Redeployá primero el backend.
4. Cuando el backend quede healthy, desplegá o redeployá el frontend.

## Paso 4: pruebas finales

Usá el dominio público del frontend.

### 1. Portada

```text
https://TU-DOMINIO/
```

Debe cargar la aplicación. Esto sólo prueba que nginx sirve archivos estáticos.

### 2. Healthcheck completo

```text
https://TU-DOMINIO/health/live
```

Debe responder:

```json
{"status":"ok"}
```

Esta prueba confirma frontend nginx → red privada Railway → backend.

### 3. API

Abrí:

```text
https://TU-DOMINIO/api/families
```

Puede responder `401`, `400` o datos según la autenticación, pero nunca debe devolver una página HTML `404 Not
Found` de nginx.

### 4. Crear la primera familia

En “Crear familia”, usá como código del administrador el valor real de `Admin__ApiKey`.

## Orden correcto para futuros cambios

Cuando cambie solamente el backend:

1. Esperá que el backend quede healthy.
2. Redeployá el frontend para que nginx vuelva a resolver la IP privada del backend.

Cuando cambie solamente el frontend, desplegá el frontend normalmente.

## Diagnóstico rápido

### El backend compila pero el healthcheck dice “service unavailable”

En Deploy Logs, confirmá:

```text
Now listening on: http://[::]:5050
```

Luego verificá que existan exactamente:

```dotenv
PORT=5050
ASPNETCORE_HTTP_PORTS=5050
ASPNETCORE_URLS=http://[::]:5050
```

No confíes solamente en los defaults del Dockerfile.

### El frontend carga, pero `/api` o `/health/live` devuelve nginx 404

La portada puede funcionar aunque el proxy no exista. Revisá:

```text
Root Directory: /app
Config File: /app/railway.json
```

Eliminá `RAILWAY_DOCKERFILE_PATH`, Build Command y Start Command. Desplegá el último commit, no reutilices un
deployment antiguo.

### El build intenta copiar `app/nginx.conf.template`

Está mezclando el Dockerfile antiguo con Root Directory `/app`. El Dockerfile correcto, dentro de `/app`, copia:

```dockerfile
COPY nginx.conf.template /etc/nginx/templates/nginx.conf.template
```

### El build intenta copiar `backend/src/...`

Está mezclando el Dockerfile antiguo con Root Directory `/backend`. El Dockerfile correcto copia:

```dockerfile
COPY src/ ./src/
```

### nginx devuelve `host not found in upstream`

Confirmá:

```dotenv
BACKEND_HOST=${{gastnyahp-backend.RAILWAY_PRIVATE_DOMAIN}}
BACKEND_PORT=5050
```

El nombre `gastnyahp-backend` debe coincidir exactamente con el servicio.

### El backend no conecta a PostgreSQL

1. Confirmá que el servicio se llame exactamente `Postgres`.
2. Revisá que `ConnectionStrings__Projections` use las cinco referencias `Postgres.PG...`.
3. No uses `DATABASE_URL`: Npgsql espera la cadena `Host=...;Port=...`.
4. Si Railway exige TLS, reemplazá `SSL Mode=Disable` por:

```text
SSL Mode=Require;Trust Server Certificate=true
```

### La clave administrativa responde 503

La variable debe llamarse exactamente:

```text
Admin__ApiKey
```

No `GASTNYAHP_ADMIN_KEY`, no `Admin_ApiKey` y no `Admin:ApiKey`.

## Verificación local

Desde la raíz del repositorio:

```powershell
docker compose build
docker compose up -d
docker compose ps
```

El frontend queda en `http://localhost:3001` y el backend en `http://localhost:5055`.
