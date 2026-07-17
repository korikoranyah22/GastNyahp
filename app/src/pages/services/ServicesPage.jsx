import { useState } from 'react'
import { Plus, Pencil, Trash2, ChevronDown, ChevronUp, Zap, Check } from 'lucide-react'
import useStore from '../../store/useStore'
import ServiceForm, { SERVICE_CATEGORIES } from './ServiceForm'
import EmptyState from '../../components/ui/EmptyState'
import Badge from '../../components/ui/Badge'
import OwnerBadge, { SHARED_OWNER } from '../../components/ui/OwnerBadge'
import { formatAmount, formatAmountShort, formatMonth } from '../../lib/formatters'
import { addMonthsToYM, generateMonths } from '../../lib/dateUtils'

// Delta vs mes anterior
function Delta({ current, prev }) {
  if (!prev || prev === 0) return null
  const pct = Math.round(((current - prev) / prev) * 100)
  if (pct === 0) return null
  const up = pct > 0
  return (
    <span className={`text-[10px] font-semibold flex items-center gap-0.5 ${up ? 'text-red-400' : 'text-green-400'}`}>
      {up ? '↑' : '↓'}{Math.abs(pct)}%
    </span>
  )
}

function ServiceCard({ svc, currentMonth, onEdit, onDelete }) {
  const creditCards = useStore((s) => s.creditCards)
  const people = useStore((s) => s.people)
  const updateServiceMonthAmount = useStore((s) => s.updateServiceMonthAmount)
  const toggleServiceMonthPaid = useStore((s) => s.toggleServiceMonthPaid)
  const [expanded, setExpanded] = useState(false)
  const [editingMonth, setEditingMonth] = useState(null)
  const [editValue, setEditValue] = useState('')

  const linkedCard = creditCards.find((c) => c.id === svc.linkedCardId)
  const catMeta = SERVICE_CATEGORIES.find((c) => c.value === svc.category) || { icon: '📌' }

  const currentEntry  = svc.amounts.find((a) => a.month === currentMonth)
  const currentAmount = currentEntry?.amount || 0
  const isPaid        = currentEntry?.paid === true
  const prevMonth = addMonthsToYM(currentMonth, -1)
  const prevAmount = svc.amounts.find((a) => a.month === prevMonth)?.amount || 0

  // Historial: 6 meses anteriores + actual + 6 futuros
  const historyMonths = generateMonths(addMonthsToYM(currentMonth, -6), 13)

  const handleAmountSave = async (month) => {
    const v = Number(editValue.replace(/[^\d]/g, ''))
    const result = await updateServiceMonthAmount(svc.id, month, v)
    if (result.error) window.alert(result.error)
    else setEditingMonth(null)
  }

  const handleTogglePaid = async () => {
    const result = await toggleServiceMonthPaid(svc.id, currentMonth)
    if (result.error) window.alert(result.error)
  }

  return (
    <div className={`bg-[#151820] border rounded-xl overflow-hidden transition-colors ${
      svc.active ? 'border-[#2e3350] hover:border-[#3d4466]' : 'border-[#1c2030] opacity-60'
    }`}>
      <div className="p-4">
        {/* Header */}
        <div className="flex items-start justify-between mb-3">
          <div className="flex items-center gap-2 min-w-0">
            <span className="text-xl shrink-0">{catMeta.icon}</span>
            <div className="min-w-0">
              <div className="flex items-center gap-1.5">
                <p className="text-sm font-semibold text-white truncate">{svc.name}</p>
                {svc.ownerId && <OwnerBadge person={svc.ownerId === 'shared' ? SHARED_OWNER : people.find((p) => p.id === svc.ownerId)} size="xs" />}
              </div>
              <p className="text-[10px] text-[#64748b]">{svc.category}</p>
            </div>
          </div>
          {/* Actions */}
          <div className="flex gap-0.5 shrink-0 opacity-0 group-hover:opacity-100">
            <button onClick={onEdit} className="p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#2e3350] transition-colors">
              <Pencil size={12} />
            </button>
            <button onClick={onDelete} className="p-1.5 rounded-lg text-[#64748b] hover:text-red-400 hover:bg-red-500/10 transition-colors">
              <Trash2 size={12} />
            </button>
          </div>
        </div>

        {/* Amount + delta */}
        <div className="flex items-end justify-between mb-3">
          <div>
            {editingMonth === currentMonth ? (
              <div className="flex items-center gap-1">
                <span className="text-xs text-[#64748b]">$</span>
                <input
                  autoFocus
                  type="text"
                  value={editValue}
                  onChange={(e) => setEditValue(e.target.value.replace(/[^\d]/g, ''))}
                  onBlur={() => handleAmountSave(currentMonth)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') handleAmountSave(currentMonth)
                    if (e.key === 'Escape') setEditingMonth(null)
                  }}
                  className="w-28 px-2 py-0.5 bg-[#2e3350] border border-blue-500 rounded text-sm text-white font-bold mono focus:outline-none"
                />
              </div>
            ) : (
              <button
                onClick={() => { setEditingMonth(currentMonth); setEditValue(String(currentAmount)) }}
                className="text-xl font-bold mono text-white hover:text-blue-300 transition-colors"
                title="Click para editar el monto del mes"
              >
                {currentAmount > 0 ? formatAmount(currentAmount) : <span className="text-[#3d4466]">—</span>}
              </button>
            )}
            <p className="text-[10px] text-[#64748b]">{formatMonth(currentMonth)}</p>
          </div>
          <Delta current={currentAmount} prev={prevAmount} />
        </div>

        {/* Linked card badge / directo + pagado */}
        <div className="flex items-center justify-between gap-2">
          {linkedCard ? (
            <span
              className="text-[10px] px-2 py-0.5 rounded font-semibold shrink-0"
              style={{ backgroundColor: `${linkedCard.color}22`, color: linkedCard.color }}
            >
              {linkedCard.label}
            </span>
          ) : (
            <span className="text-[10px] px-2 py-0.5 rounded bg-[#2e3350] text-[#64748b] font-medium shrink-0">Pago directo</span>
          )}

          <div className="flex items-center gap-2 ml-auto">
            {/* Botón pagado */}
            <button
              onClick={handleTogglePaid}
              title={isPaid ? 'Marcar como impago' : 'Marcar como pagado'}
              className={`flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-semibold transition-all ${
                isPaid
                  ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30'
                  : 'border border-[#2e3350] text-[#3d4466] hover:border-[#3d4466] hover:text-[#64748b]'
              }`}
            >
              <Check size={9} strokeWidth={isPaid ? 3 : 2} />
              {isPaid ? 'Pagado' : 'Pagar'}
            </button>

            <button
              onClick={() => setExpanded(!expanded)}
              className="flex items-center gap-0.5 text-[10px] text-[#3d4466] hover:text-[#64748b] transition-colors"
            >
              Historial {expanded ? <ChevronUp size={10} /> : <ChevronDown size={10} />}
            </button>
          </div>
        </div>

        {/* History */}
        {expanded && (
          <div className="mt-3 pt-3 border-t border-[#2e3350] space-y-1 max-h-48 overflow-y-auto">
            {historyMonths.map((m) => {
              const entry = svc.amounts.find((a) => a.month === m)
              const amt = entry?.amount || 0
              const mPaid = entry?.paid === true
              const isCurrent = m === currentMonth
              const isEditing = editingMonth === m
              return (
                <div key={m} className={`flex items-center justify-between py-1 px-2 rounded-md ${isCurrent ? 'bg-blue-500/10' : 'hover:bg-[#1c2030]'}`}>
                  <div className="flex items-center gap-1.5 min-w-0">
                    <span className={`text-[11px] ${isCurrent ? 'text-blue-400 font-semibold' : 'text-[#64748b]'}`}>
                      {formatMonth(m)}{isCurrent ? ' (actual)' : ''}
                    </span>
                    {mPaid && <Check size={9} className="text-emerald-400 shrink-0" strokeWidth={3} />}
                  </div>
                  {isEditing ? (
                    <div className="flex items-center gap-1">
                      <span className="text-[10px] text-[#64748b]">$</span>
                      <input
                        autoFocus
                        type="text"
                        value={editValue}
                        onChange={(e) => setEditValue(e.target.value.replace(/[^\d]/g, ''))}
                        onBlur={() => handleAmountSave(m)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') handleAmountSave(m)
                          if (e.key === 'Escape') setEditingMonth(null)
                        }}
                        className="w-24 px-2 py-0.5 bg-[#2e3350] border border-blue-500 rounded text-[11px] text-white mono focus:outline-none"
                      />
                    </div>
                  ) : (
                    <button
                      onClick={() => { setEditingMonth(m); setEditValue(String(amt)) }}
                      className={`text-[11px] font-semibold mono hover:opacity-80 ${isCurrent ? 'text-blue-300' : amt > 0 ? 'text-white' : 'text-[#2e3350]'}`}
                      title="Click para editar"
                    >
                      {amt > 0 ? formatAmount(amt) : '—'}
                    </button>
                  )}
                </div>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}

// ─── Category group ───────────────────────────────────────────────────────────

function CategoryGroup({ category, services, currentMonth, onEdit, onDelete }) {
  const meta = SERVICE_CATEGORIES.find((c) => c.value === category) || { icon: '📌', label: category }
  const groupTotal = services.reduce((sum, svc) => {
    const a = svc.amounts.find((a) => a.month === currentMonth)?.amount || 0
    return sum + a
  }, 0)

  return (
    <div>
      <div className="flex items-center gap-2 mb-3">
        <span className="text-sm">{meta.icon}</span>
        <h3 className="text-xs font-semibold text-[#64748b] uppercase tracking-wider">{meta.label || category}</h3>
        <div className="flex-1 h-px bg-[#1c2030]" />
        {groupTotal > 0 && (
          <span className="text-xs font-semibold text-[#94a3b8] mono">{formatAmount(groupTotal)}</span>
        )}
      </div>
      <div className="group grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
        {services.map((svc) => (
          <div key={svc.id} className="group">
            <ServiceCard
              svc={svc}
              currentMonth={currentMonth}
              onEdit={() => onEdit(svc)}
              onDelete={() => onDelete(svc)}
            />
          </div>
        ))}
      </div>
    </div>
  )
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function ServicesPage() {
  const services = useStore((s) => s.services)
  const deleteService = useStore((s) => s.deleteService)
  const currentMonth = useStore((s) => s.currentMonth)
  const getIndependentServicesTotal = useStore((s) => s.getIndependentServicesTotal)

  const [formOpen, setFormOpen] = useState(false)
  const [editService, setEditService] = useState(null)

  const handleDelete = async (svc) => {
    if (!window.confirm(`¿Eliminar el servicio "${svc.name}"?`)) return
    const result = await deleteService(svc.id)
    if (result.error) window.alert(result.error)
  }

  const handleEdit = (svc) => { setEditService(svc); setFormOpen(true) }
  const handleClose = () => { setFormOpen(false); setEditService(null) }

  // Total del mes (todos)
  const totalMes = services.reduce((sum, svc) => {
    const a = svc.amounts.find((a) => a.month === currentMonth)?.amount || 0
    return sum + a
  }, 0)

  // Agrupar por categoría, en orden predefinido
  const categoryOrder = SERVICE_CATEGORIES.map((c) => c.value)
  const grouped = categoryOrder
    .map((cat) => ({ cat, svcs: services.filter((s) => s.category === cat) }))
    .filter((g) => g.svcs.length > 0)

  // Categorías no en el orden predefinido
  const otherCats = [...new Set(services.map((s) => s.category))].filter((c) => !categoryOrder.includes(c))
  otherCats.forEach((cat) => grouped.push({ cat, svcs: services.filter((s) => s.category === cat) }))

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-lg font-semibold text-white">Servicios</h2>
          <p className="text-xs text-[#64748b] mt-0.5">{services.length} servicio{services.length !== 1 ? 's' : ''} configurados</p>
        </div>
        <div className="flex items-center gap-3">
          {totalMes > 0 && (
            <div className="px-3 py-1.5 rounded-lg bg-purple-500/10 border border-purple-500/20">
              <p className="text-[10px] text-[#64748b]">Total mes</p>
              <p className="text-sm font-bold text-purple-400 mono">{formatAmount(totalMes)}</p>
            </div>
          )}
          <button
            onClick={() => setFormOpen(true)}
            className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
          >
            <Plus size={15} />
            Nuevo servicio
          </button>
        </div>
      </div>

      {services.length === 0 ? (
        <EmptyState
          icon={Zap}
          title="No hay servicios configurados"
          description="Registrá tus servicios recurrentes: Edesur, internet, suscripciones, expensas, seguros."
          action={
            <button onClick={() => setFormOpen(true)}
              className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium">
              <Plus size={15} />Agregar servicio
            </button>
          }
        />
      ) : (
        <div className="space-y-8">
          {grouped.map(({ cat, svcs }) => (
            <CategoryGroup
              key={cat}
              category={cat}
              services={svcs}
              currentMonth={currentMonth}
              onEdit={handleEdit}
              onDelete={handleDelete}
            />
          ))}
        </div>
      )}

      <ServiceForm open={formOpen} onClose={handleClose} service={editService} />
    </div>
  )
}
