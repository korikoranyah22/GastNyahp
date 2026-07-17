import { useState, useEffect } from 'react'
import SlideOver from '../../components/ui/SlideOver'
import AmountInput from '../../components/ui/AmountInput'
import MonthPicker from '../../components/ui/MonthPicker'
import OwnerBadge, { SHARED_OWNER } from '../../components/ui/OwnerBadge'
import CategoryPicker from '../../components/ui/CategoryPicker'
import useStore from '../../store/useStore'
import { generateMonths, addMonthsToYM } from '../../lib/dateUtils'
import { formatMonth } from '../../lib/formatters'

const BLANK = {
  description: '',
  category: 'Hogar',
  purchaseDate: new Date().toISOString().slice(0, 10),
  frequency: 'fixed',
  monthlyAmount: '',
  totalInstallments: 3,
  startMonth: new Date().toISOString().slice(0, 7),
  ownerId: null,
}

export default function InstallmentForm({ open, onClose, cardId, installment = null }) {
  const addInstallment = useStore((s) => s.addInstallment)
  const updateInstallment = useStore((s) => s.updateInstallment)
  const people = useStore((s) => s.people)

  const [form, setForm] = useState(BLANK)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const isEdit = !!installment

  useEffect(() => {
    if (open) {
      setForm(installment ? {
        description: installment.description,
        category: installment.category,
        purchaseDate: installment.purchaseDate,
        frequency: installment.frequency,
        monthlyAmount: installment.monthlyAmount,
        totalInstallments: installment.totalInstallments || 3,
        startMonth: installment.startMonth,
        ownerId: installment.ownerId || null,
      } : BLANK)
      setError('')
    }
  }, [open, installment])

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.description.trim()) { setError('La descripción es requerida'); return }
    if (!form.monthlyAmount) { setError('El monto es requerido'); return }

    setError(''); setBusy(true)
    const payload = { ...form, cardId }
    const result = await (isEdit ? updateInstallment(installment.id, payload) : addInstallment(payload))
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }

  // Preview months
  const previewMonths = form.frequency === 'monthly'
    ? generateMonths(form.startMonth, 4)
    : generateMonths(form.startMonth, Math.min(form.totalInstallments, 6))

  return (
    <SlideOver open={open} onClose={onClose} title={isEdit ? 'Editar cuota' : 'Nueva cuota'} width="max-w-lg">
      <form onSubmit={handleSubmit} className="space-y-5">

        {/* Descripción */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Descripción *</label>
          <input
            type="text"
            value={form.description}
            onChange={(e) => set('description', e.target.value)}
            placeholder="ej. Lavarropas, Netflix, Ropa"
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 placeholder:text-[#3d4466]"
          />
        </div>

        {/* Categoría */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Categoría</label>
          <CategoryPicker value={form.category} onChange={(v) => set('category', v)} />
        </div>

        {/* Fecha de compra */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Fecha de compra</label>
          <input
            type="date"
            value={form.purchaseDate}
            onChange={(e) => set('purchaseDate', e.target.value)}
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500"
          />
        </div>

        {/* Frecuencia */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Tipo de cuota</label>
          <div className="flex rounded-lg border border-[#2e3350] overflow-hidden">
            {[['fixed', 'Cuotas fijas'], ['monthly', 'Mensual recurrente']].map(([val, label]) => (
              <button
                key={val}
                type="button"
                onClick={() => set('frequency', val)}
                className={`flex-1 py-2 text-xs font-medium transition-colors ${
                  form.frequency === val
                    ? 'bg-blue-600 text-white'
                    : 'text-[#64748b] hover:text-white hover:bg-[#2e3350]'
                }`}
              >
                {label}
              </button>
            ))}
          </div>
        </div>

        {/* Monto */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Monto por cuota *</label>
          <AmountInput
            value={form.monthlyAmount}
            onChange={(v) => set('monthlyAmount', v)}
            placeholder="0"
          />
        </div>

        {/* Cantidad de cuotas (solo si fixed) */}
        {form.frequency === 'fixed' && (
          <div>
            <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Cantidad de cuotas</label>
            <input
              type="number"
              min={1} max={120}
              value={form.totalInstallments}
              onChange={(e) => set('totalInstallments', Number(e.target.value))}
              className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500"
            />
          </div>
        )}

        {/* Mes de inicio */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Mes de inicio</label>
          <MonthPicker
            value={form.startMonth}
            onChange={(ym) => set('startMonth', ym)}
          />
        </div>

        {/* Preview */}
        {form.monthlyAmount > 0 && (
          <div className="p-3 rounded-lg bg-[#1c2030] border border-[#2e3350]">
            <p className="text-[10px] text-[#64748b] mb-2 uppercase tracking-wider">
              Vista previa {form.frequency === 'monthly' ? '(primeros 4 meses)' : `(${Math.min(form.totalInstallments, 6)} de ${form.totalInstallments})`}
            </p>
            <div className="flex flex-wrap gap-2">
              {previewMonths.map((m, i) => (
                <div key={m} className="text-center">
                  <p className="text-[10px] text-[#64748b]">{formatMonth(m)}</p>
                  <p className="text-xs font-semibold text-white mono">
                    ${Number(form.monthlyAmount).toLocaleString('es-AR')}
                  </p>
                  {form.frequency === 'fixed' && (
                    <p className="text-[10px] text-[#3d4466]">{i + 1}/{form.totalInstallments}</p>
                  )}
                </div>
              ))}
              {form.frequency === 'monthly' && (
                <div className="text-center self-center">
                  <p className="text-[10px] text-[#3d4466]">···</p>
                </div>
              )}
            </div>
            {form.frequency === 'fixed' && form.monthlyAmount && (
              <p className="text-xs text-[#64748b] mt-2 border-t border-[#2e3350] pt-2">
                Total: <span className="text-white font-semibold mono">
                  ${(form.monthlyAmount * form.totalInstallments).toLocaleString('es-AR')}
                </span>
              </p>
            )}
          </div>
        )}

        {/* Persona */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">¿De quién es esta cuota?</label>
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

        <div className="flex gap-2 pt-2">
          <button type="button" onClick={onClose}
            className="flex-1 px-4 py-2 rounded-lg border border-[#2e3350] text-sm text-[#94a3b8] hover:text-white transition-colors">
            Cancelar
          </button>
          <button type="submit" disabled={busy}
            className="flex-1 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors">
            {isEdit ? 'Guardar' : 'Agregar cuota'}
          </button>
        </div>
      </form>
    </SlideOver>
  )
}
