import { useState, useEffect } from 'react'
import SlideOver from '../../components/ui/SlideOver'
import AmountInput from '../../components/ui/AmountInput'
import OwnerBadge, { SHARED_OWNER } from '../../components/ui/OwnerBadge'
import useStore from '../../store/useStore'

export const SERVICE_CATEGORIES = [
  { value: 'Electricidad', label: 'Electricidad', icon: '⚡' },
  { value: 'Gas',          label: 'Gas',          icon: '🔥' },
  { value: 'Agua',         label: 'Agua',         icon: '💧' },
  { value: 'Conectividad', label: 'Conectividad', icon: '📡' },
  { value: 'Streaming',    label: 'Streaming',    icon: '🎬' },
  { value: 'Digital',      label: 'Digital',      icon: '🤖' },
  { value: 'Seguro',       label: 'Seguro',       icon: '🛡️' },
  { value: 'Expensas',     label: 'Expensas',     icon: '🏢' },
  { value: 'Telecom',      label: 'Telecom',      icon: '📱' },
  { value: 'Otros',        label: 'Otros',        icon: '📌' },
]

const BLANK = {
  name: '',
  category: 'Electricidad',
  billingType: 'monthly',
  linkedCardId: null,
  active: true,
  baseAmount: '',
  currency: 'ARS',
  ownerId: null,
}

export default function ServiceForm({ open, onClose, service = null }) {
  const creditCards = useStore((s) => s.creditCards)
  const people = useStore((s) => s.people)
  const addService = useStore((s) => s.addService)
  const updateService = useStore((s) => s.updateService)
  const updateServiceFutureAmounts = useStore((s) => s.updateServiceFutureAmounts)
  const currentMonth = useStore((s) => s.currentMonth)
  const usdRateCCL = useStore((s) => s.income.usdRateCCL)

  const [form, setForm] = useState(BLANK)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const isEdit = !!service

  useEffect(() => {
    if (open) {
      if (service) {
        // En edición: mostrar el monto del mes actual (o el original si es USD)
        const monthAmount = service.amounts?.find((a) => a.month === currentMonth)?.amount
        const currentAmount = service.originalCurrency === 'USD'
          ? (service.originalBaseAmount || '')
          : (monthAmount ?? '')
        setForm({
          name: service.name,
          category: service.category,
          billingType: service.billingType,
          linkedCardId: service.linkedCardId,
          active: service.active,
          baseAmount: currentAmount,
          currency: service.currency || 'ARS',
          ownerId: service.ownerId || null,
        })
      } else {
        setForm(BLANK)
      }
      setError('')
    }
  }, [open, service])

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  const amountARS = form.currency === 'USD' && usdRateCCL > 0
    ? Math.round(Number(form.baseAmount || 0) * usdRateCCL)
    : null

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.name.trim()) { setError('El nombre es requerido'); return }
    if (!form.baseAmount && !isEdit) { setError('El monto es requerido'); return }
    if (form.currency === 'USD' && !usdRateCCL) { setError('Configurá el tipo de cambio CCL en Ajustes.'); return }

    setError(''); setBusy(true)
    let result
    if (isEdit) {
      result = await updateService(service.id, {
        name: form.name,
        category: form.category,
        billingType: form.billingType,
        linkedCardId: form.linkedCardId,
        active: form.active,
        currency: form.currency,
        ownerId: form.ownerId || null,
      })
      if (!result.error && form.baseAmount) {
        const finalAmount = form.currency === 'USD'
          ? Math.round(Number(form.baseAmount) * usdRateCCL)
          : Number(form.baseAmount)
        result = await updateServiceFutureAmounts(service.id, currentMonth, finalAmount)
      }
    } else {
      const finalAmount = form.currency === 'USD'
        ? Math.round(Number(form.baseAmount) * usdRateCCL)
        : Number(form.baseAmount)
      result = await addService({
        ...form,
        baseAmount: finalAmount,
        ...(form.currency === 'USD' && {
          originalBaseAmount: Number(form.baseAmount),
          originalCurrency: 'USD',
        }),
      })
    }
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }
  const linkedCard = creditCards.find((c) => c.id === form.linkedCardId)
  const catIcon = SERVICE_CATEGORIES.find((c) => c.value === form.category)?.icon || '📌'

  return (
    <SlideOver open={open} onClose={onClose} title={isEdit ? 'Editar servicio' : 'Nuevo servicio'}>
      <form onSubmit={handleSubmit} className="space-y-5">

        {/* Preview */}
        <div className="p-3 rounded-lg bg-[#1c2030] border border-[#2e3350] flex items-center gap-3">
          <span className="text-2xl">{catIcon}</span>
          <div>
            <p className="text-sm font-semibold text-white">{form.name || 'Nombre del servicio'}</p>
            <p className="text-xs text-[#64748b]">
              {form.category}
              {linkedCard
                ? <span className="ml-2" style={{ color: linkedCard.color }}>· {linkedCard.label}</span>
                : <span className="ml-2 text-[#3d4466]">· Pago directo</span>
              }
            </p>
          </div>
        </div>

        {/* Nombre */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Nombre *</label>
          <input
            type="text"
            value={form.name}
            onChange={(e) => set('name', e.target.value)}
            placeholder="ej. Edesur, Netflix, Movistar"
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 placeholder:text-[#3d4466]"
          />
        </div>

        {/* Categoría */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-2">Categoría</label>
          <div className="grid grid-cols-2 gap-1.5">
            {SERVICE_CATEGORIES.map((cat) => (
              <button
                key={cat.value}
                type="button"
                onClick={() => set('category', cat.value)}
                className={`flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-medium text-left transition-colors border ${
                  form.category === cat.value
                    ? 'bg-blue-600/20 text-blue-400 border-blue-600/30'
                    : 'text-[#64748b] border-[#2e3350] hover:text-white hover:bg-[#2e3350]'
                }`}
              >
                <span>{cat.icon}</span>
                <span>{cat.label}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Frecuencia */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Frecuencia de cobro</label>
          <div className="flex rounded-lg border border-[#2e3350] overflow-hidden">
            {[['monthly', 'Mensual'], ['bimonthly', 'Bimensual'], ['quarterly', 'Trimestral']].map(([v, l]) => (
              <button
                key={v}
                type="button"
                onClick={() => set('billingType', v)}
                className={`flex-1 py-2 text-xs font-medium transition-colors ${
                  form.billingType === v
                    ? 'bg-blue-600 text-white'
                    : 'text-[#64748b] hover:text-white hover:bg-[#2e3350]'
                }`}
              >
                {l}
              </button>
            ))}
          </div>
        </div>

        {/* Tarjeta vinculada */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Medio de pago</label>
          <div className="space-y-1">
            <button
              type="button"
              onClick={() => set('linkedCardId', null)}
              className={`w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-left border transition-colors ${
                !form.linkedCardId
                  ? 'bg-blue-600/20 text-blue-400 border-blue-600/30'
                  : 'text-[#64748b] border-[#2e3350] hover:text-white hover:bg-[#2e3350]'
              }`}
            >
              <span className="text-base">🏦</span>
              <span className="text-xs font-medium">Independiente (débito / transferencia)</span>
            </button>
            {creditCards.filter((c) => c.type === 'credit').map((card) => (
              <button
                key={card.id}
                type="button"
                onClick={() => set('linkedCardId', card.id)}
                className={`w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-left border transition-colors ${
                  form.linkedCardId === card.id
                    ? 'border-current'
                    : 'text-[#64748b] border-[#2e3350] hover:text-white hover:bg-[#2e3350]'
                }`}
                style={form.linkedCardId === card.id ? {
                  backgroundColor: `${card.color}22`,
                  borderColor: `${card.color}66`,
                  color: card.color,
                } : {}}
              >
                <span className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: card.color }} />
                <span className="text-xs font-medium">{card.label}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Moneda */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Moneda</label>
          <div className="flex rounded-lg border border-[#2e3350] overflow-hidden">
            {[['ARS', '$ Pesos'], ['USD', '🇺🇸 Dólares']].map(([c, l]) => (
              <button
                key={c}
                type="button"
                onClick={() => set('currency', c)}
                className={`flex-1 py-2 text-xs font-medium transition-colors ${
                  form.currency === c
                    ? 'bg-blue-600 text-white'
                    : 'text-[#64748b] hover:text-white hover:bg-[#2e3350]'
                }`}
              >
                {l}
              </button>
            ))}
          </div>
        </div>

        {/* Monto mensual */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">
            {isEdit ? 'Nuevo monto mensual' : 'Monto mensual base'}{!isEdit && ' *'}
            <span className="ml-1 font-normal text-[#3d4466]">
              {isEdit ? '(actualiza este mes y los próximos 12)' : '(se carga para los próximos 12 meses)'}
            </span>
          </label>
          <AmountInput value={form.baseAmount} onChange={(v) => set('baseAmount', v)} />
          {form.currency === 'USD' && amountARS !== null && amountARS > 0 && (
            <p className="text-[10px] text-[#64748b] mt-1">
              ≈ ${amountARS.toLocaleString('es-AR')} ARS al CCL
            </p>
          )}
          {form.currency === 'USD' && !usdRateCCL && (
            <p className="text-[10px] text-amber-400 mt-1">
              ⚠ Configurá el tipo de cambio CCL en Ajustes
            </p>
          )}
        </div>

        {/* Activo */}
        <div className="flex items-center justify-between">
          <label className="text-xs font-medium text-[#94a3b8]">Servicio activo</label>
          <button
            type="button"
            onClick={() => set('active', !form.active)}
            className={`relative w-10 h-5 rounded-full transition-colors ${form.active ? 'bg-blue-600' : 'bg-[#2e3350]'}`}
          >
            <span
              className={`absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white transition-transform ${form.active ? 'translate-x-5' : ''}`}
            />
          </button>
        </div>

        {/* Persona */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">¿De quién es este servicio?</label>
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
            {isEdit ? 'Guardar' : 'Crear servicio'}
          </button>
        </div>
      </form>
    </SlideOver>
  )
}
