import { useState, useRef, useEffect } from 'react'
import { ChevronLeft, ChevronRight, CalendarDays } from 'lucide-react'

const MONTHS = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun',
                'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic']

/**
 * MonthPicker — reemplaza <input type="month">
 * value: 'YYYY-MM'   onChange(ym: string) => void
 */
export default function MonthPicker({ value, onChange }) {
  const [open, setOpen] = useState(false)
  const [pickerYear, setPickerYear] = useState(
    () => value ? parseInt(value.slice(0, 4)) : new Date().getFullYear()
  )
  const ref = useRef(null)

  // Sincronizar año del picker cuando el valor cambia externamente
  useEffect(() => {
    if (value) setPickerYear(parseInt(value.slice(0, 4)))
  }, [value])

  // Cerrar al hacer click fuera
  useEffect(() => {
    if (!open) return
    const handler = (e) => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [open])

  const selYear  = value ? parseInt(value.slice(0, 4)) : null
  const selMonth = value ? parseInt(value.slice(5, 7)) : null

  const handleSelect = (monthIdx) => {
    onChange(`${pickerYear}-${String(monthIdx + 1).padStart(2, '0')}`)
    setOpen(false)
  }

  const display = value
    ? `${MONTHS[parseInt(value.slice(5, 7)) - 1]} ${value.slice(0, 4)}`
    : 'Seleccionar mes'

  return (
    <div className="relative" ref={ref}>
      {/* Trigger button */}
      <button
        type="button"
        onClick={() => setOpen(v => !v)}
        className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-sm text-white flex items-center justify-between hover:border-[#3d4466] transition-colors focus:outline-none focus:border-blue-500"
      >
        <span>{display}</span>
        <CalendarDays size={14} className="text-[#64748b] shrink-0" />
      </button>

      {/* Dropdown panel */}
      {open && (
        <div className="absolute z-50 top-full mt-1 left-0 right-0 bg-[#151820] border border-[#2e3350] rounded-xl shadow-2xl p-3 fade-in">
          {/* Year navigation */}
          <div className="flex items-center justify-between mb-3">
            <button
              type="button"
              onClick={() => setPickerYear(y => y - 1)}
              className="p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#1c2030] transition-colors"
            >
              <ChevronLeft size={14} />
            </button>
            <span className="text-sm font-bold text-white tabular-nums">{pickerYear}</span>
            <button
              type="button"
              onClick={() => setPickerYear(y => y + 1)}
              className="p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#1c2030] transition-colors"
            >
              <ChevronRight size={14} />
            </button>
          </div>

          {/* Month grid 4×3 */}
          <div className="grid grid-cols-4 gap-1">
            {MONTHS.map((name, i) => {
              const isActive = selYear === pickerYear && selMonth === i + 1
              return (
                <button
                  key={name}
                  type="button"
                  onClick={() => handleSelect(i)}
                  className={`py-2 rounded-lg text-xs font-medium transition-colors ${
                    isActive
                      ? 'bg-blue-600 text-white'
                      : 'text-[#94a3b8] hover:bg-[#2e3350] hover:text-white'
                  }`}
                >
                  {name}
                </button>
              )
            })}
          </div>
        </div>
      )}
    </div>
  )
}
