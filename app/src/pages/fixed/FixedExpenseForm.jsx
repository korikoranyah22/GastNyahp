import { useState, useEffect } from 'react'
import { RefreshCw } from 'lucide-react'
import SlideOver from '../../components/ui/SlideOver'
import AmountInput from '../../components/ui/AmountInput'
import useStore from '../../store/useStore'

const ICONS = ['👤', '💵', '💳', '📊', '🏠', '🏥', '🎓', '🐾', '💼', '🎁', '⚡', '📱']

const TYPES = [
  { value: 'reserve', label: 'Reserva personal',   desc: 'Dinero para una persona específica',   color: '#3b82f6' },
  { value: 'cash',    label: 'Efectivo',            desc: 'Fondo de efectivo mensual',           color: '#f59e0b' },
  { value: 'debt',    label: 'Deuda / saldo',       desc: 'Pago de saldo impago o deuda',        color: '#ef4444' },
  { value: 'other',   label: 'Estimado variable',   desc: 'Gasto estimado difícil de predecir',  color: '#8b5cf6' },
]

const EMPTY = {
  label:      '',
  type:       'reserve',
  icon:       '👤',
  recurring:  false,
  baseAmount: '',
}

export default function FixedExpenseForm({ open, onClose, item = null }) {
  const addFixedExpense    = useStore((s) => s.addFixedExpense)
  const updateFixedExpense = useStore((s) => s.updateFixedExpense)

  const [form, setForm]   = useState(EMPTY)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!open) return
    setError('')
    if (item) {
      setForm({
        label:      item.label,
        type:       item.type,
        icon:       item.icon,
        recurring:  item.recurring || false,
        baseAmount: item.baseAmount || '',
      })
    } else {
      setForm(EMPTY)
    }
  }, [open, item])

  const patch = (key, val) => setForm((f) => ({ ...f, [key]: val }))

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.label.trim()) return setError('Ingresá un nombre para la reserva.')
    if (form.recurring && !form.baseAmount) return setError('Ingresá el monto base mensual.')

    const payload = {
      label:      form.label.trim(),
      type:       form.type,
      icon:       form.icon,
      recurring:  form.recurring,
      baseAmount: form.recurring ? (Number(form.baseAmount) || 0) : 0,
    }

    if (item) {
      // Si cambia recurring/baseAmount y el item era recurrente, limpiar months
      const clearedMonths = (item.recurring !== form.recurring || item.baseAmount !== payload.baseAmount)
        ? { months: [] }
        : {}
      payload.months = clearedMonths.months
    }

    setError(''); setBusy(true)
    const result = await (item ? updateFixedExpense(item.id, payload) : addFixedExpense(payload))
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }

  const selectedType = TYPES.find((t) => t.value === form.type) || TYPES[0]

  return (
    <SlideOver
      open={open}
      onClose={onClose}
      title={item ? 'Editar reserva' : 'Nueva reserva'}
    >
      <form onSubmit={handleSubmit} className="space-y-5">
        <p className="text-xs text-[#64748b]">
          Las reservas son montos que apartás al inicio del mes. Las periódicas repiten el mismo monto base
          en todos los meses, con posibilidad de sobreescribir uno en particular.
        </p>

        {/* Nombre */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">Nombre</label>
          <input
            type="text"
            value={form.label}
            onChange={(e) => patch('label', e.target.value)}
            placeholder="ej. Miyu, Cash, Emergencias..."
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30"
            autoFocus
          />
        </div>

        {/* Periódica toggle */}
        <div>
          <button
            type="button"
            onClick={() => patch('recurring', !form.recurring)}
            className={`w-full flex items-start gap-3 px-4 py-3 rounded-xl border text-left transition-all ${
              form.recurring
                ? 'border-emerald-500/50 bg-emerald-500/8'
                : 'border-[#2e3350] hover:border-[#3d4466]'
            }`}
          >
            <div className={`w-8 h-5 rounded-full flex-shrink-0 relative mt-0.5 transition-colors ${form.recurring ? 'bg-emerald-500' : 'bg-[#2e3350]'}`}>
              <span className={`absolute top-0.5 w-4 h-4 rounded-full bg-white transition-all ${form.recurring ? 'left-3.5' : 'left-0.5'}`} />
            </div>
            <div>
              <div className="flex items-center gap-1.5">
                <RefreshCw size={12} className={form.recurring ? 'text-emerald-400' : 'text-[#64748b]'} />
                <p className={`text-sm font-medium ${form.recurring ? 'text-emerald-400' : 'text-[#94a3b8]'}`}>
                  Reserva periódica
                </p>
              </div>
              <p className="text-xs text-[#64748b] mt-0.5">
                El mismo monto se repite automáticamente en todos los meses
              </p>
            </div>
          </button>

          {/* Base amount — solo si periódica */}
          {form.recurring && (
            <div className="mt-3">
              <label className="block text-xs font-medium text-[#64748b] mb-1.5">
                Monto base mensual
              </label>
              <AmountInput
                value={form.baseAmount}
                onChange={(v) => patch('baseAmount', v)}
                placeholder="0"
              />
              <p className="text-[10px] text-[#64748b] mt-1">
                Se puede sobreescribir mes a mes desde la lista principal
              </p>
            </div>
          )}
        </div>

        {/* Tipo */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-2">Tipo</label>
          <div className="space-y-2">
            {TYPES.map((t) => (
              <button
                key={t.value}
                type="button"
                onClick={() => patch('type', t.value)}
                className={`w-full flex items-start gap-3 px-3 py-2.5 rounded-lg border text-left transition-all ${
                  form.type === t.value ? 'border-transparent' : 'border-[#2e3350] hover:border-[#3d4466]'
                }`}
                style={
                  form.type === t.value
                    ? { borderColor: t.color, backgroundColor: `${t.color}18` }
                    : {}
                }
              >
                <span
                  className="w-2.5 h-2.5 rounded-full shrink-0 mt-1"
                  style={{ backgroundColor: t.color }}
                />
                <div>
                  <p className={`text-sm font-medium ${form.type === t.value ? 'text-white' : 'text-[#94a3b8]'}`}>
                    {t.label}
                  </p>
                  <p className="text-xs text-[#64748b]">{t.desc}</p>
                </div>
              </button>
            ))}
          </div>
        </div>

        {/* Ícono */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-2">Ícono</label>
          <div className="flex flex-wrap gap-2">
            {ICONS.map((ic) => (
              <button
                key={ic}
                type="button"
                onClick={() => patch('icon', ic)}
                className={`w-9 h-9 rounded-lg border text-lg flex items-center justify-center transition-all ${
                  form.icon === ic
                    ? 'border-blue-500 bg-blue-500/15'
                    : 'border-[#2e3350] hover:border-[#3d4466]'
                }`}
              >
                {ic}
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
            className="flex-1 px-4 py-2 rounded-lg text-white text-sm font-medium transition-colors"
            style={{ backgroundColor: selectedType.color }}
          >
            {item ? 'Guardar' : 'Agregar reserva'}
          </button>
        </div>
      </form>
    </SlideOver>
  )
}
