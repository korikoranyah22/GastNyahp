import { useState, useRef, useEffect } from 'react'
import { Plus, ChevronDown, ChevronUp, Pencil, Trash2, PiggyBank, RefreshCw } from 'lucide-react'
import useStore from '../../store/useStore'
import { formatAmount, formatMonthLong } from '../../lib/formatters'
import FixedExpenseForm from './FixedExpenseForm'

// ── Inline amount + note editor ───────────────────────────────────────────────
// Para items recurrentes muestra dos botones: "Este mes" y "Todos →"
function InlineEdit({ amount, note, recurring, onSave, onSaveAll, onCancel }) {
  const [amt, setAmt]   = useState(String(amount || ''))
  const [ntxt, setNtxt] = useState(note || '')
  const amtRef = useRef(null)

  useEffect(() => { amtRef.current?.focus(); amtRef.current?.select() }, [])

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') onSave(Number(amt) || 0, ntxt)
    if (e.key === 'Escape') onCancel()
  }

  return (
    <div className="flex flex-col gap-1.5 mt-1" onKeyDown={handleKeyDown}>
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-xs text-[#64748b]">$</span>
        <input
          ref={amtRef}
          type="number"
          value={amt}
          onChange={(e) => setAmt(e.target.value)}
          className="w-32 px-2 py-1 bg-[#1c2030] border border-blue-500/60 rounded text-sm text-white font-mono focus:outline-none focus:ring-1 focus:ring-blue-500/40"
          placeholder="0"
        />

        {recurring ? (
          <>
            <button
              onClick={() => onSave(Number(amt) || 0, ntxt)}
              className="px-2 py-1 rounded border border-blue-500/40 text-blue-400 text-xs hover:bg-blue-500/10 transition-colors"
              title="Solo cambia este mes"
            >
              Este mes
            </button>
            <button
              onClick={() => onSaveAll(Number(amt) || 0)}
              className="px-2 py-1 rounded bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium transition-colors"
              title="Actualiza el monto base · se aplica a todos los meses sin override"
            >
              Todos →
            </button>
          </>
        ) : (
          <button
            onClick={() => onSave(Number(amt) || 0, ntxt)}
            className="px-2.5 py-1 rounded bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium transition-colors"
          >
            OK
          </button>
        )}

        <button
          onClick={onCancel}
          className="px-2.5 py-1 rounded border border-[#2e3350] text-[#64748b] hover:text-white text-xs transition-colors"
        >
          ✕
        </button>
      </div>

      {/* Nota (solo para override por mes) */}
      <input
        type="text"
        value={ntxt}
        onChange={(e) => setNtxt(e.target.value)}
        placeholder="Nota opcional (ej: incluye extra)"
        className="w-full px-2 py-1 bg-[#1c2030] border border-[#2e3350] rounded text-xs text-[#94a3b8] focus:outline-none focus:border-[#3d4466]"
      />

      {recurring && (
        <p className="text-[10px] text-[#64748b]">
          <span className="text-blue-400">Este mes</span> = override puntual ·{' '}
          <span className="text-blue-400">Todos →</span> = actualiza el monto base (borra overrides)
        </p>
      )}
    </div>
  )
}

// ── History row (expanded per item) ──────────────────────────────────────────
function HistoryRow({ monthData, onEdit }) {
  const [editing, setEditing] = useState(false)
  const isCurrent = monthData.month === new Date().toISOString().slice(0, 7)
  const isOverride = monthData._isOverride  // distingue override vs base

  if (editing) {
    return (
      <div className="px-4 py-2 bg-[#1c2030]">
        <p className="text-xs text-[#64748b] mb-1">{formatMonthLong(monthData.month)}</p>
        <InlineEdit
          amount={monthData.amount}
          note={monthData.note || ''}
          recurring={false}  // en historial siempre override puntual
          onSave={(amt, note) => { onEdit(monthData.month, amt, note); setEditing(false) }}
          onSaveAll={() => {}}
          onCancel={() => setEditing(false)}
        />
      </div>
    )
  }

  return (
    <div
      className={`flex items-center justify-between px-4 py-1.5 hover:bg-[#1c2030] cursor-pointer transition-colors ${isCurrent ? 'bg-[#1c2030]/50' : ''}`}
      onClick={() => setEditing(true)}
    >
      <div className="flex items-center gap-2">
        <span className={`text-xs ${isCurrent ? 'text-[#94a3b8] font-medium' : 'text-[#64748b]'}`}>
          {formatMonthLong(monthData.month)}
        </span>
        {isCurrent && (
          <span className="text-[10px] bg-blue-500/20 text-blue-400 px-1 py-0.5 rounded">actual</span>
        )}
        {isOverride && (
          <span className="text-[10px] bg-amber-500/15 text-amber-400 px-1 py-0.5 rounded">override</span>
        )}
      </div>
      <div className="text-right">
        <span className={`text-xs font-mono ${monthData.amount > 0 ? 'text-white' : 'text-[#3d4466]'}`}>
          {monthData.amount > 0 ? formatAmount(monthData.amount) : '—'}
        </span>
        {monthData.note && (
          <p className="text-[10px] text-[#64748b]">{monthData.note}</p>
        )}
      </div>
    </div>
  )
}

// ── Single reserve item card ──────────────────────────────────────────────────
function ReserveCard({ item, currentMonth, onEdit, onDelete }) {
  const setFixedExpenseMonth = useStore((s) => s.setFixedExpenseMonth)
  const setFixedExpenseBase  = useStore((s) => s.setFixedExpenseBase)
  const [expanded, setExpanded] = useState(false)
  const [editing, setEditing]   = useState(false)

  // Monto efectivo: override puntual > baseAmount (si recurrente) > 0
  const monthOverride = item.months.find((m) => m.month === currentMonth)
  const effectiveAmount = monthOverride
    ? monthOverride.amount
    : (item.recurring ? (item.baseAmount || 0) : 0)
  const effectiveNote  = monthOverride?.note || ''
  const hasOverride    = !!monthOverride

  // Historia: si recurrente, muestra meses con overrides + nota del monto base
  const history = [...item.months]
    .sort((a, b) => b.month.localeCompare(a.month))
    .map((m) => ({ ...m, _isOverride: true }))

  const handleSave = async (amount, note) => {
    const result = await setFixedExpenseMonth(item.id, currentMonth, amount, note)
    if (result.error) window.alert(result.error)
    else setEditing(false)
  }

  const handleSaveAll = async (amount) => {
    const result = await setFixedExpenseBase(item.id, amount)
    if (result.error) window.alert(result.error)
    else setEditing(false)
  }

  // Type badge
  const typeMeta = {
    reserve: { label: 'Reserva',   color: '#3b82f6' },
    cash:    { label: 'Efectivo',  color: '#f59e0b' },
    debt:    { label: 'Deuda',     color: '#ef4444' },
    other:   { label: 'Estimado',  color: '#8b5cf6' },
  }
  const meta = typeMeta[item.type] || typeMeta.other

  return (
    <div className="bg-[#151820] border border-[#2e3350] rounded-xl overflow-hidden">
      {/* Main row */}
      <div className="flex items-start justify-between px-4 py-3.5">
        {/* Left: icon + label + badges + amount */}
        <div className="flex items-center gap-3 min-w-0 flex-1">
          <div
            className="w-9 h-9 rounded-xl flex items-center justify-center text-lg shrink-0"
            style={{ backgroundColor: `${meta.color}18` }}
          >
            {item.icon}
          </div>

          <div className="min-w-0 flex-1">
            {/* Name row */}
            <div className="flex items-center gap-2 flex-wrap">
              <p className="text-sm font-semibold text-white">{item.label}</p>
              <span
                className="text-[10px] font-medium px-1.5 py-0.5 rounded"
                style={{ backgroundColor: `${meta.color}22`, color: meta.color }}
              >
                {meta.label}
              </span>
              {item.recurring && (
                <span className="flex items-center gap-1 text-[10px] font-medium px-1.5 py-0.5 rounded bg-emerald-500/15 text-emerald-400">
                  <RefreshCw size={9} />
                  Periódica
                </span>
              )}
              {hasOverride && item.recurring && (
                <span className="text-[10px] text-amber-400 bg-amber-500/15 px-1.5 py-0.5 rounded">
                  override este mes
                </span>
              )}
            </div>

            {/* Amount / inline edit */}
            {editing ? (
              <InlineEdit
                amount={effectiveAmount}
                note={effectiveNote}
                recurring={item.recurring}
                onSave={handleSave}
                onSaveAll={handleSaveAll}
                onCancel={() => setEditing(false)}
              />
            ) : (
              <button
                type="button"
                onClick={() => setEditing(true)}
                className="mt-1 flex items-center gap-2 px-2.5 py-1.5 rounded-lg border border-[#2e3350] hover:border-blue-500/50 hover:bg-blue-500/5 transition-all group/amt text-left"
                title="Click para editar el monto"
              >
                <span className={`text-base font-bold font-mono ${effectiveAmount > 0 ? 'text-white' : 'text-[#94a3b8]'}`}>
                  {effectiveAmount > 0 ? formatAmount(effectiveAmount) : 'Ingresar monto'}
                </span>
                <Pencil size={11} className="text-[#64748b] group-hover/amt:text-blue-400 transition-colors shrink-0" />
                {effectiveAmount > 0 && effectiveNote && (
                  <span className="text-xs text-[#64748b] italic">{effectiveNote}</span>
                )}
                {item.recurring && !hasOverride && effectiveAmount > 0 && (
                  <span className="text-[10px] text-emerald-400/70 ml-0.5">· base</span>
                )}
              </button>
            )}
          </div>
        </div>

        {/* Right: actions */}
        <div className="flex items-center gap-1 shrink-0 ml-2">
          <button
            onClick={() => onEdit(item)}
            className="p-1.5 rounded-lg text-[#3d4466] hover:text-[#94a3b8] hover:bg-[#1c2030] transition-colors"
            title="Editar nombre/tipo/ícono"
          >
            <Pencil size={13} />
          </button>
          <button
            onClick={() => onDelete(item.id)}
            className="p-1.5 rounded-lg text-[#3d4466] hover:text-red-400 hover:bg-red-500/10 transition-colors"
            title="Eliminar"
          >
            <Trash2 size={13} />
          </button>
          <button
            onClick={() => setExpanded((v) => !v)}
            className="p-1.5 rounded-lg text-[#3d4466] hover:text-[#94a3b8] hover:bg-[#1c2030] transition-colors"
            title={expanded ? 'Ocultar historial' : 'Ver historial'}
          >
            {expanded ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
          </button>
        </div>
      </div>

      {/* History */}
      {expanded && (
        <div className="border-t border-[#1c2030]">
          {/* Recurring base info */}
          {item.recurring && (
            <div className="flex items-center justify-between px-4 py-2 bg-emerald-500/5 border-b border-[#1c2030]">
              <div className="flex items-center gap-1.5 text-xs text-emerald-400">
                <RefreshCw size={10} />
                <span>Monto base (todos los meses)</span>
              </div>
              <span className="text-xs font-mono text-emerald-400 font-semibold">
                {formatAmount(item.baseAmount || 0)}
              </span>
            </div>
          )}

          {history.length > 0 ? (
            <>
              <p className="px-4 pt-2 pb-1 text-[10px] font-semibold text-[#3d4466] uppercase tracking-wider">
                {item.recurring ? 'Overrides por mes' : 'Historial'}
              </p>
              {history.map((m) => (
                <HistoryRow
                  key={m.month}
                  monthData={m}
                  onEdit={(month, amt, note) => setFixedExpenseMonth(item.id, month, amt, note)}
                />
              ))}
            </>
          ) : (
            <p className="px-4 py-3 text-xs text-[#3d4466]">
              {item.recurring
                ? 'Sin overrides puntuales. Todos los meses usan el monto base.'
                : 'Sin datos históricos.'}
            </p>
          )}
        </div>
      )}
    </div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────
export default function FixedExpensesPage() {
  const fixedExpenses        = useStore((s) => s.fixedExpenses)
  const deleteFixedExpense   = useStore((s) => s.deleteFixedExpense)
  const getFixedExpenseTotal = useStore((s) => s.getFixedExpenseTotal)
  const currentMonth         = useStore((s) => s.currentMonth)

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing]   = useState(null)

  const total = getFixedExpenseTotal(currentMonth)

  const handleEdit = (item) => {
    setEditing(item)
    setFormOpen(true)
  }

  const handleDelete = async (id) => {
    if (!window.confirm('¿Eliminar esta reserva?')) return
    const result = await deleteFixedExpense(id)
    if (result.error) window.alert(result.error)
  }

  const handleFormClose = () => {
    setFormOpen(false)
    setEditing(null)
  }

  return (
    <div className="p-6 max-w-2xl">
      {/* Header */}
      <div className="flex items-start justify-between mb-6">
        <div>
          <h2 className="text-xl font-bold text-white mb-1">Reservas mensuales</h2>
          <p className="text-sm text-[#64748b]">
            Dinero apartado al inicio del mes —{' '}
            <span className="text-blue-400">{formatMonthLong(currentMonth)}</span>
          </p>
        </div>
        <button
          onClick={() => { setEditing(null); setFormOpen(true) }}
          className="flex items-center gap-2 px-3 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors shrink-0"
        >
          <Plus size={15} />
          Agregar
        </button>
      </div>

      {/* Info callout */}
      <div className="flex items-start gap-3 bg-[#1c2030] border border-[#2e3350] rounded-xl p-3.5 mb-6">
        <PiggyBank size={16} className="text-blue-400 shrink-0 mt-0.5" />
        <p className="text-xs text-[#94a3b8] leading-relaxed">
          Estas reservas no son gastos consumidos, sino{' '}
          <span className="text-white">dinero que se aparta</span> al comienzo del mes.
          Las marcadas con <span className="text-emerald-400">🔄 Periódica</span> repiten el mismo monto base
          todos los meses — podés sobreescribir un mes en particular o cambiar el monto base para todos.
        </p>
      </div>

      {/* List */}
      {fixedExpenses.length === 0 ? (
        <div className="text-center py-16 text-[#3d4466]">
          <p className="text-sm mb-2">No hay reservas configuradas.</p>
          <button
            onClick={() => setFormOpen(true)}
            className="text-blue-400 hover:text-blue-300 text-sm"
          >
            Agregar la primera →
          </button>
        </div>
      ) : (
        <div className="space-y-3">
          {fixedExpenses.map((item) => (
            <ReserveCard
              key={item.id}
              item={item}
              currentMonth={currentMonth}
              onEdit={handleEdit}
              onDelete={handleDelete}
            />
          ))}

          {/* Total */}
          <div className="flex items-center justify-between px-4 py-3 bg-[#151820] border border-[#2e3350] rounded-xl">
            <span className="text-sm font-bold text-[#94a3b8] uppercase tracking-wider">
              Total reservas — {formatMonthLong(currentMonth)}
            </span>
            <span className="text-lg font-bold font-mono text-white">
              {formatAmount(total)}
            </span>
          </div>
        </div>
      )}

      {/* Form SlideOver */}
      <FixedExpenseForm
        open={formOpen}
        onClose={handleFormClose}
        item={editing}
      />
    </div>
  )
}
