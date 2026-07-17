import { useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { FlaskConical, Sparkles, Cloud, FolderOpen } from 'lucide-react'
import useStore from '../../store/useStore'
import { markClean } from '../../lib/dirtyTracker'

// ── Tarjeta de opción ──────────────────────────────────────────────────────────
function OptionCard({ icon: Icon, iconBg, label, desc, onClick }) {
  return (
    <button
      onClick={onClick}
      className="bg-[#151820] border border-[#2e3350] rounded-xl p-5 text-left
                 hover:border-[#3d4466] hover:bg-[#1a1f2e] transition-all
                 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
    >
      <div className={`w-9 h-9 rounded-lg flex items-center justify-center mb-3 ${iconBg}`}>
        <Icon size={18} className="text-white" />
      </div>
      <p className="text-sm font-semibold text-white mb-1">{label}</p>
      <p className="text-[11px] text-[#64748b] leading-relaxed">{desc}</p>
    </button>
  )
}

// ── Pantalla de bienvenida ─────────────────────────────────────────────────────
export default function WelcomeScreen({ onDone }) {
  const importData = useStore((s) => s.importData)
  const navigate   = useNavigate()
  const fileRef    = useRef(null)

  /** Marca como limpio y cierra el welcome screen, navega a `path`. */
  const finish = (path = '/') => {
    onDone()
    navigate(path, { replace: true })
  }

  // ── Opción 1: Mantener datos de demo ────────────────────────────────────────
  const handleDemo = () => {
    markClean()
    finish('/')
  }

  // ── Opción 2: Empezar desde cero ────────────────────────────────────────────
  const handleFresh = () => {
    importData(JSON.stringify({
      banks:         [],
      creditCards:   [],
      installments:  [],
      loans:         [],
      services:      [],
      expenses:      [],
      budgets:       {},
      fixedExpenses: [],
      income: { netMonthly: 0, usdRateOfficial: 1050, usdRateCCL: 1250, splitPercent: 70 },
    }))
    markClean()
    finish('/')
  }

  // ── Opción 3: Conectar con Google Drive ─────────────────────────────────────
  const handleDrive = () => {
    markClean()
    finish('/settings')
  }

  // ── Opción 4: Importar JSON desde dispositivo ────────────────────────────────
  const handleImport = (e) => {
    const file = e.target.files[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      const result = importData(ev.target.result)
      if (result.error) {
        alert('Error al importar: ' + result.error)
      } else {
        markClean()
        finish('/')
      }
    }
    reader.readAsText(file)
    e.target.value = ''
  }

  return (
    <div className="fixed inset-0 z-50 bg-[#0d0f14] welcome-bg flex items-center justify-center p-6">
      <div className="w-full max-w-sm">

        {/* ── Encabezado ─────────────────────────────────────────────────── */}
        <div className="text-center mb-8">
          <img
            src="/logo.png"
            alt="GastNyahp"
            className="w-14 h-14 rounded-2xl object-cover mx-auto mb-4"
            onError={(e) => {
              e.currentTarget.style.display = 'none'
              e.currentTarget.insertAdjacentHTML('afterend',
                '<div class="w-14 h-14 bg-blue-600 rounded-2xl flex items-center justify-center mx-auto mb-4 text-2xl select-none">💰</div>'
              )
            }}
          />
          <h1 className="text-2xl font-bold text-white mb-2">Bienvenido a GastNyahp</h1>
          <p className="text-sm text-[#64748b]">¿Cómo querés empezar?</p>
        </div>

        {/* ── Opciones ───────────────────────────────────────────────────── */}
        <div className="grid grid-cols-2 gap-3">

          <OptionCard
            icon={FlaskConical}
            iconBg="bg-violet-600"
            label="Datos de demo"
            desc="Explorá la app con datos de ejemplo ya cargados"
            onClick={handleDemo}
          />

          <OptionCard
            icon={Sparkles}
            iconBg="bg-emerald-600"
            label="Empezar desde cero"
            desc="Sin datos, configurá todo desde el principio"
            onClick={handleFresh}
          />

          <OptionCard
            icon={Cloud}
            iconBg="bg-blue-600"
            label="Google Drive"
            desc="Conectar y cargar datos desde la nube"
            onClick={handleDrive}
          />

          {/* Importar — botón manual (el label envuelve el input) */}
          <button
            onClick={() => fileRef.current?.click()}
            className="bg-[#151820] border border-[#2e3350] rounded-xl p-5 text-left
                       hover:border-[#3d4466] hover:bg-[#1a1f2e] transition-all
                       focus:outline-none focus:ring-2 focus:ring-blue-500/40"
          >
            <div className="w-9 h-9 rounded-lg flex items-center justify-center mb-3 bg-amber-600">
              <FolderOpen size={18} className="text-white" />
            </div>
            <p className="text-sm font-semibold text-white mb-1">Importar archivo</p>
            <p className="text-[11px] text-[#64748b] leading-relaxed">Cargar un JSON desde tu dispositivo</p>
          </button>
          <input
            ref={fileRef}
            type="file"
            accept=".json"
            className="hidden"
            onChange={handleImport}
          />

        </div>

        <p className="text-center text-[10px] text-[#3d4466] mt-6">
          Podés cambiar esto en cualquier momento desde Ajustes
        </p>
      </div>
    </div>
  )
}
