import { useState, useMemo } from 'react'
import { Plus, Target, Pencil, Trash2, ChevronDown, ChevronUp, ShoppingCart, Search, X, Receipt } from 'lucide-react'
import useStore from '../../store/useStore'
import { formatAmount, formatMonth, formatMonthLong } from '../../lib/formatters'
import ExpenseForm from './ExpenseForm'
import TicketForm from './TicketForm'
import DraftsPanel from './DraftsPanel'
import OwnerBadge, { SHARED_OWNER } from '../../components/ui/OwnerBadge'
import BudgetModal from './BudgetModal'
import {
  EXPENSE_CATEGORIES,
  WEEK_RANGES,
  getCategoryIcon,
  getPaymentLabel,
  getPaymentColor,
  getPaymentMonth,
  getExpenseAmount,
  isCredit,
  filterByWeek,
  formatDay,
} from './expensesConfig'

// ─── Budget progress bar ───────────────────────────────────────────────────────
function BudgetBar({ label, spent, limit, color }) {
  if (!limit) return null
  const pct = Math.min(100, Math.round((spent / limit) * 100))
  const barColor = pct >= 90 ? '#ef4444' : pct >= 70 ? '#f59e0b' : color
  return (
    <div className="flex items-center gap-3 min-w-0">
      <span className="text-[10px] text-[#64748b] w-20 shrink-0">{label}</span>
      <div className="flex-1 h-1.5 bg-[#1c2030] rounded-full overflow-hidden">
        <div
          className="h-full rounded-full transition-all"
          style={{ width: `${pct}%`, backgroundColor: barColor }}
        />
      </div>
      <span className="text-[10px] mono text-[#94a3b8] shrink-0 w-28 text-right">
        {formatAmount(spent)} / {formatAmount(limit)}
      </span>
      <span className={`text-[10px] font-bold w-8 text-right shrink-0 ${
        pct >= 90 ? 'text-red-400' : pct >= 70 ? 'text-amber-400' : 'text-[#64748b]'
      }`}>
        {pct}%
      </span>
    </div>
  )
}

// ─── Expense row ───────────────────────────────────────────────────────────────
function ExpenseRow({ expense, onEdit, onDelete, creditCards, banks, people }) {
  const label = getPaymentLabel(expense.paymentMethod, creditCards, banks)
  const color = getPaymentColor(expense.paymentMethod, creditCards, banks)
  const icon  = getCategoryIcon(expense.category)

  // Indicador de pago: solo si el mes de pago difiere del mes calendario del gasto
  const card = creditCards.find((c) => c.id === expense.paymentMethod)
  const payMonth = card?.closingDay ? getPaymentMonth(expense.date, card.closingDay, card.dueDay) : null
  const showBillingHint = payMonth && payMonth !== expense.date.slice(0, 7)

  const owner = expense.ownerId === 'shared' ? SHARED_OWNER : expense.ownerId ? people.find((p) => p.id === expense.ownerId) : null

  return (
    <div className="group flex items-center gap-3 px-4 py-2.5 hover:bg-[#151820] transition-colors border-b border-[#1c2030]/60 last:border-0">
      <span className="text-base shrink-0">{icon}</span>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5">
          <p className="text-sm text-white truncate">{expense.description}</p>
          {owner && <OwnerBadge person={owner} size="xs" />}
        </div>
        <div className="flex items-center gap-1.5 mt-0.5 flex-wrap">
          <span className="text-[10px] text-[#64748b]">{expense.category}</span>
          <span className="text-[10px] text-[#4a5568]">·</span>
          <span
            className="text-[10px] font-medium px-1.5 py-0.5 rounded"
            style={{ color, backgroundColor: `${color}22` }}
          >
            {label}
          </span>
          {showBillingHint && (
            <span className="text-[10px] text-amber-400/80 font-medium">
              ↳ pago {formatMonth(payMonth)}
            </span>
          )}
        </div>
      </div>
      <div className="flex flex-col items-end shrink-0">
        <span className="text-sm font-semibold mono text-white">
          {formatAmount(expense.amount)}
        </span>
        {expense.originalCurrency === 'USD' && (
          <span className="text-[10px] text-emerald-400/80 font-medium">
            USD {expense.originalAmount?.toLocaleString('es-AR', { maximumFractionDigits: 2 })}
          </span>
        )}
      </div>
      <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
        <button
          onClick={() => onEdit(expense)}
          className="p-1 rounded text-[#64748b] hover:text-white hover:bg-[#2e3350] transition-colors"
        >
          <Pencil size={11} />
        </button>
        <button
          onClick={() => onDelete(expense.id)}
          className="p-1 rounded text-[#64748b] hover:text-red-400 hover:bg-red-500/10 transition-colors"
        >
          <Trash2 size={11} />
        </button>
      </div>
    </div>
  )
}

// ─── Ticket row ────────────────────────────────────────────────────────────────
function TicketRow({ ticket, onEdit, onDelete, creditCards, banks, people }) {
  const [expanded, setExpanded] = useState(false)

  const label = getPaymentLabel(ticket.paymentMethod, creditCards, banks)
  const color = getPaymentColor(ticket.paymentMethod, creditCards, banks)

  const card           = creditCards.find((c) => c.id === ticket.paymentMethod)
  const payMonth       = card?.closingDay ? getPaymentMonth(ticket.date, card.closingDay, card.dueDay) : null
  const showBillingHint = payMonth && payMonth !== ticket.date.slice(0, 7)

  const subtotal    = (ticket.items || []).reduce((s, it) => s + (it.amount || 0), 0)
  const discountAmt = ticket.discount || 0
  const total       = Math.max(0, subtotal - discountAmt)

  return (
    <div className="border-b border-[#1c2030]/60 last:border-0">
      {/* Header */}
      <div className="group flex items-center gap-3 px-4 py-2.5 hover:bg-[#151820] transition-colors">
        <button
          type="button"
          onClick={() => setExpanded(!expanded)}
          className="text-[#3d4466] hover:text-[#94a3b8] transition-colors shrink-0"
        >
          {expanded ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
        </button>
        <span className="text-base shrink-0">🧾</span>
        <div className="flex-1 min-w-0">
          <p className="text-sm text-white truncate">{ticket.description}</p>
          <div className="flex items-center gap-1.5 mt-0.5 flex-wrap">
            <span className="text-[10px] text-[#64748b]">
              {(ticket.items || []).length} {(ticket.items || []).length === 1 ? 'ítem' : 'ítems'}
            </span>
            <span className="text-[10px] text-[#4a5568]">·</span>
            <span
              className="text-[10px] font-medium px-1.5 py-0.5 rounded"
              style={{ color, backgroundColor: `${color}22` }}
            >
              {label}
            </span>
            {showBillingHint && (
              <span className="text-[10px] text-amber-400/80 font-medium">
                ↳ pago {formatMonth(payMonth)}
              </span>
            )}
          </div>
        </div>
        <div className="flex flex-col items-end shrink-0">
          <span className="text-sm font-semibold mono text-white">{formatAmount(total)}</span>
          {discountAmt > 0 && (
            <span className="text-[10px] text-green-400/70">desc. {formatAmount(discountAmt)}</span>
          )}
        </div>
        <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
          <button
            onClick={() => onEdit(ticket)}
            className="p-1 rounded text-[#64748b] hover:text-white hover:bg-[#2e3350] transition-colors"
          >
            <Pencil size={11} />
          </button>
          <button
            onClick={() => onDelete(ticket.id)}
            className="p-1 rounded text-[#64748b] hover:text-red-400 hover:bg-red-500/10 transition-colors"
          >
            <Trash2 size={11} />
          </button>
        </div>
      </div>

      {/* Expanded items */}
      {expanded && (
        <div className="bg-[#0d0f14]/40 border-t border-[#1c2030]">
          {(ticket.items || []).map((item) => {
            const owner = item.ownerId === 'shared'
              ? SHARED_OWNER
              : item.ownerId
                ? people.find((p) => p.id === item.ownerId)
                : null
            return (
              <div
                key={item.id}
                className="flex items-center gap-3 px-4 py-2 border-b border-[#1c2030]/40 last:border-0 pl-14"
              >
                <span className="text-sm shrink-0">{getCategoryIcon(item.category)}</span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-1.5">
                    <p className="text-xs text-[#94a3b8] truncate">{item.description}</p>
                    {owner && <OwnerBadge person={owner} size="xs" />}
                  </div>
                  <span className="text-[10px] text-[#3d4466]">{item.category}</span>
                </div>
                <span className="text-xs mono text-[#94a3b8] shrink-0">{formatAmount(item.amount)}</span>
              </div>
            )
          })}
          {discountAmt > 0 && (
            <div className="flex items-center justify-between px-4 py-2 border-t border-[#1c2030]/40 pl-14">
              <span className="text-[11px] text-green-400/60">Descuento</span>
              <span className="text-[11px] mono text-green-400/60">- {formatAmount(discountAmt)}</span>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ─── Day group ─────────────────────────────────────────────────────────────────
function DayGroup({ date, expenses, creditCards, banks, people, onEdit, onDelete }) {
  const [open, setOpen] = useState(true)
  const dayTotal = expenses.reduce((s, e) => s + getExpenseAmount(e), 0)

  return (
    <div className="border-b border-[#1c2030] last:border-0">
      <div
        className="flex items-center justify-between px-4 py-2 cursor-pointer hover:bg-[#1c2030]/50 transition-colors"
        onClick={() => setOpen(!open)}
      >
        <div className="flex items-center gap-2">
          {open ? <ChevronUp size={13} className="text-[#3d4466]" /> : <ChevronDown size={13} className="text-[#3d4466]" />}
          <span className="text-xs font-medium text-[#94a3b8]">{formatDay(date)}</span>
          <span className="text-[10px] text-[#3d4466]">{expenses.length} gasto{expenses.length !== 1 ? 's' : ''}</span>
        </div>
        <span className="text-xs font-semibold mono text-white">{formatAmount(dayTotal)}</span>
      </div>
      {open && (
        <div className="pl-2">
          {expenses.map((exp) =>
            exp.type === 'ticket' ? (
              <TicketRow
                key={exp.id}
                ticket={exp}
                onEdit={onEdit}
                onDelete={onDelete}
                creditCards={creditCards}
                banks={banks}
                people={people}
              />
            ) : (
              <ExpenseRow
                key={exp.id}
                expense={exp}
                onEdit={onEdit}
                onDelete={onDelete}
                creditCards={creditCards}
                banks={banks}
                people={people}
              />
            )
          )}
        </div>
      )}
    </div>
  )
}

// ─── Week group ────────────────────────────────────────────────────────────────
function WeekGroup({ weekRange, expenses, weekTotal, prevWeekTotal, creditCards, banks, people, onEdit, onDelete, defaultOpen }) {
  const [open, setOpen] = useState(defaultOpen)

  // Group by date
  const byDate = useMemo(() => {
    const map = {}
    expenses.forEach((e) => {
      if (!map[e.date]) map[e.date] = []
      map[e.date].push(e)
    })
    return Object.entries(map).sort(([a], [b]) => a.localeCompare(b))
  }, [expenses])

  const delta = prevWeekTotal > 0 ? Math.round(((weekTotal - prevWeekTotal) / prevWeekTotal) * 100) : null

  if (expenses.length === 0) return null

  return (
    <div className="border-b border-[#2e3350] last:border-0">
      {/* Week header */}
      <div
        className="flex items-center justify-between px-4 py-3 cursor-pointer hover:bg-[#1c2030]/40 transition-colors"
        onClick={() => setOpen(!open)}
      >
        <div className="flex items-center gap-3">
          {open ? <ChevronUp size={14} className="text-[#3d4466]" /> : <ChevronDown size={14} className="text-[#3d4466]" />}
          <div>
            <span className="text-sm font-semibold text-white">Semana {weekRange.key}</span>
            <span className="text-xs text-[#64748b] ml-1.5">{weekRange.label}</span>
          </div>
          <span className="text-[10px] text-[#3d4466]">{expenses.length} transacciones</span>
        </div>
        <div className="flex items-center gap-2">
          {delta !== null && (
            <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded ${
              delta > 0 ? 'bg-red-500/15 text-red-400' : 'bg-green-500/15 text-green-400'
            }`}>
              {delta > 0 ? '↑' : '↓'}{Math.abs(delta)}%
            </span>
          )}
          <span className="text-sm font-bold mono text-white">{formatAmount(weekTotal)}</span>
        </div>
      </div>

      {/* Days */}
      {open && (
        <div className="bg-[#0d0f14]/40">
          {byDate.map(([date, exps]) => (
            <DayGroup
              key={date}
              date={date}
              expenses={exps}
              creditCards={creditCards}
              banks={banks}
              people={people}
              onEdit={onEdit}
              onDelete={onDelete}
            />
          ))}
        </div>
      )}
    </div>
  )
}

// ─── Category summary ──────────────────────────────────────────────────────────
function CategorySummary({ expenses }) {
  const [open, setOpen] = useState(false)
  if (expenses.length === 0) return null

  const byCategory = {}
  expenses.forEach((e) => {
    if (e.type === 'ticket') {
      // Sum items by their individual category
      ;(e.items || []).forEach((it) => {
        if (it.category) byCategory[it.category] = (byCategory[it.category] || 0) + (it.amount || 0)
      })
    } else {
      if (e.category) byCategory[e.category] = (byCategory[e.category] || 0) + (e.amount || 0)
    }
  })
  const sorted = Object.entries(byCategory).sort(([, a], [, b]) => b - a)
  const total = expenses.reduce((s, e) => s + getExpenseAmount(e), 0)

  return (
    <div className="mt-4 border border-[#2e3350] rounded-xl overflow-hidden">
      <div
        className="flex items-center justify-between px-4 py-3 cursor-pointer hover:bg-[#1c2030] transition-colors"
        onClick={() => setOpen(!open)}
      >
        <div className="flex items-center gap-2">
          {open ? <ChevronUp size={13} className="text-[#3d4466]" /> : <ChevronDown size={13} className="text-[#3d4466]" />}
          <span className="text-xs font-semibold text-[#94a3b8] uppercase tracking-wider">Resumen por categoría</span>
        </div>
        <span className="text-xs font-bold mono text-white">{formatAmount(total)}</span>
      </div>
      {open && (
        <div className="divide-y divide-[#1c2030]">
          {sorted.map(([cat, amount]) => {
            const pct = Math.round((amount / total) * 100)
            const icon = getCategoryIcon(cat)
            return (
              <div key={cat} className="flex items-center gap-3 px-4 py-2.5">
                <span className="text-base shrink-0">{icon}</span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-xs font-medium text-[#94a3b8]">{cat}</span>
                    <span className="text-xs font-semibold mono text-white">{formatAmount(amount)}</span>
                  </div>
                  <div className="h-1 bg-[#1c2030] rounded-full overflow-hidden">
                    <div
                      className="h-full bg-blue-500/60 rounded-full"
                      style={{ width: `${pct}%` }}
                    />
                  </div>
                </div>
                <span className="text-[10px] text-[#64748b] w-8 text-right shrink-0">{pct}%</span>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

// ─── Payment type filter chips ─────────────────────────────────────────────────
const PAYMENT_FILTERS = [
  { key: null,     label: 'Todos'    },
  { key: 'credit', label: '💳 Crédito' },
  { key: 'debit',  label: '🏦 Débito'  },
  { key: 'cash',   label: '💵 Efectivo' },
]

// ─── Page ──────────────────────────────────────────────────────────────────────
export default function ExpensesPage() {
  const currentMonth = useStore((s) => s.currentMonth)
  const expenses     = useStore((s) => s.expenses)
  const creditCards  = useStore((s) => s.creditCards)
  const banks        = useStore((s) => s.banks)
  const people       = useStore((s) => s.people)
  const getBudget    = useStore((s) => s.getBudget)
  const deleteExpense = useStore((s) => s.deleteExpense)

  const getMonthCreditTotal    = useStore((s) => s.getMonthCreditTotal)
  const getMonthDebitCashTotal = useStore((s) => s.getMonthDebitCashTotal)
  const getWeekExpenseTotal    = useStore((s) => s.getWeekExpenseTotal)

  const [formOpen, setFormOpen]         = useState(false)
  const [editExp, setEditExp]           = useState(null)
  const [ticketFormOpen, setTicketFormOpen] = useState(false)
  const [editTicket, setEditTicket]     = useState(null)
  const [budgetOpen, setBudgetOpen]     = useState(false)

  // ── Filters ────────────────────────────────────────────────────────────────
  const [searchText,     setSearchText]     = useState('')
  const [filterCategory, setFilterCategory] = useState(null) // string | null
  const [filterPayment,  setFilterPayment]  = useState(null) // 'credit'|'debit'|'cash'|null

  const hasFilters  = !!(searchText || filterCategory || filterPayment)
  const resetFilters = () => { setSearchText(''); setFilterCategory(null); setFilterPayment(null) }

  const budget = getBudget(currentMonth)

  // Expenses for this month (calendar month), sorted by date desc
  const monthExpenses = useMemo(() =>
    expenses
      .filter((e) => e.date.startsWith(currentMonth))
      .sort((a, b) => b.date.localeCompare(a.date) || getExpenseAmount(b) - getExpenseAmount(a)),
    [expenses, currentMonth]
  )

  // Filtered expenses
  const filteredExpenses = useMemo(() => {
    return monthExpenses.filter((e) => {
      if (searchText) {
        const q = searchText.toLowerCase()
        const matchDesc = e.description.toLowerCase().includes(q)
        const matchItem = e.type === 'ticket' && (e.items || []).some((it) => it.description.toLowerCase().includes(q))
        if (!matchDesc && !matchItem) return false
      }
      if (filterCategory) {
        if (e.type === 'ticket') {
          if (!(e.items || []).some((it) => it.category === filterCategory)) return false
        } else {
          if (e.category !== filterCategory) return false
        }
      }
      if (filterPayment === 'credit' && !isCredit(e.paymentMethod, creditCards)) return false
      if (filterPayment === 'debit'  && !e.paymentMethod?.startsWith('debit-'))   return false
      if (filterPayment === 'cash'   && e.paymentMethod !== 'cash')               return false
      return true
    })
  }, [monthExpenses, searchText, filterCategory, filterPayment, creditCards])

  const filteredTotal = useMemo(
    () => filteredExpenses.reduce((s, e) => s + getExpenseAmount(e), 0),
    [filteredExpenses]
  )

  // Unfiltered totals for budget bars
  const creditSpent    = getMonthCreditTotal(currentMonth)
  const debitCashSpent = getMonthDebitCashTotal(currentMonth)
  const monthTotal     = creditSpent + debitCashSpent

  // Current week for weekly budget bar
  const today      = new Date()
  const todayDay   = today.getDate()
  const currentWeek      = todayDay <= 7 ? 1 : todayDay <= 14 ? 2 : todayDay <= 22 ? 3 : 4
  const currentWeekTotal = getWeekExpenseTotal(currentMonth, currentWeek)

  const handleEdit = (exp) => {
    if (exp.type === 'ticket') { setEditTicket(exp); setTicketFormOpen(true) }
    else                       { setEditExp(exp);    setFormOpen(true)       }
  }
  const handleDelete = async (id) => {
    if (!window.confirm('¿Eliminar este gasto?')) return
    const result = await deleteExpense(id)
    if (result.error) window.alert(result.error)
  }
  const handleAdd       = () => { setEditExp(null);    setFormOpen(true)       }
  const handleAddTicket = () => { setEditTicket(null); setTicketFormOpen(true) }

  // Per-week totals computed from filtered data (for delta display in WeekGroup)
  const filteredWeekTotals = useMemo(() =>
    WEEK_RANGES.map((w) =>
      filterByWeek(filteredExpenses, w.key).reduce((s, e) => s + getExpenseAmount(e), 0)
    ),
    [filteredExpenses]
  )

  const hasBudgetBars = budget.creditLimit > 0 || budget.debitCashLimit > 0 || budget.weeklyLimit > 0

  return (
    <div className="flex flex-col h-full">
      {/* ── Sub-header ────────────────────────────────────────────────────────── */}
      <div className="shrink-0 px-6 py-4 border-b border-[#1c2030] bg-[#0d0f14]">

        {/* Row 1: title + action buttons */}
        <div className="flex items-center justify-between mb-3">
          <div>
            <h2 className="text-base font-bold text-white">
              Gastos — <span className="text-blue-400">{formatMonthLong(currentMonth)}</span>
            </h2>
            <p className="text-xs text-[#64748b] mt-0.5">
              {hasFilters ? (
                <>
                  <span className="text-white font-medium">{filteredExpenses.length}</span>
                  {' de '}{monthExpenses.length} transacciones ·{' '}
                  <span className="text-white font-semibold mono">{formatAmount(filteredTotal)}</span>
                </>
              ) : (
                <>
                  {monthExpenses.length} transacciones · total{' '}
                  <span className="text-white font-semibold mono">{formatAmount(monthTotal)}</span>
                </>
              )}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setBudgetOpen(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border border-[#2e3350] text-[#64748b] hover:text-white hover:border-[#3d4466] transition-colors"
            >
              <Target size={12} />
              Presupuesto
            </button>
            <button
              onClick={handleAddTicket}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border border-[#2e3350] text-[#64748b] hover:text-white hover:border-[#3d4466] transition-colors"
            >
              <Receipt size={12} />
              Cargar ticket
            </button>
            <button
              onClick={handleAdd}
              className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
            >
              <Plus size={15} />
              Nuevo gasto
            </button>
          </div>
        </div>

        {/* Row 2: budget bars (unfiltered totals) */}
        {hasBudgetBars && (
          <div className={`space-y-1.5 ${monthExpenses.length > 0 ? 'mb-3' : ''}`}>
            <BudgetBar label="Crédito"   spent={creditSpent}    limit={budget.creditLimit}    color="#3b82f6" />
            <BudgetBar label="Déb./Efvo" spent={debitCashSpent} limit={budget.debitCashLimit} color="#22c55e" />
            <BudgetBar label="Semanal"   spent={currentWeekTotal} limit={budget.weeklyLimit}  color="#a855f7" />
          </div>
        )}

        {/* Row 3+4: filters — only when there are expenses */}
        {monthExpenses.length > 0 && (
          <div className="space-y-2">

            {/* Search + payment type + clear */}
            <div className="flex items-center gap-2 flex-wrap">
              {/* Search input */}
              <div className="relative">
                <Search size={12} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-[#3d4466] pointer-events-none" />
                <input
                  type="text"
                  placeholder="Buscar descripción..."
                  value={searchText}
                  onChange={(e) => setSearchText(e.target.value)}
                  className="pl-7 pr-7 py-1.5 w-full sm:w-52 bg-[#1c2030] border border-[#2e3350] rounded-lg text-xs text-white placeholder-[#3d4466] focus:outline-none focus:border-blue-500/50 transition-colors"
                />
                {searchText && (
                  <button
                    onClick={() => setSearchText('')}
                    className="absolute right-2 top-1/2 -translate-y-1/2 text-[#3d4466] hover:text-[#94a3b8] transition-colors"
                  >
                    <X size={11} />
                  </button>
                )}
              </div>

              {/* Payment type chips */}
              <div className="flex items-center gap-1">
                {PAYMENT_FILTERS.map(({ key, label }) => (
                  <button
                    key={String(key)}
                    onClick={() => setFilterPayment(key)}
                    className={`px-2.5 py-1 rounded-full text-[11px] font-medium whitespace-nowrap transition-colors ${
                      filterPayment === key
                        ? 'bg-purple-600/80 text-white border border-purple-500/50'
                        : 'bg-[#1c2030] text-[#64748b] hover:text-[#94a3b8] border border-[#2e3350]'
                    }`}
                  >
                    {label}
                  </button>
                ))}
              </div>

              {/* Clear button — only when filters active */}
              {hasFilters && (
                <button
                  onClick={resetFilters}
                  className="flex items-center gap-1 text-[11px] text-[#64748b] hover:text-red-400 transition-colors"
                >
                  <X size={10} />
                  Limpiar
                </button>
              )}
            </div>

            {/* Category chips (horizontal scroll) */}
            <div className="flex items-center gap-1.5 overflow-x-auto pb-0.5 [scrollbar-width:none] [-ms-overflow-style:none] [&::-webkit-scrollbar]:hidden">
              <button
                onClick={() => setFilterCategory(null)}
                className={`px-2.5 py-1 rounded-full text-[11px] font-medium whitespace-nowrap transition-colors shrink-0 ${
                  !filterCategory
                    ? 'bg-blue-600/80 text-white border border-blue-500/50'
                    : 'bg-[#1c2030] text-[#64748b] hover:text-[#94a3b8] border border-[#2e3350]'
                }`}
              >
                Todas
              </button>
              {EXPENSE_CATEGORIES.map((cat) => (
                <button
                  key={cat.value}
                  onClick={() => setFilterCategory(filterCategory === cat.value ? null : cat.value)}
                  className={`px-2.5 py-1 rounded-full text-[11px] font-medium whitespace-nowrap transition-colors shrink-0 ${
                    filterCategory === cat.value
                      ? 'bg-blue-600/80 text-white border border-blue-500/50'
                      : 'bg-[#1c2030] text-[#64748b] hover:text-[#94a3b8] border border-[#2e3350]'
                  }`}
                >
                  {cat.icon} {cat.value}
                </button>
              ))}
            </div>

          </div>
        )}
      </div>

      {/* ── List ──────────────────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-auto p-6">

        {/* Borradores del agente pendientes de confirmar (si hay) */}
        <DraftsPanel />

        {/* Empty — no expenses at all */}
        {monthExpenses.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-64 text-center">
            <ShoppingCart size={40} className="text-[#2e3350] mb-4" />
            <p className="text-[#64748b] font-medium mb-1">Sin gastos este mes</p>
            <p className="text-sm text-[#3d4466] mb-4">Registrá tus gastos diarios para llevar el control.</p>
            <button
              onClick={handleAdd}
              className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium"
            >
              <Plus size={15} />
              Agregar gasto
            </button>
          </div>

        /* Empty — filters match nothing */
        ) : filteredExpenses.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-48 text-center">
            <Search size={32} className="text-[#2e3350] mb-3" />
            <p className="text-[#64748b] font-medium mb-1">Sin resultados</p>
            <p className="text-xs text-[#3d4466] mb-3">
              No hay gastos que coincidan con los filtros aplicados.
            </p>
            <button
              onClick={resetFilters}
              className="text-xs text-blue-400 hover:text-blue-300 transition-colors"
            >
              Limpiar filtros
            </button>
          </div>

        /* Expense list */
        ) : (
          <>
            <div className="bg-[#151820] border border-[#2e3350] rounded-xl overflow-hidden">
              {WEEK_RANGES.map((weekRange, idx) => {
                const weekExpenses  = filterByWeek(filteredExpenses, weekRange.key)
                const weekTotal     = filteredWeekTotals[idx]
                const prevWeekTotal = idx > 0 ? filteredWeekTotals[idx - 1] : 0
                return (
                  <WeekGroup
                    key={weekRange.key}
                    weekRange={weekRange}
                    expenses={weekExpenses}
                    weekTotal={weekTotal}
                    prevWeekTotal={prevWeekTotal}
                    creditCards={creditCards}
                    banks={banks}
                    people={people}
                    onEdit={handleEdit}
                    onDelete={handleDelete}
                    defaultOpen={weekRange.key === currentWeek}
                  />
                )
              })}
            </div>

            <CategorySummary expenses={filteredExpenses} />
          </>
        )}
      </div>

      <ExpenseForm
        open={formOpen}
        onClose={() => { setFormOpen(false); setEditExp(null) }}
        expense={editExp}
      />
      <TicketForm
        open={ticketFormOpen}
        onClose={() => { setTicketFormOpen(false); setEditTicket(null) }}
        ticket={editTicket}
      />
      <BudgetModal
        open={budgetOpen}
        onClose={() => setBudgetOpen(false)}
        month={currentMonth}
      />
    </div>
  )
}
