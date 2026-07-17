# Deploy en Railway

Railway **no corre `docker-compose.yml`**: cada servicio del compose se vuelve un servicio Railway aparte. Esta
guía arma esos tres servicios a mano una sola vez. El compose se mantiene intacto y sigue siendo la forma de
correr todo en local — no se toca nada de lo que ya usás.

## Qué cambió en el repo para que esto funcione

- [`app/nginx.conf.template`](../app/nginx.conf.template) — la config de nginx quedó parametrizada por variables de
  entorno (`PORT`, `BACKEND_HOST`, `BACKEND_PORT`). Con los defaults del Dockerfile el resultado es **idéntico**
  al de antes en local; Railway sobreescribe esas variables.
- [`Dockerfile.frontend`](../Dockerfile.frontend) — usa el mecanismo de plantillas de la imagen de nginx (envsubst
  al arrancar) y trae los defaults `PORT=80`, `BACKEND_HOST=backend`, `BACKEND_PORT=5050`.

El backend **no necesitó cambios**: el puerto y la connection string ya salen de configuración/variables.

---

## Paso 0 — Repositorio git

Railway construye desde un repo de GitHub, y este proyecto todavía no es un repo. Lo más simple es que el repo
sea **la carpeta `gastnyahp/`** (es autocontenida: acá están el compose y los dos Dockerfiles).

```bash
cd gastnyahp
git init
git add .
git commit -m "GastNyahp: stack docker-compose + deploy Railway"
# crear el repo en GitHub y:
git remote add origin https://github.com/korikoranyah22/gastapp.git
git push -u origin main
```

El `.env` ya está en `.gitignore`, así que los secretos **no** se suben. Los vas a recrear como variables de
Railway (paso 2 y 3).

> **Si preferís un repo en la carpeta de arriba** (`gastnyahp-docker-backend`, junto a `angelnairav2_public`): sirve
> igual, pero en cada servicio de Railway tenés que setear **Root Directory = `gastnyahp`**. El resto es idéntico.

---

## Paso 1 — Postgres administrado

En el proyecto de Railway: **New → Database → Add PostgreSQL**. Reemplaza al servicio `postgres` del compose; no
uses una imagen de Postgres a mano. Anotá el nombre del servicio (por defecto **`Postgres`**): lo usás en la
connection string de abajo.

---

## Paso 2 — Servicio backend

**New → GitHub Repo →** elegí el repo. Después, en el servicio:

**Settings**
- **Root Directory**: `/` (o `gastnyahp` si el repo es la carpeta de arriba — ver Paso 0).
- **Networking**: *no* generes dominio público. El backend queda interno; sale al mundo sólo por el frontend.

**Variables** (equivalen al bloque `environment:` del backend en el compose, con dos ajustes marcados 👇):

```
ASPNETCORE_ENVIRONMENT   = Production
ASPNETCORE_URLS          = http://+:5050          # 👈 escucha en todas las interfaces (IPv4+IPv6 de la red privada)
Database__Provider       = Postgres
EventStore__Provider     = Postgres
EventStore__Schema       = eventuous
Admin__ApiKey            = <tu-clave-de-admin>     # el GASTNYAHP_ADMIN_KEY del .env (OJO: acá se llama Admin__ApiKey)
Admin__AllowKeyAsCode    = true                   # opcional — ver "Crear la primera familia"
BusinessDay__Enabled     = true
BusinessDay__OpenTime    = 06:00
BusinessDay__TimeZone    = America/Argentina/Buenos_Aires
OAuth__Issuer            = https://TU-DOMINIO-PUBLICO-DEL-FRONTEND
RAILWAY_DOCKERFILE_PATH  = Dockerfile.backend      # 👈 Railway sólo detecta un archivo llamado "Dockerfile" por defecto
ConnectionStrings__Projections = Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};SSL Mode=Disable
```

Notas:
- `${{Postgres.PGHOST}}` es una **variable de referencia** de Railway: apunta al servicio Postgres del Paso 1. Si
  tu servicio Postgres se llama distinto, cambiá `Postgres` por ese nombre. Confirmá los nombres exactos de las
  variables en la pestaña *Variables* del servicio Postgres.
- Npgsql **no parsea** el `DATABASE_URL` en formato URL que da Railway; por eso se arma la string con `Host=…`.
- La conexión va por la red privada, así que `SSL Mode=Disable` está bien. Si el primer deploy falla por SSL,
  probá `SSL Mode=Require;Trust Server Certificate=true`.
- OAuth__Issuer debe ser el origen público HTTPS del frontend, sin barra final. ChatGPT usa ese origen para descubrir y completar OAuth; nginx reenvía /.well-known, /oauth y /mcp al backend privado.
- Las migraciones se aplican **solas** al arrancar y reintentan si Postgres todavía no está listo, así que el
  orden de arranque no importa (no hace falta `depends_on`).

---

## Paso 3 — Servicio frontend

**New → GitHub Repo →** el **mismo** repo, segundo servicio.

**Settings**
- **Root Directory**: igual que el backend (`/` o `gastnyahp`).
- **Networking → Generate Domain**: acá **sí** — este es el único servicio público. Railway le inyecta `PORT` y
  nginx escucha ahí (no seteés `PORT` a mano).

**Variables**:

```
RAILWAY_DOCKERFILE_PATH = Dockerfile.frontend
BACKEND_HOST            = ${{gastnyahp-backend.RAILWAY_PRIVATE_DOMAIN}}   # ver nota
BACKEND_PORT            = 5050
```

**`BACKEND_HOST` tiene que ser el dominio privado EXACTO del servicio backend.** El dominio interno es
`<nombre-del-servicio>.railway.internal` — si tu backend se llama `gastnyahp-backend`, es
`gastnyahp-backend.railway.internal`, **no** `gastnyahp.railway.internal`. Lo más seguro es no adivinar y usar la
variable de referencia `${{<nombre-del-backend>.RAILWAY_PRIVATE_DOMAIN}}` (reemplazá por el nombre real del
servicio), que Railway completa con el valor correcto. Como el frontend proxea `/api` y `/mcp` al backend por la
red interna, la app sigue hablándole a su propio origen (`/api`) — cero CORS, igual que en local.

---

## Paso 4 — Primer arranque

1. Deployá el backend primero (o los dos; el reintento de conexión cubre el desfasaje).
2. Cuando ambos estén verdes, abrí el dominio del frontend.
3. Para crear la primera familia hace falta un "código de administrador". Dos caminos:
   - **Atajo (recomendado para self-hosted):** poné `Admin__AllowKeyAsCode=true` en el backend y escribí el valor
     de `Admin__ApiKey` **directo** en el campo "código del administrador" del form. Es reusable. Cuidado: con el
     flag activo, esa llave pasa a ser también una contraseña de "crear familia" — mantenela secreta.
   - **Estricto (código de un solo uso):** dejá el flag apagado y generá un código con
     `POST /api/admin/invites` + header `X-Admin-Key: <tu-Admin__ApiKey>`; escribí **ese** código en el form.

> ⚠️ **`Admin__ApiKey`, con doble guión bajo.** En local el `.env` usa `GASTNYAHP_ADMIN_KEY` y el docker-compose lo
> traduce; en Railway **no hay traducción**, el backend lee literalmente `Admin__ApiKey`. Si lo ponés como
> `GASTNYAHP_ADMIN_KEY`, el endpoint de admin responde `503` y el atajo no funciona.

---

## Verificación local (que nada se rompió)

Con Docker corriendo, desde `gastnyahp/`:

```bash
docker compose build frontend   # el template de nginx se resuelve con los defaults
docker compose up -d
```

Debe quedar idéntico a siempre: frontend en http://localhost:3001, backend interno en 5050.

---

## Troubleshooting (gotchas ya pisados)

**Railpack en vez de Docker / "could not determine how to build".** Dos causas, casi siempre juntas: (a) Railway
está construyendo una rama que no tiene los Dockerfiles —revisá que el servicio apunte a la rama correcta en
*Settings → Source → Branch*, o hacé que esa sea la default del repo—; y (b) falta `RAILWAY_DOCKERFILE_PATH`
(los Dockerfiles no se llaman `Dockerfile` a secas, así que sin la variable Railway cae a Railpack).

**`... could not be resolved` o `upstream timed out` en los logs del frontend.** nginx no llega al backend. En
orden de probabilidad:
- *`BACKEND_HOST` mal.* nginx loguea qué host intentó resolver. Tiene que ser el dominio privado EXACTO del
  backend (`gastnyahp-backend.railway.internal`, no `gastnyahp.railway.internal`). Ver la nota del Paso 3.
- *El backend escucha en otro puerto.* La imagen `aspnet:10.0` por defecto usa 8080. Confirmá que el backend
  tenga `ASPNETCORE_URLS=http://+:5050` (en sus logs, Kestrel imprime `Now listening on: http://[::]:5050`).
- *nginx cacheó una IP vieja del backend.* nginx resuelve `BACKEND_HOST` **una vez al arrancar**; si redeployás
  el backend, su IP privada cambia y el proxy queda con la vieja → **redeployá el frontend** para que re-resuelva.
  Por eso conviene deployar el backend primero y el frontend después.

> Si ese trade-off de redeploy molesta, la salida limpia es servir el SPA desde el propio backend (un solo
> servicio, sin DNS entre servicios). No está hecho acá, pero elimina toda esta clase de problemas.

**El backend arranca pero no conecta a Postgres.** Revisá `ConnectionStrings__Projections`: Npgsql no parsea el
`DATABASE_URL` en formato URL — tiene que ser la string `Host=…;Port=…;…`. Las migraciones se aplican solas y
reintentan, así que un desfasaje de arranque se recupera; un fallo persistente es la connection string.
