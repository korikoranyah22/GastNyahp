import { useState } from 'react'
import { Plus, ChevronDown, ChevronUp, Pencil, Trash2, Landmark, CheckCircle2, Circle } from 'lucide-react'
import useStore from '../../store/useStore'
import LoanForm from './LoanForm'
import EmptyState from '../../components/ui/EmptyState'
import Badge from '../../components/ui/Badge'
import { formatAmount, formatMonth, formatAmountShort } from '../../lib/formatters'

function ProgressBar({ paid, total, color }) {
  const pct = total > 0 ? Math.round((paid / total) * 100) : 0
  return (
    <div className="w-full bg-[#1c2030] rounded-full h-1.5">
      <div
        className="h-1.5 rounded-full transition-all"
        style={{ width: `${pct}%`, backgroundColor: color || '#f97316' }}
      />
    </div>
  )
}

function LoanCard({ loan, bank, currentMonth }) {
  const [expanded, setExpanded] = useState(false)
  const toggleLoanPaid = useStore((s) => s.toggleLoanPaid)
  const updateLoanMonthAmount = useStore((s) => s.updateLoanMonthAmount)
  const deleteLoan = useStore((s) => s.deleteLoan)
  const updateLoan = useStore((s) => s.updateLoan)
  const [editOpen, setEditOpen] = useState(false)
  const [editingCell, setEditingCell] = useState(null)
  const [editValue, setEditValue] = useState('')

  const paidCount = loan.months.filter((m) => m.paid).length
  const totalCount = loan.months.length
  const currentMonthData = loan.months.find((m) => m.month === currentMonth)
  const remainingAmount = loan.months
    .filter((m) => !m.paid)
    .reduce((sum, m) => sum + m.amount, 0)

  const bankColor = bank?.color || '#f97316'

  const handleCellEdit = (month, currentAmount) => {
    setEditingCell(month)
    setEditValue(String(currentAmount))
  }

  const handleCellSave = async (loanId, month) => {
    const amount = Number(editValue.replace(/[^\d]/g, ''))
    if (amount <= 0) return
    const result = await updateLoanMonthAmount(loanId, month, amount)
    if (result.error) window.alert(result.error)
    else setEditingCell(null)
  }

  return (
    <div className="bg-[#151820] border border-[#2e3350] rounded-xl overflow-hidden">
      {/* Color top bar */}
      <div className="h-1" style={{ backgroundColor: bankColor }} />

      <div className="p-5">
        <div className="flex items-start justify-between mb-4">
          <div className="flex items-center gap-3">
            <div
              className="w-9 h-9 rounded-lg flex items-center justify-center"
              style={{ backgroundColor: `${bankColor}22` }}
            >
              <Landmark size={16} style={{ color: bankColor }} />
            </div>
            <div>
              <p className="text-sm font-semibold text-white">{loan.description}</p>
              {bank && <p className="text-xs text-[#64748b]">{bank.name}</p>}
            </div>
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={() => setEditOpen(true)}
              className="p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#2e3350] transition-colors"
            >
              <Pencil size={13} />
            </button>
            <button
              onClick={async () => {
                if (!window.confirm(`¿Eliminar "${loan.description}"?`)) return
                const result = await deleteLoan(loan.id)
                if (result.error) window.alert(result.error)
              }}
              className="p-1.5 rounded-lg text-[#64748b] hover:text-red-400 hover:bg-red-500/10 transition-colors"
            >
              <Trash2 size={13} />
            </button>
          </div>
        </div>

        {/* Progress */}
        <div className="mb-4">
          <div className="flex justify-between mb-1.5">
            <span className="text-xs text-[#64748b]">{paidCount} de {totalCount} cuotas pagadas</span>
            <span className="text-xs font-medium" style={{ color: bankColor }}>
              {totalCount > 0 ? Math.round((paidCount / totalCount) * 100) : 0}%
            </span>
          </div>
          <ProgressBar paid={paidCount} total={totalCount} color={bankColor} />
        </div>

        {/* Stats row */}
        <div className="grid grid-cols-3 gap-3">
          <div className="bg-[#1c2030] rounded-lg p-2.5 text-center">
            <p className="text-[10px] text-[#64748b] mb-0.5">Este mes</p>
            <p className={`text-sm font-bold mono ${currentMonthData ? (currentMonthData.paid ? 'text-green-400' : 'text-white') : 'text-[#3d4466]'}`}>
              {currentMonthData ? formatAmountShort(currentMonthData.amount) : '—'}
            </p>
            {currentMonthData && (
              <p className="text-[10px] mt-0.5">
                {currentMonthData.paid
                  ? <span className="text-green-500">✓ Pagada</span>
                  : <span className="text-yellow-500">Pendiente</span>
                }
              </p>
            )}
          </div>
          <div className="bg-[#1c2030] rounded-lg p-2.5 text-center">
            <p className="text-[10px] text-[#64748b] mb-0.5">Restante</p>
            <p className="text-sm font-bold mono text-orange-400">{formatAmountShort(remainingAmount)}</p>
          </div>
          <div className="bg-[#1c2030] rounded-lg p-2.5 text-center">
            <p className="text-[10px] text-[#64748b] mb-0.5">Cuotas rest.</p>
            <p className="text-sm font-bold text-white">{totalCount - paidCount}</p>
          </div>
        </div>

        {/* Expand toggle */}
        <button
          onClick={() => setExpanded(!expanded)}
          className="flex items-center gap-1.5 mt-4 text-xs text-[#64748b] hover:text-white transition-colors w-full"
        >
          {expanded ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
          {expanded ? 'Ocultar detalle por mes' : 'Ver detalle por mes'}
        </button>

        {/* Month detail */}
        {expanded && (
          <div className="mt-3 border-t border-[#2e3350] pt-3">
            <div className="space-y-1 max-h-64 overflow-y-auto pr-1">
              {loan.months.map((m) => {
                const isCurrent = m.month === currentMonth
                const isPast = m.month < currentMonth
                const isEditing = editingCell === m.month

                return (
                  <div
                    key={m.month}
                    className={`flex items-center justify-between px-3 py-2 rounded-lg transition-colors ${
                      isCurrent
                        ? 'bg-blue-500/10 border border-blue-500/20'
                        : m.paid
                          ? 'bg-green-500/5'
                          : isPast
                            ? 'bg-red-500/5'
                            : 'hover:bg-[#1c2030]'
                    }`}
                  >
                    <div className="flex items-center gap-2">
                      <button
                        onClick={async () => {
                          const result = await toggleLoanPaid(loan.id, m.month)
                          if (result.error) window.alert(result.error)
                        }}
                        className="text-[#64748b] hover:text-white transition-colors"
                      >
                        {m.paid
                          ? <CheckCircle2 size={14} className="text-green-500" />
                          : <Circle size={14} className={isCurrent ? 'text-blue-400' : isPast ? 'text-red-400' : ''} />
                        }
                      </button>
                      <span className={`text-xs ${isCurrent ? 'text-blue-400 font-semibold' : 'text-[#94a3b8]'}`}>
                        {formatMonth(m.month)}
                        {isCurrent && ' (actual)'}
                      </span>
                    </div>

                    {/* Editable amount */}
                    {isEditing ? (
                      <div className="flex items-center gap-1">
                        <span className="text-xs text-[#64748b]">$</span>
                        <input
                          autoFocus
                          type="text"
                          value={editValue}
                          onChange={(e) => setEditValue(e.target.value.replace(/[^\d]/g, ''))}
                          onBlur={() => handleCellSave(loan.id, m.month)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter') handleCellSave(loan.id, m.month)
                            if (e.key === 'Escape') setEditingCell(null)
                          }}
                          className="w-24 px-2 py-0.5 bg-[#2e3350] border border-blue-500 rounded text-xs text-white focus:outline-none mono"
                        />
                      </div>
                    ) : (
                      <button
                        onClick={() => handleCellEdit(m.month, m.amount)}
                        className={`text-xs font-semibold mono hover:opacity-80 transition-opacity ${
                          m.paid
                            ? 'text-green-400'
                            : isCurrent
                              ? 'text-blue-300'
                              : isPast
                                ? 'text-red-400'
                                : 'text-[#94a3b8]'
                        }`}
                        title="Click para editar el monto"
                      >
                        {formatAmount(m.amount)}
                      </button>
                    )}
                  </div>
                )
              })}
            </div>
          </div>
        )}
      </div>

      <LoanForm open={editOpen} onClose={() => setEditOpen(false)} loan={loan} />
    </div>
  )
}

export default function LoansPage() {
  const banks = useStore((s) => s.banks)
  const loans = useStore((s) => s.loans)
  const currentMonth = useStore((s) => s.currentMonth)
  const [formOpen, setFormOpen] = useState(false)

  const getBank = (bankId) => banks.find((b) => b.id === bankId)

  // Total loans for current month
  const monthTotal = loans.reduce((sum, loan) => {
    const m = loan.months.find((m) => m.month === currentMonth)
    return sum + (m ? m.amount : 0)
  }, 0)

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-lg font-semibold text-white">Préstamos</h2>
          <p className="text-xs text-[#64748b] mt-0.5">{loans.length} préstamo{loans.length !== 1 ? 's' : ''} activos</p>
        </div>
        <div className="flex items-center gap-3">
          {monthTotal > 0 && (
            <div className="px-3 py-1.5 rounded-lg bg-orange-500/10 border border-orange-500/20">
              <p className="text-[10px] text-[#64748b]">Este mes</p>
              <p className="text-sm font-bold text-orange-400 mono">{formatAmount(monthTotal)}</p>
            </div>
          )}
          <button
            onClick={() => setFormOpen(true)}
            className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
          >
            <Plus size={15} />
            Nuevo préstamo
          </button>
        </div>
      </div>

      {loans.length === 0 ? (
        <EmptyState
          icon={Landmark}
          title="No hay préstamos registrados"
          description="Registrá tus préstamos bancarios para hacer seguimiento de las cuotas."
          action={
            <button
              onClick={() => setFormOpen(true)}
              className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium"
            >
              <Plus size={15} />
              Agregar préstamo
            </button>
          }
        />
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-3 gap-4">
          {loans.map((loan) => (
            <LoanCard
              key={loan.id}
              loan={loan}
              bank={getBank(loan.bankId)}
              currentMonth={currentMonth}
            />
          ))}
        </div>
      )}

      <LoanForm open={formOpen} onClose={() => setFormOpen(false)} />
    </div>
  )
}
