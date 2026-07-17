import { useState } from 'react'
import { GitMerge, Check, User, Cloud, ChevronDown, ChevronUp } from 'lucide-react'
import { useDriveSync } from '../../hooks/useDriveSync'
import { getConflictMeta } from '../../lib/mergeUtils'

// ── Helpers de formato ─────────────────────────────────────────────────────────

function fmtDateTime(isoStr) {
  if (!isoStr) return '—'
  return new Date(isoStr).toLocaleString('es-AR', {
    day:    '2-digit',
    month:  '2-digit',
    hour:   '2-digit',
    minute: '2-digit',
  })
}

// ── Tarjeta de un conflicto individual ────────────────────────────────────────

function ConflictCard({ conflict, choice, onChoice }) {
  const meta     = getConflictMeta(conflict)
  const keepMine = choice === 'local'  || !choice   // default: local
  const keepHis  = choice === 'remote'
  const [expanded, setExpanded] = useState(false)

  return (
    <div className="rounded-xl border border-[#2e3350] bg-[#111318] overflow-hidden">
      {/* Header */}
      <div className="flex items-center gap-3 px-4 py-3">
        <span className="text-lg shrink-0">{meta.icon}</span>
        <div className="flex-1 min-w-0">
          <p className="text-[11px] font-semibold text-[#64748b] uppercase tracking-wide leading-none">
            {meta.screen}
          </p>
          <p className="text-sm font-medium text-white truncate mt-0.5">{meta.label}</p>
        </div>
        <button
          onClick={() => setExpanded((v) => !v)}
          className="p-1 rounded text-[#3d4466] hover:text-[#64748b] transition-colors"
          title="Ver fechas"
        >
          {expanded ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
        </button>
      </div>

      {/* Timestamps (expandible) */}
      {expanded && (
        <div className="px-4 pb-2 flex gap-4 text-[11px] text-[#64748b]">
          <span>📱 Local: <strong className="text-[#94a3b8]">{fmtDateTime(meta.localUpdatedAt)}</strong></span>
          <span>☁️ Remoto: <strong className="text-[#94a3b8]">{fmtDateTime(meta.remoteUpdatedAt)}</strong></span>
        </div>
      )}

      {/* Selector */}
      <div className="flex border-t border-[#2e3350]">
        <button
          onClick={() => onChoice('local')}
          className={`flex-1 flex items-center justify-center gap-1.5 py-2.5 text-xs font-semibold transition-colors ${
            keepMine
              ? 'bg-blue-600/20 text-blue-400'
              : 'text-[#3d4466] hover:text-[#64748b] hover:bg-[#1c2030]'
          }`}
        >
          {keepMine && <Check size={11} strokeWidth={3} />}
          <User size={11} />
          Mantener mío
        </button>
        <div className="w-px bg-[#2e3350]" />
        <button
          onClick={() => onChoice('remote')}
          className={`flex-1 flex items-center justify-center gap-1.5 py-2.5 text-xs font-semibold transition-colors ${
            keepHis
              ? 'bg-purple-600/20 text-purple-400'
              : 'text-[#3d4466] hover:text-[#64748b] hover:bg-[#1c2030]'
          }`}
        >
          {keepHis && <Check size={11} strokeWidth={3} />}
          <Cloud size={11} />
          Aceptar remoto
        </button>
      </div>
    </div>
  )
}

// ── Modal principal ────────────────────────────────────────────────────────────

export default function MergeConflictModal() {
  const { pendingMerge, resolveMerge, isSaving } = useDriveSync()

  const [choices,   setChoices]   = useState({})
  const [resolving, setResolving] = useState(false)

  if (!pendingMerge) return null

  const { conflicts } = pendingMerge

  const setChoice = (id, value) =>
    setChoices((prev) => ({ ...prev, [id]: value }))

  const setAllChoices = (value) => {
    const all = {}
    conflicts.forEach((c) => { all[c.id] = value })
    setChoices(all)
  }

  const handleResolve = async () => {
    setResolving(true)
    try {
      await resolveMerge(choices)
    } catch (e) {
      console.error('[MergeConflictModal] resolveMerge:', e)
    } finally {
      setResolving(false)
    }
  }

  const isWorking = resolving || isSaving

  return (
    /* Overlay */
    <div className="fixed inset-0 z-[9999] flex items-start justify-center p-4 pt-12 bg-black/70 backdrop-blur-sm overflow-y-auto">
      <div className="relative w-full max-w-md rounded-2xl bg-[#151820] border border-[#2e3350] shadow-2xl flex flex-col gap-4 p-5 mb-8">

        {/* Título */}
        <div className="flex items-start gap-3">
          <div className="shrink-0 w-10 h-10 rounded-xl bg-purple-500/10 flex items-center justify-center">
            <GitMerge size={20} className="text-purple-400" />
          </div>
          <div>
            <h2 className="text-sm font-semibold text-white leading-snug">
              Conflictos de sincronización
            </h2>
            <p className="mt-0.5 text-xs text-[#64748b] leading-relaxed">
              Otra instancia modificó {conflicts.length} ítem{conflicts.length !== 1 ? 's' : ''} mientras trabajabas.
              Elegí qué versión mantener por cada uno.
            </p>
          </div>
        </div>

        {/* Acciones bulk */}
        <div className="flex gap-2">
          <button
            onClick={() => setAllChoices('local')}
            className="flex-1 py-1.5 rounded-lg border border-[#2e3350] text-xs font-medium text-[#64748b] hover:text-white hover:border-[#3d4466] transition-colors"
          >
            Mantener todos los míos
          </button>
          <button
            onClick={() => setAllChoices('remote')}
            className="flex-1 py-1.5 rounded-lg border border-[#2e3350] text-xs font-medium text-[#64748b] hover:text-white hover:border-[#3d4466] transition-colors"
          >
            Aceptar todos remotos
          </button>
        </div>

        {/* Lista de conflictos */}
        <div className="space-y-2 max-h-[50vh] overflow-y-auto pr-0.5">
          {conflicts.map((conflict) => (
            <ConflictCard
              key={conflict.id}
              conflict={conflict}
              choice={choices[conflict.id]}
              onChoice={(v) => setChoice(conflict.id, v)}
            />
          ))}
        </div>

        {/* Nota: items auto-mergeados */}
        <p className="text-[10px] text-[#3d4466] text-center leading-relaxed">
          Los ítems sin conflicto se mergearon automáticamente.
          Los sub-datos (montos por mes, cuotas, etc.) se fusionaron por item.
        </p>

        {/* Botón resolver */}
        <button
          onClick={handleResolve}
          disabled={isWorking}
          className="w-full flex items-center justify-center gap-2 py-2.5 rounded-xl text-sm font-semibold bg-purple-600 hover:bg-purple-500 disabled:opacity-60 disabled:cursor-not-allowed text-white transition-colors"
        >
          {isWorking ? (
            <>
              <span className="w-3.5 h-3.5 rounded-full border-2 border-white/30 border-t-white animate-spin" />
              {isSaving ? 'Guardando en Drive…' : 'Aplicando…'}
            </>
          ) : (
            <>
              <GitMerge size={14} />
              Resolver y guardar
            </>
          )}
        </button>

      </div>
    </div>
  )
}
