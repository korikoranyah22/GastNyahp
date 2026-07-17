import { useState } from 'react'
import { Users, KeyRound, LogIn, Loader2, ArrowLeft, Home } from 'lucide-react'
import useStore from '../../store/useStore'
import PasswordField from '../../components/ui/PasswordField'
import { api } from '../../lib/api'

// La puerta de entrada. Lo normal es ENTRAR con email y contraseña (docs/DISENO_CUENTAS_LOGIN.md): el acceso ya
// no depende de que este dispositivo conserve un token. Crear una familia sigue requiriendo un código del
// administrador de la app, y unirse el código de una invitación QR.
//
// Sin mails no hay "te mandamos un link": si perdiste la contraseña, un Admin de tu familia te da un código a
// mano y lo canjeás acá mismo (§2).

const inputCls = 'w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30 placeholder:text-[#3d4466]'
const primaryBtnCls = 'w-full flex items-center justify-center gap-2 bg-blue-600 hover:bg-blue-500 disabled:opacity-60 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors'
const cardCls = 'space-y-4 bg-[#151820] border border-[#2e3350] rounded-xl p-5'

// Canje del código que te pasó un Admin. Anónimo a propósito: el que llega acá no tiene con qué entrar.
function ResetForm({ onBack }) {
  const [code, setCode] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [done, setDone] = useState(false)
  const [busy, setBusy] = useState(false)

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    if (!code.trim() || !password) { setError('Completá todos los campos.'); return }
    setBusy(true)
    try {
      await api.redeemPasswordReset(code.trim(), password)
      setDone(true)
    } catch (err) {
      setError(err.message)
    } finally {
      setBusy(false)
    }
  }

  if (done) {
    return (
      <div className={cardCls}>
        <p className="text-sm text-green-400">Listo: tu contraseña quedó cambiada.</p>
        <p className="text-xs text-[#94a3b8]">Entrá con tu email y la contraseña nueva.</p>
        <button onClick={onBack} className={primaryBtnCls}>Ir a entrar</button>
      </div>
    )
  }

  return (
    <form onSubmit={handleSubmit} className={cardCls}>
      <div>
        <label className="block text-xs text-[#94a3b8] mb-1.5">Código de reseteo</label>
        <input className={inputCls} value={code} onChange={(e) => setCode(e.target.value)}
          placeholder="El código que te pasó un admin" autoFocus />
        <p className="mt-1.5 text-[11px] text-[#64748b]">
          Un admin de tu familia lo genera desde Ajustes y te lo pasa. Es de un solo uso y vence a las 48 horas.
        </p>
      </div>

      <PasswordField label="Contraseña nueva" value={password} onChange={setPassword} autoComplete="new-password"
        placeholder="Al menos 10 caracteres" hint="Mejor una frase larga que algo corto y raro." />

      {error && <p className="text-xs text-red-400">{error}</p>}

      <button type="submit" disabled={busy} className={primaryBtnCls}>
        {busy && <Loader2 size={14} className="animate-spin" />}
        Cambiar mi contraseña
      </button>
      <button type="button" onClick={onBack}
        className="w-full flex items-center justify-center gap-1.5 text-xs text-[#64748b] hover:text-white transition-colors">
        <ArrowLeft size={12} /> Volver
      </button>
    </form>
  )
}

// El mismo email puede estar en varias familias (la unicidad es por familia, §2.1). Si la contraseña coincide en
// más de una, el server no adivina: pregunta (§3.2).
function FamilyPicker({ choices, onPick, onCancel, busy }) {
  return (
    <div className={cardCls}>
      <div>
        <p className="text-sm text-white font-medium">¿A qué familia querés entrar?</p>
        <p className="text-xs text-[#64748b] mt-1">Tu email está en más de una.</p>
      </div>
      <ul className="space-y-2">
        {choices.map((f) => (
          <li key={f.familyId}>
            <button
              onClick={() => onPick(f.familyId)}
              disabled={busy}
              className="w-full flex items-center gap-2 px-3 py-2.5 rounded-lg border border-[#2e3350] text-sm text-[#e2e8f0] hover:border-blue-500 hover:bg-[#1c2030] disabled:opacity-60 transition-colors"
            >
              <Home size={14} className="text-[#64748b]" /> {f.name}
            </button>
          </li>
        ))}
      </ul>
      <button type="button" onClick={onCancel}
        className="w-full flex items-center justify-center gap-1.5 text-xs text-[#64748b] hover:text-white transition-colors">
        <ArrowLeft size={12} /> Volver
      </button>
    </div>
  )
}

export default function AuthScreen() {
  const createFamily = useStore((s) => s.createFamily)
  const joinFamily = useStore((s) => s.joinFamily)
  const login = useStore((s) => s.login)

  // Si llegaste por un enlace "crear familia nueva" (?crear-familia=<código>), arrancás en ese modo con el código
  // ya cargado — solo ponés el nombre de la familia y el tuyo.
  const params = new URLSearchParams(window.location.search)
  const createCode = params.get('crear-familia') || ''
  const joinCode = params.get('unirme') || ''
  const prefillCode = createCode || joinCode

  const [mode, setMode] = useState(createCode ? 'create' : joinCode ? 'join' : 'login')   // 'login' | 'join' | 'create' | 'reset'
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [choices, setChoices] = useState(null)  // familias entre las que elegir (el 300)
  const [code, setCode] = useState(prefillCode)
  const [familyName, setFamilyName] = useState('')
  const [memberName, setMemberName] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const switchMode = (next) => { setMode(next); setError(''); setChoices(null) }

  const doLogin = async (familyId) => {
    setError(''); setBusy(true)
    const result = await login(email.trim(), password, familyId)
    setBusy(false)
    if (result.error) { setError(result.error); return }
    if (result.choices) setChoices(result.choices)
    // Sin error ni choices, initAuth ya cambió authStatus y esta pantalla se desmonta.
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')

    if (mode === 'login') {
      if (!email.trim() || !password) { setError('Completá todos los campos.'); return }
      await doLogin(undefined)
      return
    }

    if (!code.trim() || !memberName.trim() || (mode === 'create' && !familyName.trim())) {
      setError('Completá todos los campos.')
      return
    }
    setBusy(true)
    const result = mode === 'create'
      ? await createFamily(code.trim(), familyName.trim(), memberName.trim())
      : await joinFamily(code.trim(), memberName.trim())
    setBusy(false)
    if (result.error) setError(result.error)
  }

  const tabCls = (active) => `flex items-center justify-center gap-1.5 px-2 py-2 rounded-lg text-xs font-medium border transition-colors ${
    active ? 'bg-blue-600 border-blue-600 text-white' : 'border-[#2e3350] text-[#94a3b8] hover:text-white'
  }`

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#0d0f14] text-[#e2e8f0] p-6">
      <div className="w-full max-w-sm">
        <div className="flex items-center gap-3 mb-6">
          <img src="/logo.png" alt="" className="w-10 h-10" />
          <div>
            <h1 className="text-lg font-semibold text-white">GastNyahp</h1>
            <p className="text-xs text-[#64748b]">Finanzas de tu familia, en tu servidor.</p>
          </div>
        </div>

        {mode !== 'reset' && !choices && (
          <div className="grid grid-cols-3 gap-2 mb-5">
            <button onClick={() => switchMode('login')} className={tabCls(mode === 'login')}>
              <LogIn size={13} /> Entrar
            </button>
            <button onClick={() => switchMode('join')} className={tabCls(mode === 'join')}>
              <Users size={13} /> Unirme
            </button>
            <button onClick={() => switchMode('create')} className={tabCls(mode === 'create')}>
              <KeyRound size={13} /> Crear familia
            </button>
          </div>
        )}

        {mode === 'reset' ? (
          <ResetForm onBack={() => switchMode('login')} />
        ) : choices ? (
          <FamilyPicker choices={choices} busy={busy} onPick={doLogin} onCancel={() => setChoices(null)} />
        ) : (
          <form onSubmit={handleSubmit} className={cardCls}>
            {mode === 'login' && (
              <>
                <div>
                  <label className="block text-xs text-[#94a3b8] mb-1.5">Email</label>
                  <input type="email" className={inputCls} value={email} onChange={(e) => setEmail(e.target.value)}
                    placeholder="vos@ejemplo.com" autoFocus />
                </div>
                <PasswordField label="Contraseña" value={password} onChange={setPassword}
                  placeholder="Tu contraseña" autoComplete="current-password" />
              </>
            )}

            {mode === 'create' && (
              <>
                <div>
                  <label className="block text-xs text-[#94a3b8] mb-1.5">Código del administrador</label>
                  <input className={inputCls} value={code} onChange={(e) => setCode(e.target.value)}
                    placeholder="El código que te dio el admin de la app" autoFocus />
                </div>
                <div>
                  <label className="block text-xs text-[#94a3b8] mb-1.5">Nombre de la familia</label>
                  <input className={inputCls} value={familyName} onChange={(e) => setFamilyName(e.target.value)} placeholder="Los Pérez" />
                </div>
              </>
            )}

            {mode === 'join' && (
              <div>
                <label className="block text-xs text-[#94a3b8] mb-1.5">Código de invitación</label>
                <input className={inputCls} value={code} onChange={(e) => setCode(e.target.value)}
                  placeholder="Escaneá el QR o pegá el código" autoFocus />
                <p className="mt-1.5 text-[11px] text-[#64748b]">Un administrador de tu familia lo genera desde Ajustes.</p>
              </div>
            )}

            {mode !== 'login' && (
              <div>
                <label className="block text-xs text-[#94a3b8] mb-1.5">Tu nombre</label>
                <input className={inputCls} value={memberName} onChange={(e) => setMemberName(e.target.value)} placeholder="Cami" />
              </div>
            )}

            {error && <p className="text-xs text-red-400">{error}</p>}

            <button type="submit" disabled={busy} className={primaryBtnCls}>
              {busy && <Loader2 size={14} className="animate-spin" />}
              {mode === 'create' ? 'Crear mi familia' : mode === 'join' ? 'Unirme a la familia' : 'Entrar'}
            </button>

            {mode === 'login' && (
              <button type="button" onClick={() => switchMode('reset')}
                className="w-full text-center text-xs text-[#64748b] hover:text-white transition-colors">
                Olvidé mi contraseña
              </button>
            )}
          </form>
        )}

      </div>
    </div>
  )
}
