<#
.SYNOPSIS
    Levanta el stack completo de GastNyahp (Postgres + backend .NET + frontend nginx) y lo prueba end-to-end.

.DESCRIPTION
    - Genera .env con secretos aleatorios si no existe (password de Postgres + admin key).
    - docker compose up -d --build y espera a que el backend este healthy.
    - Corre un smoke test end-to-end contra el stack real: crea una familia de prueba (aislada - las
      familias no ven los datos de otras), registra un banco y un gasto, y verifica que se leen.
    - Emite un codigo de administrador fresco para que crees TU familia en el navegador.

.EXAMPLE
    .\run-stack.ps1              # levantar + smoke + codigo de admin
    .\run-stack.ps1 -NoSmoke     # levantar sin el smoke test
    .\run-stack.ps1 -Down        # bajar el stack (los datos persisten en el volumen)
#>
param(
    [switch]$Down,
    [switch]$NoSmoke
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Write-Step($msg)  { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "  OK $msg" -ForegroundColor Green }
function Write-Fail($msg)  { Write-Host "  X  $msg" -ForegroundColor Red }

# -- Bajar el stack -------------------------------------------------------------
if ($Down) {
    Write-Step "Bajando el stack (docker compose down)..."
    docker compose down
    Write-Ok "Stack detenido. Los datos persisten en el volumen 'postgres_data'."
    exit 0
}

# -- Prerrequisitos -------------------------------------------------------------
Write-Step "Verificando Docker..."
docker version *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Docker no esta disponible. Abri Docker Desktop y volve a ejecutar este script."
    exit 1
}
Write-Ok "Docker disponible."

# -- .env con secretos aleatorios -----------------------------------------------
function New-RandomSecret([int]$length) {
    $chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
    $bytes = New-Object byte[] $length
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    -join ($bytes | ForEach-Object { $chars[ $_ % $chars.Length ] })
}

if (-not (Test-Path .env)) {
    Write-Step "Generando .env con secretos aleatorios (primera vez)..."
    $pgPass   = New-RandomSecret 24
    $adminKey = New-RandomSecret 32
    @(
        "POSTGRES_DB=gastnyahpdb"
        "POSTGRES_USER=gastnyahp"
        "POSTGRES_PASSWORD=$pgPass"
        "GASTNYAHP_ADMIN_KEY=$adminKey"
        "ASPNETCORE_ENVIRONMENT=Production"
    ) | Out-File -FilePath .env -Encoding ascii
    Write-Ok ".env creado (gitignored)."
} else {
    Write-Ok ".env existente - se reutiliza."
}

$adminKey = (Select-String -Path .env -Pattern '^GASTNYAHP_ADMIN_KEY=(.+)$').Matches[0].Groups[1].Value
if ([string]::IsNullOrWhiteSpace($adminKey)) {
    Write-Fail "GASTNYAHP_ADMIN_KEY esta vacia en .env - completala y volve a ejecutar."
    exit 1
}

# -- Levantar el stack ----------------------------------------------------------
Write-Step "Levantando el stack (docker compose up -d --build)... la primera vez tarda unos minutos."
docker compose up -d --build
if ($LASTEXITCODE -ne 0) {
    Write-Fail "docker compose fallo. Revisa el output de arriba."
    exit 1
}

# -- Esperar el healthcheck del backend -----------------------------------------
Write-Step "Esperando a que el backend este healthy (hasta 120s)..."
$backend = 'http://localhost:5055'
$healthy = $false
for ($i = 0; $i -lt 60; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "$backend/health/live" -UseBasicParsing -TimeoutSec 3
        if ($r.StatusCode -eq 200) { $healthy = $true; break }
    } catch { }
    Start-Sleep -Seconds 2
}
if (-not $healthy) {
    Write-Fail "El backend no respondio a tiempo. Diagnostico: docker compose logs backend"
    exit 1
}
Write-Ok "Backend healthy en $backend."

# -- Smoke test end-to-end (familia de prueba, aislada de las reales) -----------
if (-not $NoSmoke) {
    Write-Step "Smoke test end-to-end contra el stack real..."
    try {
        # 1. Codigo de admin -> crear familia de prueba
        $code = (Invoke-RestMethod -Method Post -Uri "$backend/api/admin/invites" -Headers @{ 'X-Admin-Key' = $adminKey }).code
        $cred = Invoke-RestMethod -Method Post -Uri "$backend/api/families" -ContentType 'application/json' -Body (@{
            adminInviteCode = $code; familyName = "Smoke $(Get-Date -Format 'HHmmss')"; memberName = 'Robot'
        } | ConvertTo-Json)
        $auth = @{ Authorization = "Bearer $($cred.memberToken)" }
        Write-Ok "Familia de prueba creada (aislada: las familias reales no la ven)."

        # 2. Banco -> gasto -> verificacion de lectura (API -> dominio -> proyeccion -> Postgres -> API)
        $null = Invoke-RestMethod -Method Post -Uri "$backend/api/banks" -Headers $auth -ContentType 'application/json' -Body (@{
            name = 'Banco Smoke'; color = '#004B9B'; icon = 'building-2'
        } | ConvertTo-Json)
        $banks = Invoke-RestMethod -Uri "$backend/api/banks" -Headers $auth
        if (-not ($banks | Where-Object { $_.name -eq 'Banco Smoke' })) { throw 'El banco no aparecio en la lectura.' }
        Write-Ok "Banco registrado y leido desde Postgres."

        $month = Get-Date -Format 'yyyy-MM'
        $null = Invoke-RestMethod -Method Post -Uri "$backend/api/expenses" -Headers $auth -ContentType 'application/json' -Body (@{
            date = (Get-Date -Format 'yyyy-MM-dd'); description = 'Gasto smoke'; category = 'Comida'
            amount = 12345; currency = 'Ars'; paymentMethodKind = 'Cash'
        } | ConvertTo-Json)
        $expenses = Invoke-RestMethod -Uri "$backend/api/expenses?month=$month" -Headers $auth
        if (-not ($expenses | Where-Object { $_.description -eq 'Gasto smoke' })) { throw 'El gasto no aparecio en la lectura.' }
        Write-Ok "Gasto registrado y leido (evento -> proyeccion -> read model)."

        # 3. Dia habil + novedades (lo que consulta un agente MCP)
        $today = Get-Date -Format 'yyyy-MM-dd'
        try { $null = Invoke-RestMethod -Method Post -Uri "$backend/api/business-days/$today/open" -Headers $auth } catch { }
        $null = Invoke-RestMethod -Uri "$backend/api/business-days/$today/novelties" -Headers $auth
        Write-Ok "Dia habil abierto y novedades consultadas."

        Write-Host ""
        Write-Host "  SMOKE TEST: PASS - el stack completo funciona end-to-end." -ForegroundColor Green
    } catch {
        Write-Fail "Smoke test fallo: $($_.Exception.Message)"
        Write-Host "  Diagnostico: docker compose logs backend" -ForegroundColor Yellow
        exit 1
    }
}

# -- Codigo de admin para TU familia --------------------------------------------
Write-Step "Emitiendo un codigo de administrador para que crees tu familia..."
$yourCode = (Invoke-RestMethod -Method Post -Uri "$backend/api/admin/invites" -Headers @{ 'X-Admin-Key' = $adminKey }).code

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "  GastNyahp esta corriendo" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Frontend:  http://localhost:3001"
Write-Host "  API:       $backend/api"
Write-Host "  MCP:       $backend/mcp  (tambien proxeado en http://localhost:3001/mcp)"
Write-Host ""
Write-Host "  Tu codigo para crear una familia (un solo uso):" -ForegroundColor Yellow
Write-Host ""
Write-Host "      $yourCode" -ForegroundColor White
Write-Host ""
Write-Host "  1. Abri http://localhost:3001 -> 'Crear familia' -> pega el codigo."
Write-Host "  2. Desde Ajustes genera invitaciones QR para tu familia y claves de"
Write-Host "     agente para conectar clientes MCP (Claude Desktop, cron, etc.)."
Write-Host ""
Write-Host "  Logs:   docker compose logs -f backend"
Write-Host "  Bajar:  .\run-stack.ps1 -Down"
Write-Host ""

Start-Process 'http://localhost:3001'
