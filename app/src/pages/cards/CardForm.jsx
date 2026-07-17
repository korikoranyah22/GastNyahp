import { useState, useEffect } from 'react'
import SlideOver from '../../components/ui/SlideOver'
import useStore from '../../store/useStore'
import { useTheme } from '../../hooks/useTheme'

/* Paleta modo oscuro — colores vivos */
const CARD_PRESETS_DARK = [
  { label: 'Azul',    value: '#3b82f6' },
  { label: 'Índigo',  value: '#6366f1' },
  { label: 'Violeta', value: '#7c3aed' },
  { label: 'Rosa',    value: '#ec4899' },
  { label: 'Rojo',    value: '#ef4444' },
  { label: 'Ámbar',   value: '#f59e0b' },
  { label: 'Verde',   value: '#10b981' },
  { label: 'Teal',    value: '#14b8a6' },
]

/* Paleta modo cozy — colores pastel */
const CARD_PRESETS_COZY = [
  { label: 'Rosa',    value: '#D4688A' },
  { label: 'Lila',    value: '#9B7ED4' },
  { label: 'Lavanda', value: '#A87CC8' },
  { label: 'Celeste', value: '#5EB3D5' },
  { label: 'Menta',   value: '#5BBFBC' },
  { label: 'Sage',    value: '#6FAF4A' },
  { label: 'Durazno', value: '#E09060' },
  { label: 'Dorado',  value: '#C0963A' },
]

const BLANK = {
  bankId: '',
  label: '',
  network: 'VISA',
  type: 'credit',
  closingDay: 15,
  dueDay: 5,
  color: '#3b82f6',
  active: true,
}

export default function CardForm({ open, onClose, card = null }) {
  const banks      = useStore((s) => s.banks)
  const addCard    = useStore((s) => s.addCard)
  const updateCard = useStore((s) => s.updateCard)
  const { isCozy } = useTheme()

  const [form, setForm] = useState(card || { ...BLANK, bankId: banks[0]?.id || '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const isEdit = !!card

  const PRESETS = isCozy ? CARD_PRESETS_COZY : CARD_PRESETS_DARK

  useEffect(() => {
    if (open) {
      setForm(card || { ...BLANK, bankId: banks[0]?.id || '' })
      setError('')
    }
  }, [open, card])

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  // Auto-set color from bank when bank changes
  const handleBankChange = (bankId) => {
    const bank = banks.find((b) => b.id === bankId)
    set('bankId', bankId)
    if (bank && !card) set('color', bank.color)
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.bankId) { setError('Seleccioná un banco'); return }
    if (!form.label.trim()) { setError('El nombre es requerido'); return }
    setError(''); setBusy(true)
    const result = await (isEdit ? updateCard(card.id, form) : addCard(form))
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }

  const selectedBank = banks.find((b) => b.id === form.bankId)

  return (
    <SlideOver open={open} onClose={onClose} title={isEdit ? 'Editar tarjeta' : 'Nueva tarjeta'}>
      <form onSubmit={handleSubmit} className="space-y-5">

        {/* Preview card */}
        <div
          className="relative h-32 rounded-xl p-4 overflow-hidden"
          style={{
            background: `linear-gradient(135deg, ${form.color}dd, ${form.color}88)`,
          }}
        >
          <div className="absolute inset-0 opacity-10"
            style={{ backgroundImage: 'radial-gradient(circle at 80% 20%, white, transparent 60%)' }}
          />
          <div className="flex justify-between items-start">
            <div>
              <p className="text-white/60 text-xs mb-0.5">
                {selectedBank?.name || 'Banco'}
              </p>
              {/* text-always-white: la etiqueta de tarjeta va sobre fondo degradado */}
              <p className="text-always-white font-bold text-base">{form.label || 'VISA / MASTER'}</p>
            </div>
            <div className="text-right">
              <p className="text-white/80 text-lg font-bold">{form.network}</p>
              <p className="text-white/60 text-xs">{form.type === 'credit' ? 'Crédito' : 'Débito'}</p>
            </div>
          </div>
          <div className="absolute bottom-4 left-4 right-4 flex justify-between">
            <p className="text-white/60 text-xs">Cierre día {form.closingDay}</p>
            <p className="text-white/60 text-xs">Vence día {form.dueDay}</p>
          </div>
        </div>

        {/* Banco */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Banco *</label>
          {banks.length === 0 ? (
            <p className="text-xs text-red-400">Primero debés crear un banco.</p>
          ) : (
            <select
              value={form.bankId}
              onChange={(e) => handleBankChange(e.target.value)}
              className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500"
            >
              {banks.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
          )}
        </div>

        {/* Etiqueta */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Etiqueta *</label>
          <input
            type="text"
            value={form.label}
            onChange={(e) => set('label', e.target.value)}
            placeholder="ej. VISA BBVA"
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 placeholder:text-[#3d4466]"
          />
        </div>

        {/* Red + Tipo */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Red</label>
            <div className="flex rounded-lg border border-[#2e3350] overflow-hidden">
              {['VISA', 'MASTERCARD'].map((net) => (
                <button
                  key={net}
                  type="button"
                  onClick={() => set('network', net)}
                  className={`flex-1 py-2 text-xs font-medium transition-colors ${
                    form.network === net
                      ? 'bg-blue-600 text-white'
                      : 'text-[#64748b] hover:text-white hover:bg-[#2e3350]'
                  }`}
                >
                  {net === 'MASTERCARD' ? 'Master' : net}
                </button>
              ))}
            </div>
          </div>
          <div>
            <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Tipo</label>
            <div className="flex rounded-lg border border-[#2e3350] overflow-hidden">
              {[['credit', 'Crédito'], ['debit', 'Débito']].map(([val, label]) => (
                <button
                  key={val}
                  type="button"
                  onClick={() => set('type', val)}
                  className={`flex-1 py-2 text-xs font-medium transition-colors ${
                    form.type === val
                      ? 'bg-blue-600 text-white'
                      : 'text-[#64748b] hover:text-white hover:bg-[#2e3350]'
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Días */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Día de cierre</label>
            <input
              type="number"
              min={1} max={31}
              value={form.closingDay}
              onChange={(e) => set('closingDay', Number(e.target.value))}
              className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Día de vencimiento</label>
            <input
              type="number"
              min={1} max={31}
              value={form.dueDay}
              onChange={(e) => set('dueDay', Number(e.target.value))}
              className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500"
            />
          </div>
        </div>

        {/* Color */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-2">Color de la tarjeta</label>
          {/* Presets */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 mb-2">
            {PRESETS.map((c) => (
              <button
                key={c.value}
                type="button"
                onClick={() => set('color', c.value)}
                className="relative flex flex-col items-center gap-1 p-2 rounded-lg border transition-all"
                style={{
                  borderColor:     form.color === c.value ? c.value        : (isCozy ? '#F7CDCD' : '#2e3350'),
                  backgroundColor: form.color === c.value ? `${c.value}22` : (isCozy ? '#F7EBEC' : '#1c2030'),
                }}
                title={c.label}
              >
                <span className="w-6 h-6 rounded-full" style={{ backgroundColor: c.value }} />
                <span className="text-[10px] text-[#64748b] truncate w-full text-center">{c.label}</span>
              </button>
            ))}
          </div>
          {/* Custom color picker */}
          <div className="flex items-center gap-2">
            <input
              type="color"
              value={form.color}
              onChange={(e) => set('color', e.target.value)}
              className="w-9 h-9 rounded cursor-pointer bg-transparent border-0"
            />
            <span className="text-xs text-[#64748b] font-mono">{form.color}</span>
          </div>
        </div>

        {error && <p className="text-xs text-red-400">{error}</p>}

        {/* Actions */}
        <div className="flex gap-2 pt-2">
          <button
            type="button"
            onClick={onClose}
            className="flex-1 px-4 py-2 rounded-lg border border-[#2e3350] text-sm text-[#94a3b8] hover:text-white transition-colors"
          >
            Cancelar
          </button>
          <button
            type="submit" disabled={busy}
            className="flex-1 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
          >
            {isEdit ? 'Guardar' : 'Crear tarjeta'}
          </button>
        </div>
      </form>
    </SlideOver>
  )
}
