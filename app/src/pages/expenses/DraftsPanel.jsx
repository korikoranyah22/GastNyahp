import { useState } from 'react'
import { Bot, Check, Trash2, Loader2 } from 'lucide-react'
import useStore from '../../store/useStore'
import { formatAmount } from '../../lib/formatters'

// Borradores conversacionales: los moldea un agente (Telegram vía MCP) y acá se confirman o descartan.
// Confirmar dispara la carga REAL en el backend con todas las validaciones del dominio.

const KIND_LABEL = { Expense: 'gasto', Ticket: 'ticket', Installment: 'cuotas' }

function draftTotal(draft) {
  const p = draft.payload ?? {}
  if (draft.kind === 'Ticket') {
    const items = p.items ?? []
    return Math.max(0, items.reduce((s, i) => s + (i.amount ?? 0), 0) - (p.discount ?? 0))
  }
  if (draft.kind === 'Installment') return (p.monthlyAmount ?? 0) * (p.totalInstallments ?? 0)
  return p.amount ?? 0
}

function draftDetail(draft) {
  const p = draft.payload ?? {}
  if (draft.kind === 'Ticket') {
    const items = p.items ?? []
    const detail = items.slice(0, 3).map((i) => i.description).join(', ')
    return items.length === 0
      ? 'sin ítems todavía'
      : `${items.length} ítem${items.length === 1 ? '' : 's'}: ${detail}${items.length > 3 ? '…' : ''}${p.discount > 0 ? ` · desc. ${formatAmount(p.discount)}` : ''}`
  }
  if (draft.kind === 'Installment')
    return p.totalInstallments ? `${p.totalInstallments} cuotas de ${formatAmount(p.monthlyAmount ?? 0)}` : 'cuotas sin definir'
  return p.category ?? ''
}

export default function DraftsPanel() {
  const drafts = useStore((s) => s.drafts)
  const confirmDraft = useStore((s) => s.confirmDraft)
  const discardDraft = useStore((s) => s.discardDraft)
  const [busyId, setBusyId] = useState(null)
  const [error, setError] = useState('')

  if (!drafts?.length) return null

  const handleConfirm = async (draft) => {
    setBusyId(draft.id); setError('')
    const { error: err } = await confirmDraft(draft)
    if (err) setError(err)
    setBusyId(null)
  }

  const handleDiscard = async (draft) => {
    if (!window.confirm('¿Descartar este borrador? No afecta la contabilidad.')) return
    setBusyId(draft.id); setError('')
    const { error: err } = await discardDraft(draft.id)
    if (err) setError(err)
    setBusyId(null)
  }

  return (
    <div className="mb-5 bg-[#151820] border border-amber-500/25 rounded-xl overflow-hidden">
      <div className="px-4 py-2.5 border-b border-[#1c2030] flex items-center gap-2 flex-wrap">
        <Bot size={14} className="text-amber-400" />
        <h3 className="text-xs font-semibold text-amber-400">Borradores pendientes ({drafts.length})</h3>
        <span className="text-[10px] text-[#64748b]">los arma tu agente por chat — acá los confirmás</span>
      </div>

      {error && <p className="px-4 pt-2 text-xs text-red-400">{error}</p>}

      {drafts.map((d) => (
        <div key={d.id} className="flex items-center gap-3 px-4 py-2.5 border-b border-[#1c2030]/60 last:border-0">
          <span className="text-[10px] font-medium px-1.5 py-0.5 rounded bg-amber-500/10 text-amber-400 uppercase shrink-0">
            {KIND_LABEL[d.kind] ?? d.kind}
          </span>
          <div className="flex-1 min-w-0">
            <p className="text-sm text-white truncate">{d.payload?.description ?? '(sin descripción)'}</p>
            <p className="text-[10px] text-[#64748b] truncate">
              {draftDetail(d)}
              {d.payload?.note ? ` · ${d.payload.note}` : ''}
            </p>
          </div>
          <span className="text-sm mono text-white shrink-0">{formatAmount(draftTotal(d))}</span>
          <div className="flex items-center gap-1.5 shrink-0">
            <button
              onClick={() => handleConfirm(d)}
              disabled={busyId === d.id}
              className="flex items-center gap-1 px-2.5 py-1 rounded-lg text-[11px] font-medium bg-green-600/80 hover:bg-green-500 text-white transition-colors disabled:opacity-50"
            >
              {busyId === d.id ? <Loader2 size={11} className="animate-spin" /> : <Check size={11} />}
              Confirmar
            </button>
            <button
              onClick={() => handleDiscard(d)}
              disabled={busyId === d.id}
              className="p-1.5 rounded-lg text-[#64748b] hover:text-red-400 hover:bg-red-500/10 transition-colors disabled:opacity-50"
              title="Descartar borrador"
            >
              <Trash2 size={13} />
            </button>
          </div>
        </div>
      ))}
    </div>
  )
}
