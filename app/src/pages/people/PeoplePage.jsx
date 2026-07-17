import { useState, useEffect } from 'react'
import { Plus, Pencil, Trash2, Users } from 'lucide-react'
import useStore from '../../store/useStore'
import SlideOver from '../../components/ui/SlideOver'
import EmptyState from '../../components/ui/EmptyState'

const PRESET_COLORS = [
  '#3b82f6', '#8b5cf6', '#ec4899', '#f97316',
  '#10b981', '#f59e0b', '#06b6d4', '#ef4444',
]

const FIELD = 'w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30'

const EMPTY = { name: '', emoji: '😀', color: PRESET_COLORS[0] }

function PersonForm({ open, onClose, person = null }) {
  const addPerson    = useStore((s) => s.addPerson)
  const updatePerson = useStore((s) => s.updatePerson)

  const [form, setForm] = useState(EMPTY)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const set = (key, val) => setForm((f) => ({ ...f, [key]: val }))

  useEffect(() => {
    if (!open) return
    setError('')
    setForm(person ? { name: person.name, emoji: person.emoji || '😀', color: person.color } : EMPTY)
  }, [open, person])

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.name.trim()) return setError('Ingresá un nombre.')
    const payload = { name: form.name.trim(), emoji: form.emoji, color: form.color }
    setError(''); setBusy(true)
    const result = await (person ? updatePerson(person.id, payload) : addPerson(payload))
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }

  return (
    <SlideOver open={open} onClose={onClose} title={person ? 'Editar persona' : 'Nueva persona'}>
      <form onSubmit={handleSubmit} className="space-y-5">
        {/* Emoji + nombre */}
        <div className="flex gap-3">
          <div>
            <label className="block text-xs font-medium text-[#64748b] mb-1.5">Emoji</label>
            <input
              type="text"
              value={form.emoji}
              onChange={(e) => set('emoji', e.target.value)}
              className="w-16 px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm text-center focus:outline-none focus:border-blue-500"
              maxLength={2}
            />
          </div>
          <div className="flex-1">
            <label className="block text-xs font-medium text-[#64748b] mb-1.5">Nombre</label>
            <input
              type="text"
              value={form.name}
              onChange={(e) => set('name', e.target.value)}
              placeholder="ej. Miyu, Cami, Michis"
              className={FIELD}
              autoFocus
            />
          </div>
        </div>

        {/* Color */}
        <div>
          <label className="block text-xs font-medium text-[#64748b] mb-1.5">Color</label>
          <div className="grid grid-cols-8 gap-2">
            {PRESET_COLORS.map((c) => (
              <button
                key={c}
                type="button"
                onClick={() => set('color', c)}
                className="w-8 h-8 rounded-full border-2 transition-all"
                style={{
                  backgroundColor: c,
                  borderColor: form.color === c ? 'white' : 'transparent',
                  transform: form.color === c ? 'scale(1.15)' : 'scale(1)',
                }}
              />
            ))}
          </div>
          {/* Preview */}
          <div className="mt-3 flex items-center gap-2">
            <span
              className="w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold"
              style={{ backgroundColor: `${form.color}33`, color: form.color, border: `1.5px solid ${form.color}66` }}
            >
              {form.name ? form.name.charAt(0).toUpperCase() : '?'}
            </span>
            <span className="text-sm text-white">{form.name || 'Vista previa'}</span>
            <span className="text-sm">{form.emoji}</span>
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
            {person ? 'Guardar' : 'Agregar'}
          </button>
        </div>
      </form>
    </SlideOver>
  )
}

export default function PeoplePage() {
  const people       = useStore((s) => s.people)
  const deletePerson = useStore((s) => s.deletePerson)

  const [formOpen, setFormOpen]   = useState(false)
  const [editPerson, setEditPerson] = useState(null)

  const handleEdit = (p) => { setEditPerson(p); setFormOpen(true) }
  const handleClose = () => { setFormOpen(false); setEditPerson(null) }
  const handleDelete = async (p) => {
    if (!window.confirm(`¿Eliminar a "${p.name}"?`)) return
    const result = await deletePerson(p.id)
    if (result.error) window.alert(result.error)
  }

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-lg font-semibold text-white">Familia</h2>
          <p className="text-xs text-[#64748b] mt-0.5">Personas para etiquetar gastos, cuotas y servicios</p>
        </div>
        <button
          onClick={() => setFormOpen(true)}
          className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
        >
          <Plus size={15} />
          Nueva persona
        </button>
      </div>

      {people.length === 0 ? (
        <EmptyState
          icon={Users}
          title="Sin personas configuradas"
          description="Agregá a los miembros de tu familia para etiquetar de quién nace cada gasto."
          action={
            <button
              onClick={() => setFormOpen(true)}
              className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
            >
              <Plus size={15} />
              Agregar persona
            </button>
          }
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {people.map((p) => (
            <div
              key={p.id}
              className="relative group bg-[#151820] border border-[#2e3350] rounded-xl p-5 hover:border-[#3d4466] transition-colors"
            >
              <div className="absolute top-0 left-0 right-0 h-1 rounded-t-xl" style={{ backgroundColor: p.color }} />
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div
                    className="w-10 h-10 rounded-full flex items-center justify-center text-lg font-bold"
                    style={{ backgroundColor: `${p.color}33`, color: p.color, border: `2px solid ${p.color}66` }}
                  >
                    {p.name.charAt(0).toUpperCase()}
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-white">{p.name}</p>
                    <p className="text-base leading-none mt-0.5">{p.emoji}</p>
                  </div>
                </div>
                <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                  <button
                    onClick={() => handleEdit(p)}
                    className="p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#2e3350] transition-colors"
                  >
                    <Pencil size={13} />
                  </button>
                  <button
                    onClick={() => handleDelete(p)}
                    className="p-1.5 rounded-lg text-[#64748b] hover:text-red-400 hover:bg-red-500/10 transition-colors"
                  >
                    <Trash2 size={13} />
                  </button>
                </div>
              </div>
            </div>
          ))}

          <button
            onClick={() => setFormOpen(true)}
            className="border-2 border-dashed border-[#2e3350] rounded-xl p-5 flex flex-col items-center justify-center gap-2 text-[#3d4466] hover:text-[#64748b] hover:border-[#3d4466] transition-colors min-h-[90px]"
          >
            <Plus size={20} />
            <span className="text-xs font-medium">Agregar persona</span>
          </button>
        </div>
      )}

      <PersonForm open={formOpen} onClose={handleClose} person={editPerson} />
    </div>
  )
}
