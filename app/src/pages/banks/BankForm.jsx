import { useState, useEffect } from 'react'
import SlideOver from '../../components/ui/SlideOver'
import useStore from '../../store/useStore'
import { useTheme } from '../../hooks/useTheme'

/* Paleta modo oscuro — colores vivos, típicos de bancos */
const PRESET_COLORS_DARK = [
  { label: 'BBVA',      value: '#004B9B' },
  { label: 'Galicia',   value: '#E30613' },
  { label: 'Santander', value: '#CC0000' },
  { label: 'Macro',     value: '#F5A800' },
  { label: 'Naranja',   value: '#FF6B35' },
  { label: 'Verde',     value: '#16A34A' },
  { label: 'Violeta',   value: '#7C3AED' },
  { label: 'Celeste',   value: '#0EA5E9' },
]

/* Paleta modo cozy — colores pastel armónicos con la paleta kawaii */
const PRESET_COLORS_COZY = [
  { label: 'Rosa',    value: '#D4688A' },
  { label: 'Lavanda', value: '#8B72BE' },
  { label: 'Verde',   value: '#5B9A3E' },
  { label: 'Celeste', value: '#4A9FC0' },
  { label: 'Menta',   value: '#3EAAAA' },
  { label: 'Durazno', value: '#D4784A' },
  { label: 'Lila',    value: '#A87CC8' },
  { label: 'Dorado',  value: '#B8922A' },
]

const BLANK = { name: '', color: '#004B9B', alias: '', icon: 'building-2' }

export default function BankForm({ open, onClose, bank = null }) {
  const addBank    = useStore((s) => s.addBank)
  const updateBank = useStore((s) => s.updateBank)
  const { isCozy } = useTheme()

  const [form, setForm] = useState(bank || BLANK)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  // Resetear formulario cada vez que se abre (ya sea para crear o editar)
  useEffect(() => {
    if (open) {
      setForm(bank || BLANK)
      setError('')
    }
  }, [open, bank])

  const isEdit = !!bank
  const PRESET_COLORS = isCozy ? PRESET_COLORS_COZY : PRESET_COLORS_DARK

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.name.trim()) { setError('El nombre es requerido'); return }
    setError(''); setBusy(true)
    const result = await (isEdit ? updateBank(bank.id, form) : addBank(form))
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }

  return (
    <SlideOver open={open} onClose={onClose} title={isEdit ? 'Editar banco' : 'Nuevo banco'}>
      <form onSubmit={handleSubmit} className="space-y-5">
        {/* Nombre */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Nombre *</label>
          <input
            type="text"
            value={form.name}
            onChange={(e) => set('name', e.target.value)}
            placeholder="ej. BBVA, Galicia"
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30 placeholder:text-[#3d4466]"
          />
        </div>

        {/* Alias */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Alias (opcional)</label>
          <input
            type="text"
            value={form.alias}
            onChange={(e) => set('alias', e.target.value)}
            placeholder="ej. BBVA Personal"
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30 placeholder:text-[#3d4466]"
          />
        </div>

        {/* Color */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-2">Color</label>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
            {PRESET_COLORS.map((c) => (
              <button
                key={c.value}
                type="button"
                onClick={() => set('color', c.value)}
                className="relative flex flex-col items-center gap-1 p-2 rounded-lg border transition-all"
                style={{
                  borderColor:     form.color === c.value ? c.value          : (isCozy ? '#F7CDCD' : '#2e3350'),
                  backgroundColor: form.color === c.value ? `${c.value}22`   : (isCozy ? '#F7EBEC' : '#1c2030'),
                }}
                title={c.label}
              >
                <span className="w-6 h-6 rounded-full" style={{ backgroundColor: c.value }} />
                <span className="text-[10px] text-[#64748b] truncate w-full text-center">{c.label}</span>
              </button>
            ))}
          </div>
          {/* Custom color */}
          <div className="mt-2 flex items-center gap-2">
            <input
              type="color"
              value={form.color}
              onChange={(e) => set('color', e.target.value)}
              className="w-8 h-8 rounded cursor-pointer bg-transparent border-0"
            />
            <span className="text-xs text-[#64748b]">Color personalizado: <span className="font-mono">{form.color}</span></span>
          </div>
        </div>

        {/* Preview */}
        <div className="p-3 rounded-lg bg-[#1c2030] border border-[#2e3350]">
          <p className="text-[10px] text-[#64748b] mb-2 uppercase tracking-wider">Vista previa</p>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl flex items-center justify-center" style={{ backgroundColor: form.color }}>
              {/* text-always-white: la letra debe ser blanca sobre el fondo coloreado */}
              <span className="text-always-white font-bold text-lg">{(form.name || '?').charAt(0).toUpperCase()}</span>
            </div>
            <div>
              <p className="text-sm font-semibold text-white">{form.name || 'Nombre del banco'}</p>
              {form.alias && <p className="text-xs text-[#64748b]">{form.alias}</p>}
            </div>
          </div>
        </div>

        {error && <p className="text-xs text-red-400">{error}</p>}

        {/* Actions */}
        <div className="flex gap-2 pt-2">
          <button
            type="button"
            onClick={onClose}
            className="flex-1 px-4 py-2 rounded-lg border border-[#2e3350] text-sm text-[#94a3b8] hover:text-white hover:bg-[#1c2030] transition-colors"
          >
            Cancelar
          </button>
          <button
            type="submit" disabled={busy}
            className="flex-1 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
          >
            {isEdit ? 'Guardar cambios' : 'Crear banco'}
          </button>
        </div>
      </form>
    </SlideOver>
  )
}
