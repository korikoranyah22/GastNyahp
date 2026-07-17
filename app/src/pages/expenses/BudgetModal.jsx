import { useState, useEffect } from 'react'
import SlideOver from '../../components/ui/SlideOver'
import AmountInput from '../../components/ui/AmountInput'
import useStore from '../../store/useStore'
import { formatMonthLong } from '../../lib/formatters'

export default function BudgetModal({ open, onClose, month }) {
  const getBudget = useStore((s) => s.getBudget)
  const setBudget = useStore((s) => s.setBudget)

  const [form, setForm] = useState({ creditLimit: '', debitCashLimit: '', weeklyLimit: '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!open || !month) return
    const b = getBudget(month)
    setForm({
      creditLimit:    b.creditLimit    || '',
      debitCashLimit: b.debitCashLimit || '',
      weeklyLimit:    b.weeklyLimit    || '',
    })
  }, [open, month])

  const set = (key, val) => setForm((f) => ({ ...f, [key]: val }))

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError(''); setBusy(true)
    const result = await setBudget(month, {
      creditLimit:    Number(form.creditLimit)    || 0,
      debitCashLimit: Number(form.debitCashLimit) || 0,
      weeklyLimit:    Number(form.weeklyLimit)    || 0,
    })
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }

  return (
    <SlideOver
      open={open}
      onClose={onClose}
      title={`Presupuesto — ${month ? formatMonthLong(month) : ''}`}
    >
      <form onSubmit={handleSubmit} className="space-y-5">
        <p className="text-xs text-[#64748b]">
          Definí las metas mensuales. Dejá en 0 para no mostrar la barra de progreso.
        </p>

        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">
            Meta Crédito
          </label>
          <AmountInput
            value={form.creditLimit}
            onChange={(v) => set('creditLimit', v)}
            placeholder="0"
          />
          <p className="text-[10px] text-[#3d4466] mt-1">Gastos en tarjetas de crédito</p>
        </div>

        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">
            Meta Débito / Efectivo
          </label>
          <AmountInput
            value={form.debitCashLimit}
            onChange={(v) => set('debitCashLimit', v)}
            placeholder="0"
          />
          <p className="text-[10px] text-[#3d4466] mt-1">Gastos en débito o efectivo</p>
        </div>

        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">
            Meta semanal
          </label>
          <AmountInput
            value={form.weeklyLimit}
            onChange={(v) => set('weeklyLimit', v)}
            placeholder="0"
          />
          <p className="text-[10px] text-[#3d4466] mt-1">Límite por cada semana del mes</p>
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
            Guardar metas
          </button>
        </div>
      </form>
    </SlideOver>
  )
}
