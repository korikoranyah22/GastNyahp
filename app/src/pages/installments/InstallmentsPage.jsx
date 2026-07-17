import { useState, useMemo } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Plus, ArrowLeft, Pencil, Trash2, CheckCircle2, Circle, Filter } from 'lucide-react'
import useStore from '../../store/useStore'
import InstallmentForm from './InstallmentForm'
import EmptyState from '../../components/ui/EmptyState'
import Badge from '../../components/ui/Badge'
import { formatAmount, formatMonth, formatDate } from '../../lib/formatters'
import { generateMonths, addMonthsToYM, compareYM } from '../../lib/dateUtils'

const CATEGORY_COLORS = {
  Hogar:      'bg-blue-500/15 text-blue-400',
  Ropa:       'bg-pink-500/15 text-pink-400',
  Educación:  'bg-purple-500/15 text-purple-400',
  Transporte: 'bg-orange-500/15 text-orange-400',
  Servicios:  'bg-teal-500/15 text-teal-400',
  Perfumes:   'bg-rose-500/15 text-rose-400',
  Higiene:    'bg-cyan-500/15 text-cyan-400',
  Salud:      'bg-green-500/15 text-green-400',
  Electrónica:'bg-indigo-500/15 text-indigo-400',
  Mascotas:   'bg-amber-500/15 text-amber-400',
  Salidas:    'bg-emerald-500/15 text-emerald-400',
  Vicios:     'bg-red-500/15 text-red-400',
  default:    'bg-[#2e3350] text-[#94a3b8]',
}

function getCategoryClass(cat) {
  return CATEGORY_COLORS[cat] || CATEGORY_COLORS.default
}

// Generate the visible month columns (6 past + current + 12 future = 19 columns)
function getColumns(currentMonth) {
  const start = addMonthsToYM(currentMonth, -6)
  return generateMonths(start, 19)
}

export default function InstallmentsPage() {
  const { cardId } = useParams()
  const navigate = useNavigate()
  const currentMonth = useStore((s) => s.currentMonth)
  const creditCards = useStore((s) => s.creditCards)
  const installments = useStore((s) => s.installments)
  const services = useStore((s) => s.services)
  const toggleInstallmentPaid = useStore((s) => s.toggleInstallmentPaid)
  const deleteInstallment = useStore((s) => s.deleteInstallment)

  const [formOpen, setFormOpen] = useState(false)
  const [editInst, setEditInst] = useState(null)
  const [showAll, setShowAll] = useState(false)

  const card = creditCards.find((c) => c.id === cardId)
  const bank = useStore((s) => s.banks.find((b) => b.id === card?.bankId))

  const columns = useMemo(() => getColumns(currentMonth), [currentMonth])

  // Filter installments for this card
  const cardInst = installments.filter((i) => i.cardId === cardId)
  const visibleInst = showAll
    ? cardInst
    : cardInst.filter((i) => i.active !== false)

  // Only show rows that have at least one amount in our visible columns
  const filteredInst = visibleInst.filter((inst) =>
    columns.some((col) => inst.months.find((m) => m.month === col && m.amount > 0))
  )

  // Services vinculados a esta tarjeta
  const cardServices = services.filter((s) => s.linkedCardId === cardId && s.active !== false)

  // Total per column: installments + services
  const columnTotals = columns.map((col) => {
    const instSum = filteredInst.reduce((sum, inst) => {
      const m = inst.months.find((m) => m.month === col)
      return sum + (m ? m.amount : 0)
    }, 0)
    const svcSum = cardServices.reduce((sum, svc) => {
      const a = svc.amounts.find((a) => a.month === col)
      return sum + (a ? a.amount : 0)
    }, 0)
    return instSum + svcSum
  })

  const grandTotal = columnTotals[columns.indexOf(currentMonth)] || 0

  if (!card) return (
    <div className="p-6 text-[#64748b]">Tarjeta no encontrada.</div>
  )

  const handleDelete = async (inst) => {
    if (!window.confirm(`¿Eliminar la cuota "${inst.description}"?`)) return
    const result = await deleteInstallment(inst.id)
    if (result.error) window.alert(result.error)
  }

  return (
    <div className="flex flex-col h-full">
      {/* Sub-header */}
      <div className="px-6 py-4 border-b border-[#1c2030] flex items-center justify-between bg-[#0d0f14] shrink-0">
        <div className="flex items-center gap-3">
          <button
            onClick={() => navigate('/cards')}
            className="p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#1c2030] transition-colors"
          >
            <ArrowLeft size={16} />
          </button>
          {/* Card chip */}
          <div
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg"
            style={{ backgroundColor: `${card.color}22`, border: `1px solid ${card.color}44` }}
          >
            <span className="w-2 h-2 rounded-full" style={{ backgroundColor: card.color }} />
            <span className="text-sm font-semibold" style={{ color: card.color }}>{card.label}</span>
            {bank && <span className="text-xs text-[#64748b]">· {bank.name}</span>}
          </div>
          <Badge variant="gray">{filteredInst.length} cuota{filteredInst.length !== 1 ? 's' : ''}</Badge>
        </div>
        <div className="flex items-center gap-2">
          {/* Month total badge */}
          {grandTotal > 0 && (
            <div className="px-3 py-1.5 rounded-lg bg-blue-600/15 border border-blue-600/20">
              <p className="text-xs text-[#64748b]">Este mes</p>
              <p className="text-sm font-bold text-blue-400 mono">{formatAmount(grandTotal)}</p>
            </div>
          )}
          <button
            onClick={() => setShowAll(!showAll)}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors ${
              showAll ? 'bg-blue-600/20 text-blue-400 border-blue-600/20' : 'text-[#64748b] border-[#2e3350] hover:text-white'
            }`}
          >
            <Filter size={12} />
            {showAll ? 'Todas' : 'Activas'}
          </button>
          <button
            onClick={() => { setEditInst(null); setFormOpen(true) }}
            className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
          >
            <Plus size={15} />
            Nueva cuota
          </button>
        </div>
      </div>

      {filteredInst.length === 0 ? (
        <div className="flex-1 flex items-center justify-center">
          <EmptyState
            icon={CheckCircle2}
            title="No hay cuotas para esta tarjeta"
            description="Registrá las compras en cuotas o servicios recurrentes."
            action={
              <button
                onClick={() => setFormOpen(true)}
                className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium"
              >
                <Plus size={15} />
                Agregar cuota
              </button>
            }
          />
        </div>
      ) : (
        /* MonthGrid */
        <div
          className="flex-1 overflow-auto"
          onWheel={(e) => {
            if (Math.abs(e.deltaY) > Math.abs(e.deltaX)) {
              e.currentTarget.scrollLeft += e.deltaY
              e.preventDefault()
            }
          }}
        >
          <table className="w-full border-collapse">
            <thead>
              <tr className="sticky top-0 z-20 bg-[#0d0f14]">
                {/* Description col (sticky left) */}
                <th className="sticky left-0 z-30 bg-[#0d0f14] border-b border-r border-[#1c2030] px-4 py-3 text-left w-40 sm:w-64 shrink-0">
                  <span className="text-xs font-semibold text-[#64748b] uppercase tracking-wider">Descripción</span>
                </th>
                {columns.map((col) => {
                  const isCurrent = col === currentMonth
                  const isPast = col < currentMonth
                  return (
                    <th
                      key={col}
                      className={`px-2 sm:px-2 sm:px-3 py-3 text-center min-w-[80px] sm:min-w-[110px] border-b border-[#1c2030] ${
                        isCurrent
                          ? 'border-b-2 border-b-blue-500'
                          : ''
                      }`}
                    >
                      <span className={`text-xs font-semibold uppercase tracking-wider ${
                        isCurrent ? 'text-blue-400' : isPast ? 'text-[#4a5568]' : 'text-[#64748b]'
                      }`}>
                        {formatMonth(col)}
                      </span>
                    </th>
                  )
                })}
              </tr>
            </thead>
            <tbody>
              {filteredInst.map((inst) => (
                <tr key={inst.id} className="group border-b border-[#1c2030] bg-[#151820] hover:bg-[#1a1f2e] transition-colors">
                  {/* Description cell (sticky left) */}
                  <td className="sticky left-0 z-10 bg-[#151820] group-hover:bg-[#1a1f2e] border-r border-[#1c2030] px-4 py-3 w-40 sm:w-64">
                    <div className="flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-white truncate">{inst.description}</p>
                        <div className="flex items-center gap-1.5 mt-0.5">
                          <span className={`text-[10px] px-1.5 py-0.5 rounded font-medium ${getCategoryClass(inst.category)}`}>
                            {inst.category}
                          </span>
                          {inst.frequency === 'monthly' && (
                            <span className="text-[10px] text-[#64748b]">∞ mensual</span>
                          )}
                          {inst.frequency === 'fixed' && (
                            <span className="text-[10px] text-[#64748b]">
                              {inst.totalInstallments} cuotas
                            </span>
                          )}
                        </div>
                      </div>
                      {/* Row actions */}
                      <div className="flex gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
                        <button
                          onClick={() => { setEditInst(inst); setFormOpen(true) }}
                          className="p-1 rounded text-[#64748b] hover:text-white hover:bg-[#2e3350] transition-colors"
                        >
                          <Pencil size={11} />
                        </button>
                        <button
                          onClick={() => handleDelete(inst)}
                          className="p-1 rounded text-[#64748b] hover:text-red-400 hover:bg-red-500/10 transition-colors"
                        >
                          <Trash2 size={11} />
                        </button>
                      </div>
                    </div>
                  </td>

                  {/* Month cells */}
                  {columns.map((col) => {
                    const monthData = inst.months.find((m) => m.month === col)
                    const isCurrent = col === currentMonth
                    const isPast = col < currentMonth
                    const hasCuota = monthData && monthData.amount > 0

                    if (!hasCuota) {
                      return (
                        <td key={col} className={`px-2 sm:px-3 py-3 text-center ${isCurrent ? 'bg-blue-500/5' : ''}`}>
                          <span className="text-[#1c2030]">—</span>
                        </td>
                      )
                    }

                    const paid = monthData.paid

                    return (
                      <td
                        key={col}
                        className={`px-2 sm:px-3 py-3 text-center cursor-pointer transition-colors ${
                          isCurrent
                            ? paid
                              ? 'bg-green-500/10 hover:bg-green-500/20'
                              : 'bg-blue-500/5 hover:bg-blue-500/10'
                            : isPast
                              ? paid
                                ? 'hover:bg-[#1c2030]'
                                : 'bg-red-500/5 hover:bg-red-500/10'
                              : 'hover:bg-[#1c2030]'
                        }`}
                        onClick={async () => {
                          const result = await toggleInstallmentPaid(inst.id, col)
                          if (result.error) window.alert(result.error)
                        }}
                        title={`${paid ? 'Marcar como pendiente' : 'Marcar como pagada'}`}
                      >
                        <div className="flex flex-col items-center gap-0.5">
                          <span className={`text-xs font-semibold mono ${
                            paid
                              ? 'text-green-400'
                              : isCurrent
                                ? 'text-blue-300'
                                : isPast
                                  ? 'text-red-400'
                                  : 'text-[#94a3b8]'
                          }`}>
                            {formatAmount(monthData.amount)}
                          </span>
                          {paid ? (
                            <CheckCircle2 size={10} className="text-green-500" />
                          ) : (
                            isCurrent && <Circle size={10} className="text-blue-500" />
                          )}
                        </div>
                      </td>
                    )
                  })}
                </tr>
              ))}

              {/* ── Services rows ──────────────────────────────────────────── */}
              {cardServices.length > 0 && (
                <>
                  <tr>
                    <td colSpan={columns.length + 1} className="sticky left-0 px-4 py-1.5 bg-[#0d0f14] border-t border-[#2e3350]">
                      <span className="text-[10px] font-semibold text-teal-500 uppercase tracking-wider">Servicios en tarjeta</span>
                    </td>
                  </tr>
                  {cardServices.map((svc) => (
                    <tr key={svc.id} className="border-b border-[#1c2030] bg-[#151820] hover:bg-[#1a1f2e] transition-colors">
                      <td className="sticky left-0 z-10 bg-[#151820] hover:bg-[#1a1f2e] border-r border-[#1c2030] px-4 py-2.5 w-40 sm:w-64">
                        <div>
                          <p className="text-sm font-medium text-white truncate">{svc.name}</p>
                          <div className="flex items-center gap-1.5 mt-0.5">
                            <span className="text-[10px] px-1.5 py-0.5 rounded font-medium bg-teal-500/15 text-teal-400">
                              {svc.category}
                            </span>
                            <span className="text-[10px] text-teal-600 font-semibold">[Svc]</span>
                            <span className="text-[10px] text-[#64748b]">∞ mensual</span>
                          </div>
                        </div>
                      </td>
                      {columns.map((col) => {
                        const amountData = svc.amounts.find((a) => a.month === col)
                        const amount = amountData?.amount || 0
                        const isCurrent = col === currentMonth
                        return (
                          <td key={col} className={`px-3 py-2.5 text-center ${isCurrent ? 'bg-blue-500/5' : ''}`}>
                            {amount > 0 ? (
                              <span className={`text-xs font-semibold mono ${isCurrent ? 'text-teal-300' : 'text-[#94a3b8]'}`}>
                                {formatAmount(amount)}
                              </span>
                            ) : (
                              <span className="text-[#1c2030]">—</span>
                            )}
                          </td>
                        )
                      })}
                    </tr>
                  ))}
                </>
              )}

              {/* TOTAL row */}
              <tr className="sticky bottom-0 z-10 bg-[#0d0f14] border-t-2 border-[#2e3350]">
                <td className="sticky left-0 z-20 bg-[#0d0f14] border-r border-[#2e3350] px-4 py-3">
                  <span className="text-xs font-bold text-[#94a3b8] uppercase tracking-wider">Total</span>
                </td>
                {columnTotals.map((total, i) => {
                  const col = columns[i]
                  const isCurrent = col === currentMonth
                  return (
                    <td key={col} className={`px-2 sm:px-3 py-3 text-center ${isCurrent ? 'bg-blue-500/5' : ''}`}>
                      <span className={`text-xs font-bold mono ${
                        total > 0
                          ? isCurrent ? 'text-blue-400' : 'text-[#94a3b8]'
                          : 'text-[#2e3350]'
                      }`}>
                        {total > 0 ? formatAmount(total) : '—'}
                      </span>
                    </td>
                  )
                })}
              </tr>
            </tbody>
          </table>
        </div>
      )}

      <InstallmentForm
        open={formOpen}
        onClose={() => { setFormOpen(false); setEditInst(null) }}
        cardId={cardId}
        installment={editInst}
      />
    </div>
  )
}
