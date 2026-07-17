import { useState, useEffect, useMemo } from 'react'
import { Plus, Trash2, ChevronDown, ChevronUp } from 'lucide-react'
import SlideOver from '../../components/ui/SlideOver'
import AmountInput from '../../components/ui/AmountInput'
import OwnerBadge from '../../components/ui/OwnerBadge'
import DescriptionAutocomplete, { toFreqSorted } from '../../components/ui/DescriptionAutocomplete'
import CategoryPicker from '../../components/ui/CategoryPicker'
import useStore from '../../store/useStore'
import { formatMonth, formatAmount } from '../../lib/formatters'
import {
  buildPaymentMethods,
  getPaymentMonth,
  getCategoryIcon,
  todayStr,
} from './expensesConfig'

const FIELD = 'w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30'

const EMPTY_FORM = { date: todayStr(), description: '', paymentMethod: '' }

const mkItem = () => ({
  _id: Math.random().toString(36).slice(2, 9),
  description: '',
  amount: '',
  category: '',
  ownerId: null,
})

export default function TicketForm({ open, onClose, ticket = null }) {
  const creditCards   = useStore((s) => s.creditCards)
  const banks         = useStore((s) => s.banks)
  const people        = useStore((s) => s.people)
  const addExpense    = useStore((s) => s.addExpense)
  const updateExpense = useStore((s) => s.updateExpense)
  const expenses      = useStore((s) => s.expenses)

  // Sugerencias para la descripción del ticket (nombre del comercio)
  const ticketSuggestions = useMemo(() =>
    toFreqSorted(expenses.filter((e) => e.type === 'ticket').map((e) => e.description)),
  [expenses])

  // Sugerencias para descripciones de ítems (productos)
  const itemSuggestions = useMemo(() =>
    toFreqSorted(
      expenses
        .filter((e) => e.type === 'ticket')
        .flatMap((e) => e.items || [])
        .map((it) => it.description)
    ),
  [expenses])

  const [form, setForm]         = useState(EMPTY_FORM)
  const [items, setItems]       = useState([])
  const [discount, setDiscount] = useState('')
  const [expanded, setExpanded] = useState(new Set())
  const [error, setError]       = useState('')
  const [busy, setBusy]       = useState(false)

  useEffect(() => {
    if (!open) return
    setError('')
    if (ticket) {
      setForm({ date: ticket.date, description: ticket.description, paymentMethod: ticket.paymentMethod })
      setItems((ticket.items || []).map((it) => ({ ...it, _id: it.id || Math.random().toString(36).slice(2, 9) })))
      setDiscount(ticket.discount || '')
      setExpanded(new Set())
    } else {
      const first = mkItem()
      setForm({ ...EMPTY_FORM, date: todayStr() })
      setItems([first])
      setDiscount('')
      setExpanded(new Set([first._id]))
    }
  }, [open, ticket])

  const setF = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  const paymentGroups = buildPaymentMethods(creditCards, banks)

  const subtotal    = items.reduce((s, it) => s + (Number(it.amount) || 0), 0)
  const discountAmt = Number(discount) || 0
  const total       = Math.max(0, subtotal - discountAmt)

  const toggleExpand = (_id) =>
    setExpanded((prev) => {
      const next = new Set(prev)
      next.has(_id) ? next.delete(_id) : next.add(_id)
      return next
    })

  const addItem = () => {
    const item = mkItem()
    setItems((prev) => [...prev, item])
    setExpanded((prev) => new Set([...prev, item._id]))
  }

  const removeItem = (_id) => {
    setItems((prev) => prev.filter((it) => it._id !== _id))
    setExpanded((prev) => { const next = new Set(prev); next.delete(_id); return next })
  }

  const updItem = (_id, k, v) =>
    setItems((prev) => prev.map((it) => it._id === _id ? { ...it, [k]: v } : it))

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.description.trim()) return setError('Ingresá una descripción.')
    if (!form.paymentMethod)      return setError('Seleccioná el medio de pago.')
    if (items.length === 0)       return setError('Agregá al menos un ítem.')
    for (let i = 0; i < items.length; i++) {
      const it = items[i]
      if (!it.description.trim())           return setError(`Ítem ${i + 1}: falta descripción.`)
      if (!it.amount || Number(it.amount) <= 0) return setError(`Ítem ${i + 1}: ingresá un monto válido.`)
      if (!it.category)                     return setError(`Ítem ${i + 1}: seleccioná una categoría.`)
    }

    const payload = {
      type: 'ticket',
      date: form.date,
      description: form.description.trim(),
      paymentMethod: form.paymentMethod,
      items: items.map((it, idx) => ({
        id: `item-${Date.now()}-${idx}`,
        description: it.description.trim(),
        amount: Number(it.amount),
        category: it.category,
        ownerId: it.ownerId || null,
      })),
      discount: discountAmt,
    }

    setError(''); setBusy(true)
    const result = await (ticket ? updateExpense(ticket.id, payload) : addExpense(payload))
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }

  // Billing hint
  const billingHint = (() => {
    const card = creditCards.find((c) => c.id === form.paymentMethod)
    if (!card || !form.date || !card.closingDay) return null
    const payMonth = getPaymentMonth(form.date, card.closingDay, card.dueDay)
    const differs  = payMonth !== form.date.slice(0, 7)
    return { payMonth, differs }
  })()

  return (
    <SlideOver open={open} onClose={onClose} title={ticket ? 'Editar ticket' : 'Cargar ticket'}>
      <form onSubmit={handleSubmit} className="space-y-5">

        {/* Fecha */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">Fecha</label>
          <input
            type="date"
            value={form.date}
            onChange={(e) => setF('date', e.target.value)}
            className={FIELD + ' [color-scheme:dark]'}
            required
          />
        </div>

        {/* Descripción */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">Descripción del ticket</label>
          <DescriptionAutocomplete
            value={form.description}
            onChange={(v) => setF('description', v)}
            suggestions={ticketSuggestions}
            placeholder="ej. Coto, Día, Farmacity..."
            autoFocus
          />
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
                      onClick={() => setF('paymentMethod', item.value)}
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
                      <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: item.color }} />
                      {item.label}
                    </button>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Billing hint */}
        {billingHint && (
          <p className={`text-[11px] -mt-1 ${billingHint.differs ? 'text-amber-400' : 'text-[#64748b]'}`}>
            {billingHint.differs ? '⚠ ' : '📅 '}
            Este ticket se paga en{' '}
            <span className="font-semibold">{formatMonth(billingHint.payMonth)}</span>
            {billingHint.differs && (
              <span className="text-[#64748b]">
                {' '}(cierre día {creditCards.find((c) => c.id === form.paymentMethod)?.closingDay})
              </span>
            )}
          </p>
        )}

        {/* ── Ítems ──────────────────────────────────────────────────────── */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <label className="text-xs font-medium text-[#64748b]">Ítems del ticket</label>
            <span className="text-[10px] text-[#3d4466]">
              {items.length} {items.length === 1 ? 'ítem' : 'ítems'}
            </span>
          </div>

          <div className="space-y-2">
            {items.map((item, idx) => {
              const isOpen   = expanded.has(item._id)
              const catIcon  = item.category ? getCategoryIcon(item.category) : null

              return (
                <div key={item._id} className="border border-[#2e3350] rounded-xl overflow-hidden">

                  {/* ── Header ── */}
                  <div
                    className="flex items-center gap-2 px-3 py-2.5 bg-[#1c2030] cursor-pointer select-none"
                    onClick={() => toggleExpand(item._id)}
                  >
                    <span className="text-[#3d4466] shrink-0">
                      {isOpen ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
                    </span>

                    {!isOpen && catIcon && (
                      <span className="text-base leading-none shrink-0">{catIcon}</span>
                    )}

                    <span
                      className="flex-1 min-w-0 text-sm truncate"
                      style={{ color: item.description ? 'white' : '#3d4466' }}
                    >
                      {item.description || `Ítem ${idx + 1}`}
                    </span>

                    {!isOpen && Number(item.amount) > 0 && (
                      <span className="text-xs mono text-[#94a3b8] shrink-0">
                        {formatAmount(Number(item.amount))}
                      </span>
                    )}

                    {items.length > 1 && (
                      <button
                        type="button"
                        onClick={(ev) => { ev.stopPropagation(); removeItem(item._id) }}
                        className="p-0.5 rounded text-[#3d4466] hover:text-red-400 transition-colors shrink-0"
                      >
                        <Trash2 size={12} />
                      </button>
                    )}
                  </div>

                  {/* ── Expanded body ── */}
                  {isOpen && (
                    <div className="px-3 py-3 space-y-3 bg-[#151820] border-t border-[#2e3350]">

                      {/* Descripción del ítem */}
                      <div>
                        <label className="block text-[10px] font-semibold text-[#3d4466] uppercase tracking-wider mb-1.5">
                          Descripción
                        </label>
                        <DescriptionAutocomplete
                          value={item.description}
                          onChange={(v) => updItem(item._id, 'description', v)}
                          suggestions={itemSuggestions}
                          placeholder="ej. Leche, Aceite, Shampoo..."
                          onClick={(e) => e.stopPropagation()}
                        />
                      </div>

                      {/* Monto del ítem */}
                      <div>
                        <label className="block text-[10px] font-semibold text-[#3d4466] uppercase tracking-wider mb-1.5">
                          Precio
                        </label>
                        <AmountInput
                          value={item.amount}
                          onChange={(v) => updItem(item._id, 'amount', v)}
                        />
                      </div>

                      {/* Categoría */}
                      <div>
                        <label className="block text-[10px] font-semibold text-[#3d4466] uppercase tracking-wider mb-1.5">
                          Categoría
                        </label>
                        <CategoryPicker
                          value={item.category}
                          onChange={(v) => updItem(item._id, 'category', v)}
                        />
                      </div>

                      {/* Propietario */}
                      <div>
                        <label className="block text-[10px] font-semibold text-[#3d4466] uppercase tracking-wider mb-1.5">
                          ¿De quién?
                        </label>
                        <div className="flex flex-wrap gap-1.5">
                          <button
                            type="button"
                            onClick={() => updItem(item._id, 'ownerId', null)}
                            className={`flex items-center gap-1 px-2.5 py-1 rounded-lg border text-[11px] font-medium transition-all ${
                              !item.ownerId
                                ? 'border-[#3d4466] bg-[#1c2030] text-[#94a3b8]'
                                : 'border-[#2e3350] text-[#64748b] hover:border-[#3d4466] hover:text-white'
                            }`}
                          >
                            Sin asignar
                          </button>
                          <button
                            type="button"
                            onClick={() => updItem(item._id, 'ownerId', item.ownerId === 'shared' ? null : 'shared')}
                            className={`flex items-center gap-1 px-2.5 py-1 rounded-lg border text-[11px] font-medium transition-all ${
                              item.ownerId === 'shared'
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
                              onClick={() => updItem(item._id, 'ownerId', item.ownerId === p.id ? null : p.id)}
                              className={`flex items-center gap-1 px-2.5 py-1 rounded-lg border text-[11px] font-medium transition-all ${
                                item.ownerId === p.id
                                  ? 'text-white'
                                  : 'border-[#2e3350] text-[#64748b] hover:border-[#3d4466] hover:text-white'
                              }`}
                              style={
                                item.ownerId === p.id
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
                    </div>
                  )}
                </div>
              )
            })}
          </div>

          <button
            type="button"
            onClick={addItem}
            className="mt-2 w-full flex items-center justify-center gap-1.5 py-2.5 rounded-xl border border-dashed border-[#2e3350] text-[11px] text-[#3d4466] hover:border-[#3d4466] hover:text-[#64748b] transition-colors"
          >
            <Plus size={12} />
            Agregar ítem
          </button>
        </div>

        {/* Descuento */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">Descuento</label>
          <AmountInput
            value={discount}
            onChange={(v) => setDiscount(v)}
            placeholder="0"
          />
        </div>

        {/* Resumen */}
        {subtotal > 0 && (
          <div className="rounded-xl border border-[#2e3350] overflow-hidden">
            <div className="px-4 py-3 space-y-1.5 bg-[#1c2030]/50">
              <div className="flex items-center justify-between text-[11px] text-[#64748b]">
                <span>Subtotal ({items.length} {items.length === 1 ? 'ítem' : 'ítems'})</span>
                <span className="mono">{formatAmount(subtotal)}</span>
              </div>
              {discountAmt > 0 && (
                <div className="flex items-center justify-between text-[11px] text-green-400">
                  <span>Descuento</span>
                  <span className="mono">- {formatAmount(discountAmt)}</span>
                </div>
              )}
              <div className="flex items-center justify-between pt-1.5 border-t border-[#2e3350]">
                <span className="text-sm font-semibold text-white">Total</span>
                <span className="text-sm font-bold mono text-white">{formatAmount(total)}</span>
              </div>
            </div>
          </div>
        )}

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
            {ticket ? 'Guardar' : 'Agregar ticket'}
          </button>
        </div>
      </form>
    </SlideOver>
  )
}
