import { useState, useEffect, useMemo } from 'react'
import SlideOver from '../../components/ui/SlideOver'
import AmountInput from '../../components/ui/AmountInput'
import OwnerBadge, { SHARED_OWNER } from '../../components/ui/OwnerBadge'
import DescriptionAutocomplete, { toFreqSorted } from '../../components/ui/DescriptionAutocomplete'
import CategoryPicker from '../../components/ui/CategoryPicker'
import useStore from '../../store/useStore'
import { formatMonth, formatAmount } from '../../lib/formatters'
import {
  buildPaymentMethods,
  getPaymentMonth,
  todayStr,
} from './expensesConfig'

const FIELD = 'w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30'

const EMPTY = {
  date: todayStr(),
  description: '',
  category: '',
  amount: '',
  paymentMethod: '',
  currency: 'ARS',
  ownerId: null,
}

export default function ExpenseForm({ open, onClose, expense = null }) {
  const creditCards   = useStore((s) => s.creditCards)
  const banks         = useStore((s) => s.banks)
  const people        = useStore((s) => s.people)
  const addExpense    = useStore((s) => s.addExpense)
  const updateExpense = useStore((s) => s.updateExpense)
  const usdRateCCL    = useStore((s) => s.income.usdRateCCL)
  const expenses      = useStore((s) => s.expenses)

  // Sugerencias de descripción: gastos regulares (no tickets), ordenados por frecuencia
  const descSuggestions = useMemo(() =>
    toFreqSorted(expenses.filter((e) => e.type !== 'ticket').map((e) => e.description)),
  [expenses])

  const [form, setForm] = useState(EMPTY)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  // Reset / populate when opening
  useEffect(() => {
    if (!open) return
    setError('')
    if (expense) {
      setForm({
        ...EMPTY,
        ...expense,
        currency: expense.originalCurrency === 'USD' ? 'USD' : 'ARS',
        amount: expense.originalCurrency === 'USD' ? expense.originalAmount : expense.amount,
        ownerId: expense.ownerId || null,
      })
    } else {
      setForm({ ...EMPTY, date: todayStr() })
    }
  }, [open, expense])

  const set = (key, val) => setForm((f) => ({ ...f, [key]: val }))

  const paymentGroups = buildPaymentMethods(creditCards, banks)

  const amountARS = form.currency === 'USD' && usdRateCCL > 0
    ? Math.round(Number(form.amount || 0) * usdRateCCL)
    : null

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.description.trim()) return setError('Ingresá una descripción.')
    if (!form.category) return setError('Seleccioná una categoría.')
    if (!form.amount || form.amount <= 0) return setError('Ingresá un monto válido.')
    if (!form.paymentMethod) return setError('Seleccioná el medio de pago.')
    if (form.currency === 'USD' && !usdRateCCL) return setError('Configurá el tipo de cambio CCL en Ajustes.')

    const finalAmount = form.currency === 'USD'
      ? Math.round(Number(form.amount) * usdRateCCL)
      : Number(form.amount)

    const payload = {
      date: form.date,
      description: form.description.trim(),
      category: form.category,
      amount: finalAmount,
      paymentMethod: form.paymentMethod,
      ownerId: form.ownerId || null,
      ...(form.currency === 'USD' && {
        originalAmount: Number(form.amount),
        originalCurrency: 'USD',
      }),
    }

    if (expense) {
      // Si se cambió de USD a ARS al editar, limpiar campos originales
      if (form.currency === 'ARS' && expense.originalCurrency === 'USD') {
        payload.originalAmount = null
        payload.originalCurrency = null
      }
    }

    setError(''); setBusy(true)
    const result = await (expense ? updateExpense(expense.id, payload) : addExpense(payload))
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }

  return (
    <SlideOver
      open={open}
      onClose={onClose}
      title={expense ? 'Editar gasto' : 'Nuevo gasto'}
    >
      <form onSubmit={handleSubmit} className="space-y-5">
        {/* Fecha */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">Fecha</label>
          <input
            type="date"
            value={form.date}
            onChange={(e) => set('date', e.target.value)}
            className={FIELD + ' [color-scheme:dark]'}
            required
          />
        </div>

        {/* Descripción */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">Descripción</label>
          <DescriptionAutocomplete
            value={form.description}
            onChange={(v) => set('description', v)}
            suggestions={descSuggestions}
            placeholder="ej. Coto, Farmacity, Nafta..."
            autoFocus
          />
        </div>

        {/* Categoría */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">Categoría</label>
          <CategoryPicker value={form.category} onChange={(v) => set('category', v)} />
        </div>

        {/* Monto */}
        <div>
          <div className="flex items-center justify-between mb-1.5">
            <label className="text-xs font-medium text-[#64748b]">Precio</label>
            {/* Toggle ARS / USD */}
            <div className="flex rounded-lg overflow-hidden border border-[#2e3350]">
              {['ARS', 'USD'].map((cur) => (
                <button
                  key={cur}
                  type="button"
                  onClick={() => { set('currency', cur); set('amount', '') }}
                  className={`px-2.5 py-0.5 text-[11px] font-semibold transition-colors ${
                    form.currency === cur
                      ? cur === 'USD'
                        ? 'bg-emerald-600 text-white'
                        : 'bg-blue-600 text-white'
                      : 'text-[#64748b] hover:text-white'
                  }`}
                >
                  {cur}
                </button>
              ))}
            </div>
          </div>
          <AmountInput
            value={form.amount}
            onChange={(v) => set('amount', v)}
            placeholder="0"
          />
          {/* Conversión USD → ARS */}
          {form.currency === 'USD' && (
            <p className="text-[11px] mt-1.5">
              {usdRateCCL > 0 ? (
                form.amount > 0 ? (
                  <span className="text-emerald-400">
                    ≈ {formatAmount(amountARS)} ARS
                    <span className="text-[#64748b]"> (CCL ${usdRateCCL.toLocaleString('es-AR')}/USD)</span>
                  </span>
                ) : (
                  <span className="text-[#64748b]">CCL: ${usdRateCCL.toLocaleString('es-AR')}/USD</span>
                )
              ) : (
                <span className="text-amber-400">⚠ Configurá el tipo de cambio CCL en Ajustes</span>
              )}
            </p>
          )}
        </div>

        {/* Medio de pago */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">Medio de pago</label>
          <div className="space-y-3">
            {paymentGroups.map((group) => (
              <div key={group.group}>
                <p className="text-[10px] font-semibold text-[#3d4466] uppercase tracking-wider mb-1.5">
                  {group.group}
                </p>
                <div className="flex flex-wrap gap-2">
                  {group.items.map((item) => (
                    <button
                      key={item.value}
                      type="button"
                      onClick={() => set('paymentMethod', item.value)}
                      className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg border text-xs font-medium transition-all ${
                        form.paymentMethod === item.value
                          ? 'text-white'
                          : 'border-[#2e3350] text-[#64748b] hover:border-[#3d4466] hover:text-white'
                      }`}
                      style={
                        form.paymentMethod === item.value
                          ? { borderColor: item.color, backgroundColor: `${item.color}22`, color: item.color }
                          : {}
                      }
                    >
                      <span
                        className="w-2 h-2 rounded-full shrink-0"
                        style={{ backgroundColor: item.color }}
                      />
                      {item.label}
                    </button>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* ── Hint de pago de tarjeta ───────────────────────────────────── */}
        {(() => {
          const card = creditCards.find((c) => c.id === form.paymentMethod)
          if (!card || !form.date || !card.closingDay) return null
          const payMonth = getPaymentMonth(form.date, card.closingDay, card.dueDay)
          const calendarMonth = form.date.slice(0, 7)
          const differsFromCalendar = payMonth !== calendarMonth
          return (
            <p className={`text-[11px] -mt-1 ${differsFromCalendar ? 'text-amber-400' : 'text-[#64748b]'}`}>
              {differsFromCalendar ? '⚠ ' : '📅 '}
              Este gasto se paga en{' '}
              <span className="font-semibold">{formatMonth(payMonth)}</span>
              {differsFromCalendar && (
                <span className="text-[#64748b]"> (cierre día {card.closingDay})</span>
              )}
            </p>
          )
        })()}

        {/* Persona */}
        <div>
            <label className="block text-xs font-medium text-[#64748b] mb-1.5">¿De quién es este gasto?</label>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => set('ownerId', null)}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg border text-xs font-medium transition-all ${
                  !form.ownerId
                    ? 'border-[#3d4466] bg-[#1c2030] text-[#94a3b8]'
                    : 'border-[#2e3350] text-[#64748b] hover:border-[#3d4466] hover:text-white'
                }`}
              >
                Sin asignar
              </button>
              <button
                type="button"
                onClick={() => set('ownerId', form.ownerId === 'shared' ? null : 'shared')}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg border text-xs font-medium transition-all ${
                  form.ownerId === 'shared'
                    ? 'border-[#4a5568] bg-[#2e3350] text-[#94a3b8]'
                    : 'border-[#2e3350] text-[#64748b] hover:border-[#3d4466] hover:text-white'
                }`}
              >
                🤝 Compartido
              </button>
              {people.map((p) => (
                <button
                  key={p.id}
                  type="button"
                  onClick={() => set('ownerId', form.ownerId === p.id ? null : p.id)}
                  className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg border text-xs font-medium transition-all ${
                    form.ownerId === p.id
                      ? 'text-white'
                      : 'border-[#2e3350] text-[#64748b] hover:border-[#3d4466] hover:text-white'
                  }`}
                  style={
                    form.ownerId === p.id
                      ? { borderColor: p.color, backgroundColor: `${p.color}22`, color: p.color }
                      : {}
                  }
                >
                  <OwnerBadge person={p} size="xs" />
                  {p.name}
                </button>
              ))}
            </div>
          </div>

        {error && <p className="text-xs text-red-400">{error}</p>}

        <div className="flex gap-3 pt-2">
          <button
            type="button"
            onClick={onClose}
            className="flex-1 px-4 py-2 rounded-lg border border-[#2e3350] text-sm text-[#64748b] hover:text-white hover:border-[#3d4466] transition-colors"
          >
            Cancelar
          </button>
          <button
            type="submit" disabled={busy}
            className="flex-1 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
          >
            {expense ? 'Guardar' : 'Agregar'}
          </button>
        </div>
      </form>
    </SlideOver>
  )
}
