import { useEffect, useRef, useState } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { Users, QrCode, KeyRound, Copy, Check, Ban, Loader2, Bot, Upload, Monitor, RotateCcw, Eye, EyeOff, LogOut } from 'lucide-react'
import useStore from '../../store/useStore'
import PasswordField from '../../components/ui/PasswordField'
import { api } from '../../lib/api'

// ── Controlled number input ───────────────────────────────────────────────────
function NumberField({ label, value, onChange, prefix = '$', suffix = '', hint }) {
  const [draft, setDraft] = useState(String(value ?? ''))

  const commit = () => {
    const parsed = draft === '' ? 0 : Number(draft)
    const nextValue = Number.isFinite(parsed) ? Math.max(0, parsed) : 0
    setDraft(String(nextValue))
    if (nextValue !== Number(value)) onChange(nextValue)
  }

  return (
    <div>
      <label className="block text-xs font-medium text-[#64748b] mb-1.5">{label}</label>
      <div className="flex items-center gap-2">
        {prefix && <span className="text-sm text-[#64748b] shrink-0">{prefix}</span>}
        <input
          type="number"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => {
            if (e.key === 'Enter') e.currentTarget.blur()
          }}
          min="0"
          step="any"
          className="flex-1 px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm font-mono focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30"
        />
        {suffix && <span className="text-sm text-[#64748b] shrink-0">{suffix}</span>}
      </div>
      {hint && <p className="text-[10px] text-[#64748b] mt-1">{hint}</p>}
    </div>
  )
}

function CopyButton({ text }) {
  const [copied, setCopied] = useState(false)
  const handleCopy = async () => {
    await navigator.clipboard.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 1500)
  }
  return (
    <button onClick={handleCopy} className="p-1.5 rounded-md text-[#94a3b8] hover:text-white hover:bg-[#1c2030] transition-colors" title="Copiar">
      {copied ? <Check size={13} className="text-green-400" /> : <Copy size={13} />}
    </button>
  )
}

// ── Ingresos ──────────────────────────────────────────────────────────────────
function IncomeSection() {
  const income = useStore((s) => s.income)
  const setIncome = useStore((s) => s.setIncome)

  return (
    <div className="bg-[#151820] border border-[#2e3350] rounded-xl p-4 grid gap-4 sm:grid-cols-2">
      <NumberField label="Sueldo neto mensual" value={income.netMonthly}
        onChange={(v) => setIncome({ netMonthly: v })} />
      <NumberField label="% destinado a gastos" value={income.splitPercent} prefix="" suffix="%"
        onChange={(v) => setIncome({ splitPercent: v })} />
      <NumberField label="Dólar oficial" value={income.usdRateOfficial}
        onChange={(v) => setIncome({ usdRateOfficial: v })} hint="Referencia — no se usa en cálculos" />
      <NumberField label="Dólar CCL" value={income.usdRateCCL}
        onChange={(v) => setIncome({ usdRateCCL: v })} hint="Usado para convertir gastos/servicios en USD" />
    </div>
  )
}

// ── Mi cuenta: crear credenciales (etapa 1) o cambiar la contraseña ───────────
// docs/DISENO_CUENTAS_LOGIN.md §3.3: los miembros que ya existían no tienen email ni contraseña y entran solo con
// el token de este dispositivo. Acá se crean la cuenta con ese mismo token, sin quedar afuera.
function AccountSection() {
  const family = useStore((s) => s.family)
  const setCredentials = useStore((s) => s.setCredentials)
  const changePassword = useStore((s) => s.changePassword)

  const me = family?.members.find((m) => m.memberId === family.meId)

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [current, setCurrent] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  if (!me) return null

  const handleCreate = async (e) => {
    e.preventDefault()
    setError(''); setBusy(true)
    const result = await setCredentials(email.trim(), password)
    setBusy(false)
    if (result.error) setError(result.error)
    else { setEmail(''); setPassword('') }
  }

  const handleChange = async (e) => {
    e.preventDefault()
    setError(''); setBusy(true)
    // Si sale bien no hace falta limpiar nada: cerrar las sesiones nos devuelve a la pantalla de ingreso.
    const result = await changePassword(current, password)
    setBusy(false)
    if (result.error) setError(result.error)
  }

  if (!me.hasCredentials) {
    return (
      <form onSubmit={handleCreate} className="bg-[#151820] border border-[#2e3350] rounded-xl p-4 space-y-4">
        <p className="text-xs text-[#64748b]">
          Hoy entrás solo porque este dispositivo tiene tu credencial guardada. Si la borrás o cambiás de teléfono,
          te quedás afuera y nadie puede recuperarte. Con un email y una contraseña volvés a entrar desde donde sea.
        </p>
        <div>
          <label className="block text-xs text-[#94a3b8] mb-1.5">Email</label>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="vos@ejemplo.com"
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30 placeholder:text-[#3d4466]" />
        </div>
        <PasswordField label="Contraseña" value={password} onChange={setPassword} autoComplete="new-password"
          placeholder="Al menos 10 caracteres" hint="Mejor una frase larga que algo corto y raro." />
        {error && <p className="text-xs text-red-400">{error}</p>}
        <button type="submit" disabled={busy}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-500 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors">
          {busy ? <Loader2 size={14} className="animate-spin" /> : <KeyRound size={14} />}
          Crear mi cuenta
        </button>
      </form>
    )
  }

  return (
    <form onSubmit={handleChange} className="bg-[#151820] border border-[#2e3350] rounded-xl p-4 space-y-4">
      <div>
        <p className="text-xs font-medium text-[#64748b] mb-1">Tu cuenta</p>
        <p className="text-sm text-[#e2e8f0]">{me.email}</p>
      </div>
      <div className="border-t border-[#2e3350] pt-4 space-y-4">
        <PasswordField label="Contraseña actual" value={current} onChange={setCurrent} autoComplete="current-password" />
        <PasswordField label="Contraseña nueva" value={password} onChange={setPassword} autoComplete="new-password"
          placeholder="Al menos 10 caracteres"
          hint="Al cambiarla se cierran todas las sesiones — incluida esta: vas a tener que entrar de nuevo." />
        {error && <p className="text-xs text-red-400">{error}</p>}
        <button type="submit" disabled={busy || !current || !password}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-500 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors">
          {busy ? <Loader2 size={14} className="animate-spin" /> : <KeyRound size={14} />}
          Cambiar contraseña
        </button>
      </div>
    </form>
  )
}

// ── Dispositivos donde tenés la sesión abierta ────────────────────────────────
function SessionsSection() {
  const [sessions, setSessions] = useState([])
  const [error, setError] = useState('')

  const reload = async () => setSessions(await api.sessions())
  useEffect(() => { api.sessions().then(setSessions).catch(() => {}) }, [])

  const handleRevoke = async (s) => {
    if (!window.confirm(`¿Cerrar la sesión de "${s.deviceName}"?`)) return
    setError('')
    try {
      await api.revokeSession(s.sessionId)
      await reload()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <div className="bg-[#151820] border border-[#2e3350] rounded-xl p-4 space-y-3">
      <p className="text-xs text-[#64748b]">
        Cada vez que entrás con tu email y contraseña se abre una sesión. Si perdiste un teléfono o entraste en una
        computadora prestada, cerrala desde acá.
      </p>
      {sessions.length === 0 ? (
        <p className="text-xs text-[#64748b]">No hay sesiones abiertas.</p>
      ) : (
        <ul className="space-y-1.5">
          {sessions.map((s) => (
            <li key={s.sessionId} className="flex items-center gap-2 text-sm">
              <Monitor size={13} className="text-[#64748b] shrink-0" />
              <span className="text-[#e2e8f0] truncate">{s.deviceName}</span>
              {s.current && <span className="text-[10px] px-1.5 py-0.5 rounded-md bg-green-500/15 text-green-400 shrink-0">este</span>}
              <span className="text-[10px] text-[#64748b] shrink-0">{new Date(s.createdAt).toLocaleDateString('es-AR')}</span>
              <button onClick={() => handleRevoke(s)}
                className="ml-auto flex items-center gap-1 text-[11px] text-[#94a3b8] hover:text-red-300 transition-colors shrink-0">
                <Ban size={11} /> Cerrar
              </button>
            </li>
          ))}
        </ul>
      )}
      {error && <p className="text-xs text-red-400">{error}</p>}
    </div>
  )
}

// ── Familia: miembros + invitación QR ─────────────────────────────────────────
function FamilySection() {
  const family = useStore((s) => s.family)
  const refreshFamily = useStore((s) => s.refreshFamily)
  const [invite, setInvite] = useState(null)   // { inviteCode, qrPayload, expiresAt }
  const [famInvite, setFamInvite] = useState(null)  // { code, expiresAt } — enlace para crear una familia nueva
  const [reset, setReset] = useState(null)     // { memberId, code } — el código se muestra UNA vez
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [busyFam, setBusyFam] = useState(false)

  const handleInvite = async () => {
    setError(''); setBusy(true)
    try {
      setInvite(await api.issueInvite())
      await refreshFamily()
    } catch (e) {
      setError(e.message)
    } finally {
      setBusy(false)
    }
  }

  const handleFamilyCreationInvite = async () => {
    setError(''); setBusyFam(true)
    try {
      setFamInvite(await api.issueFamilyCreationInvite())
    } catch (e) {
      setError(e.message)
    } finally {
      setBusyFam(false)
    }
  }

  // Sin mails, el reseteo es a mano: el admin genera el código y se lo pasa al miembro por donde quiera
  // (docs/DISENO_CUENTAS_LOGIN.md §2). El miembro lo canjea desde "Olvidé mi contraseña".
  const handleReset = async (m) => {
    if (!window.confirm(`¿Generar un código para que ${m.name} cambie su contraseña? Al usarlo se le van a cerrar todas las sesiones.`)) return
    setError('')
    try {
      const issued = await api.issuePasswordReset(m.memberId)
      setReset({ memberId: m.memberId, code: issued.code })
    } catch (e) {
      setError(e.message)
    }
  }

  if (!family) return null

  const me = family.members.find((m) => m.memberId === family.meId)
  const soyAdmin = me?.role === 'Admin'

  return (
    <div className="bg-[#151820] border border-[#2e3350] rounded-xl p-4 space-y-4">
      <div>
        <p className="text-xs font-medium text-[#64748b] mb-2">Miembros de {family.name}</p>
        <ul className="space-y-1">
          {family.members.map((m) => (
            <li key={m.memberId} className="flex items-center gap-2 text-sm text-[#e2e8f0]">
              <Users size={13} className="text-[#64748b] shrink-0" />
              {m.name}
              <span className="text-[10px] px-1.5 py-0.5 rounded-md bg-[#2e3350] text-[#94a3b8] shrink-0">{m.role === 'Admin' ? 'Admin' : 'Miembro'}</span>
              {!m.hasCredentials && (
                <span className="text-[10px] px-1.5 py-0.5 rounded-md bg-amber-500/15 text-amber-400 shrink-0" title="Todavía entra solo con la credencial de su dispositivo">
                  sin cuenta
                </span>
              )}
              {/* Resetear a alguien sin cuenta no serviría de nada: no tiene email con qué entrar. */}
              {soyAdmin && m.hasCredentials && (
                <button onClick={() => handleReset(m)}
                  className="ml-auto flex items-center gap-1 text-[11px] text-[#94a3b8] hover:text-white transition-colors shrink-0">
                  <RotateCcw size={11} /> Resetear contraseña
                </button>
              )}
            </li>
          ))}
        </ul>
        {reset && (
          <div className="mt-3 bg-amber-500/10 border border-amber-500/30 rounded-lg p-3 text-xs space-y-1.5">
            <p className="text-amber-300 font-medium">
              Pasale este código a {family.members.find((m) => m.memberId === reset.memberId)?.name} — no se vuelve a mostrar:
            </p>
            <div className="flex items-center gap-1 bg-[#1c2030] border border-[#2e3350] rounded-lg px-2 py-1.5">
              <code className="font-mono text-[11px] text-white truncate">{reset.code}</code>
              <CopyButton text={reset.code} />
            </div>
            <p className="text-[10px] text-[#64748b]">
              Lo canjea desde “Olvidé mi contraseña” en la pantalla de ingreso. Un solo uso · vence en 48 horas.
            </p>
          </div>
        )}
      </div>

      <div className="border-t border-[#2e3350] pt-4">
        <button
          onClick={handleInvite}
          disabled={busy}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-500 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors"
        >
          {busy ? <Loader2 size={14} className="animate-spin" /> : <QrCode size={14} />}
          Generar invitación QR
        </button>
        {error && <p className="text-xs text-red-400 mt-2">{error}</p>}

        {invite && (() => {
          const joinLink = `${window.location.origin}/?unirme=${encodeURIComponent(invite.inviteCode)}`
          return (
          <div className="mt-4 flex flex-col sm:flex-row items-start gap-4">
            <div className="bg-white p-3 rounded-xl">
              <QRCodeSVG value={invite.qrPayload} size={148} />
            </div>
            <div className="text-xs text-[#94a3b8] space-y-2 min-w-0">
              <p>La otra persona escanea el QR o pega este código en la pantalla de ingreso:</p>
              <div className="flex items-center gap-1 bg-[#1c2030] border border-[#2e3350] rounded-lg px-2 py-1.5">
                <code className="font-mono text-[11px] text-white truncate">{invite.inviteCode}</code>
                <CopyButton text={invite.inviteCode} />
              </div>
              <p>O compartile este enlace para que llegue directamente a “Unirme”:</p>
              <div className="flex items-center gap-1 bg-[#1c2030] border border-blue-500/40 rounded-lg px-2 py-1.5">
                <code className="font-mono text-[11px] text-white truncate">{joinLink}</code>
                <CopyButton text={joinLink} />
              </div>
              <p className="text-[10px] text-[#64748b]">
                Un solo uso · vence {new Date(invite.expiresAt).toLocaleString('es-AR', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })}
              </p>
            </div>
          </div>
          )
        })()}
      </div>

      {/* Solo el dueño de la instancia (admin de una familia del dueño) puede invitar a crear familias NUEVAS. */}
      {soyAdmin && family.isInstanceOwner && (
        <div className="border-t border-[#2e3350] pt-4">
          <p className="text-xs font-medium text-[#64748b] mb-1">Invitar a crear una familia nueva</p>
          <p className="text-[11px] text-[#64748b] mb-3">
            Generá un enlace y mandáselo a quien quieras. Al abrirlo, esa persona crea su propia familia (aparte de
            la tuya) y queda como su administrador.
          </p>
          <button
            onClick={handleFamilyCreationInvite}
            disabled={busyFam}
            className="flex items-center gap-2 bg-[#1c2030] border border-[#2e3350] hover:border-blue-500 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors"
          >
            {busyFam ? <Loader2 size={14} className="animate-spin" /> : <KeyRound size={14} />}
            Generar enlace de familia nueva
          </button>

          {famInvite && (() => {
            const link = `${window.location.origin}/?crear-familia=${encodeURIComponent(famInvite.code)}`
            return (
              <div className="mt-3 bg-amber-500/10 border border-amber-500/30 rounded-lg p-3 text-xs space-y-1.5">
                <p className="text-amber-300 font-medium">Copiá el enlace y mandáselo — no se vuelve a mostrar:</p>
                <div className="flex items-center gap-1 bg-[#1c2030] border border-[#2e3350] rounded-lg px-2 py-1.5">
                  <code className="font-mono text-[11px] text-white truncate">{link}</code>
                  <CopyButton text={link} />
                </div>
                <p className="text-[10px] text-[#64748b]">
                  Un solo uso · vence {new Date(famInvite.expiresAt).toLocaleString('es-AR', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })}
                </p>
              </div>
            )
          })()}
        </div>
      )}
    </div>
  )
}

// ── Claves de agente (credencial para clientes MCP) ───────────────────────────
function AgentKeysSection() {
  const [keys, setKeys] = useState([])
  const [name, setName] = useState('')
  const [issued, setIssued] = useState(null)  // { keyId, name, token } — el token se muestra UNA vez
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const reload = async () => setKeys(await api.agentKeys())
  useEffect(() => { reload().catch(() => {}) }, [])

  const handleIssue = async (e) => {
    e.preventDefault()
    if (!name.trim()) return
    setError(''); setBusy(true)
    try {
      setIssued(await api.issueAgentKey(name.trim()))
      setName('')
      await reload()
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  const handleRevoke = async (key) => {
    if (!window.confirm(`¿Revocar la clave "${key.name}"? El agente que la use va a perder acceso.`)) return
    setError('')
    try {
      await api.revokeAgentKey(key.keyId)
      if (issued?.keyId === key.keyId) setIssued(null)
      await reload()
    } catch (err) {
      setError(err.message)
    }
  }

  return (
    <div className="bg-[#151820] border border-[#2e3350] rounded-xl p-4 space-y-4">
      <p className="text-xs text-[#64748b]">
        Los agentes de IA (Claude Desktop, un cron) se conectan al servidor MCP con una de estas claves como
        <code className="mx-1 text-[10px] bg-[#1c2030] px-1 py-0.5 rounded">Authorization: Bearer</code>.
        Solo acceden a los datos: no pueden invitar miembros ni generar claves.
      </p>

      <form onSubmit={handleIssue} className="flex gap-2">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder='Nombre, ej. "cron matutino"'
          className="flex-1 px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 placeholder:text-[#3d4466]"
        />
        <button
          type="submit"
          disabled={busy || !name.trim()}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-500 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors"
        >
          <KeyRound size={14} /> Generar
        </button>
      </form>
      {error && <p className="text-xs text-red-400">{error}</p>}

      {issued && (
        <div className="bg-amber-500/10 border border-amber-500/30 rounded-lg p-3 text-xs space-y-1.5">
          <p className="text-amber-300 font-medium">Guardá esta clave ahora — no se vuelve a mostrar:</p>
          <div className="flex items-center gap-1 bg-[#1c2030] border border-[#2e3350] rounded-lg px-2 py-1.5">
            <code className="font-mono text-[11px] text-white truncate">{issued.token}</code>
            <CopyButton text={issued.token} />
          </div>
        </div>
      )}

      {keys.length > 0 && (
        <ul className="space-y-1.5">
          {keys.map((k) => (
            <li key={k.keyId} className="flex items-center gap-2 text-sm">
              <Bot size={13} className="text-[#64748b]" />
              <span className={k.revoked ? 'text-[#64748b] line-through' : 'text-[#e2e8f0]'}>{k.name}</span>
              <span className="text-[10px] text-[#64748b]">
                {new Date(k.issuedAt).toLocaleDateString('es-AR')}
              </span>
              {!k.revoked && (
                <button onClick={() => handleRevoke(k)} className="ml-auto flex items-center gap-1 text-[11px] text-[#94a3b8] hover:text-red-300 transition-colors">
                  <Ban size={11} /> Revocar
                </button>
              )}
              {k.revoked && <span className="ml-auto text-[10px] text-red-400/70">revocada</span>}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ── Importar el JSON de la maqueta ────────────────────────────────────────────
function ImportSection() {
  const loadAll = useStore((s) => s.loadAll)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [summary, setSummary] = useState(null)
  const [progress, setProgress] = useState(null)
  const [pendingData, setPendingData] = useState(null) // archivo parseado, esperando elegir Reemplazar/Agregar
  const pollTimer = useRef(null)

  // El import corre en el BACKEND (202 + job en background): acá solo seguimos el avance por polling.
  // Un F5 no lo cancela — al montar, si hay un job corriendo retomamos el spinner donde estaba.
  const applyFinal = async (st) => {
    setBusy(false); setProgress(null)
    if (st.status === 'completed') { setSummary(st.summary); await loadAll() }
    if (st.status === 'failed') setError(st.error || 'La importación falló.')
  }

  const poll = async () => {
    try {
      const st = await api.importStatus()
      if (st.status === 'running') {
        setBusy(true); setProgress(st.progress)
        pollTimer.current = setTimeout(poll, 1000)
      } else {
        await applyFinal(st)
      }
    } catch {
      pollTimer.current = setTimeout(poll, 2000) // blip de red: seguir intentando
    }
  }

  useEffect(() => {
    api.importStatus().then(async (st) => {
      if (st.status === 'running') {
        setBusy(true); setProgress(st.progress)
        pollTimer.current = setTimeout(poll, 1000)
      } else if (st.finishedAt && Date.now() - new Date(st.finishedAt).getTime() < 120000) {
        await applyFinal(st) // terminó mientras no mirábamos (p.ej. durante el F5)
      }
    }).catch(() => {})
    return () => clearTimeout(pollTimer.current)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleFile = async (e) => {
    const file = e.target.files[0]
    e.target.value = ''
    if (!file) return
    setError(''); setSummary(null); setPendingData(null); setBusy(true)
    try {
      const legacyData = JSON.parse(await file.text())
      try {
        await api.importLegacy(legacyData)
      } catch (err) {
        if (err.status === 409) {
          // Ya hay un job corriendo (p.ej. doble click): solo seguimos su avance.
        } else if (err.status === 422 && err.message.includes('ya tiene datos')) {
          // Familia con datos → el usuario elige: reemplazar todo, agregar encima, o cancelar.
          setPendingData(legacyData)
          setBusy(false)
          return
        } else {
          throw err
        }
      }
      poll()
    } catch (err) {
      setBusy(false)
      setError(err.message?.startsWith('Unexpected') ? 'El archivo no es un JSON válido.' : err.message)
    }
  }

  const startPending = async (options) => {
    setPendingData(null); setBusy(true)
    try {
      await api.importLegacy(pendingData, options)
      poll()
    } catch (err) {
      setBusy(false)
      setError(err.status === 409 ? 'Ya hay una importación en curso.' : err.message)
      if (err.status === 409) poll()
    }
  }

  const handleReplace = () => {
    if (window.confirm('Se va a BORRAR todo lo que la familia tiene cargado (queda auditado en el historial) y se importa el archivo desde cero. ¿Seguro?'))
      startPending({ replace: true })
  }

  return (
    <div className="bg-[#151820] border border-[#2e3350] rounded-xl p-4 space-y-3">
      <p className="text-xs text-[#64748b]">
        Subí el export JSON de la versión anterior (localStorage/Drive). Se reproduce como una carga manual:
        cada banco, cuota pagada y gasto queda registrado en el historial auditable de la familia.
      </p>
      <label className="inline-flex items-center gap-2 bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors cursor-pointer">
        {busy ? <Loader2 size={14} className="animate-spin" /> : <Upload size={14} />}
        Elegir archivo JSON
        <input type="file" accept=".json" className="hidden" onChange={handleFile} disabled={busy} />
      </label>
      {busy && (
        <p className="text-xs text-[#94a3b8]">
          {progress
            ? `Importando ${progress.section.toLowerCase()}… ${Math.min(progress.done + 1, progress.total)}/${progress.total}`
            : 'Importando…'}
          {' '}Podés salir de esta pantalla: la importación sigue en el servidor.
        </p>
      )}
      {pendingData && (
        <div className="bg-[#1c2030] border border-amber-500/30 rounded-lg p-3 space-y-2">
          <p className="text-xs text-amber-400 font-medium">La familia ya tiene datos. ¿Qué hacemos con lo del archivo?</p>
          <div className="flex items-center gap-2 flex-wrap">
            <button
              onClick={handleReplace}
              className="px-3 py-1.5 rounded-lg text-xs font-medium bg-red-600/90 hover:bg-red-500 text-white transition-colors"
            >
              Reemplazar todo
            </button>
            <button
              onClick={() => startPending({ force: true })}
              className="px-3 py-1.5 rounded-lg text-xs font-medium bg-blue-600 hover:bg-blue-500 text-white transition-colors"
            >
              Agregar encima
            </button>
            <button
              onClick={() => setPendingData(null)}
              className="px-3 py-1.5 rounded-lg text-xs font-medium border border-[#2e3350] text-[#64748b] hover:text-white transition-colors"
            >
              Cancelar
            </button>
          </div>
          <p className="text-[10px] text-[#64748b]">
            Reemplazar borra lo cargado (queda auditado en el historial) e importa desde cero. Agregar suma lo del archivo a lo existente.
          </p>
        </div>
      )}
      {error && <p className="text-xs text-red-400">{error}</p>}
      {summary && (
        <div className="text-xs text-[#94a3b8] space-y-1">
          <p className="text-green-400 font-medium">Importación completa:</p>
          <p>
            {summary.banks} bancos · {summary.cards} tarjetas · {summary.installments} cuotas · {summary.loans} préstamos
            · {summary.services} servicios · {summary.reserves} reservas · {summary.expenses + summary.tickets} movimientos
            · {summary.budgets} presupuestos{summary.income ? ' · ingresos' : ''}
            {summary.removed > 0 ? ` · reemplazó ${summary.removed} registros anteriores` : ''}
          </p>
          {summary.warnings?.length > 0 && (
            <details className="text-amber-400/80">
              <summary className="cursor-pointer">{summary.warnings.length} advertencias</summary>
              <ul className="mt-1 space-y-0.5 text-[11px]">
                {summary.warnings.map((w, i) => <li key={i}>• {w}</li>)}
              </ul>
            </details>
          )}
        </div>
      )}
    </div>
  )
}

// ── Página ────────────────────────────────────────────────────────────────────
function LogoutSection() {
  const logout = useStore((s) => s.logout)

  const handleLogout = () => {
    if (window.confirm('¿Cerrar sesión en este dispositivo? Vas a poder volver a entrar con tu email y contraseña o con una nueva invitación.')) {
      logout()
    }
  }

  return (
    <div className="bg-[#151820] border border-red-500/25 rounded-xl p-4">
      <p className="text-xs text-[#94a3b8] mb-3">
        Cierra la sesión únicamente en este dispositivo. Tu familia y sus datos no se eliminan.
      </p>
      <button
        type="button"
        onClick={handleLogout}
        className="flex items-center gap-2 px-4 py-2 rounded-lg border border-red-500/40 text-sm font-medium text-red-300 hover:text-white hover:bg-red-500/15 transition-colors"
      >
        <LogOut size={15} />
        Cerrar sesión
      </button>
    </div>
  )
}
export default function SettingsPage() {
  return (
    <div className="p-4 md:p-6 max-w-2xl space-y-6">
      <section>
        <h2 className="text-sm font-semibold text-white mb-3">Ingresos</h2>
        <IncomeSection />
      </section>

      <section>
        <h2 className="text-sm font-semibold text-white mb-3">Mi cuenta</h2>
        <AccountSection />
      </section>

      <section>
        <h2 className="text-sm font-semibold text-white mb-3">Mis dispositivos</h2>
        <SessionsSection />
      </section>

      <section>
        <h2 className="text-sm font-semibold text-white mb-3">Familia</h2>
        <FamilySection />
      </section>

      <section>
        <h2 className="text-sm font-semibold text-white mb-3">Claves de agente (MCP)</h2>
        <AgentKeysSection />
      </section>

      <section>
        <h2 className="text-sm font-semibold text-white mb-3">Importar desde la maqueta</h2>
        <ImportSection />
      </section>


      <section>
        <h2 className="text-sm font-semibold text-white mb-3">Sesión</h2>
        <LogoutSection />
      </section>
    </div>
  )
}
