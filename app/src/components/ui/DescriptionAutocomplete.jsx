import { useState, useRef, useEffect } from 'react'

const FIELD = 'w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30'

/**
 * Input de texto con dropdown de autocompletado basado en historial.
 *
 * Props:
 *   value          — valor actual (string)
 *   onChange       — (string) => void
 *   suggestions    — string[] ya deduplicados y ordenados por frecuencia
 *   placeholder    — placeholder del input
 *   autoFocus      — boolean
 *   onClick        — handler adicional para el input (ej. stopPropagation en cards)
 *   maxSuggestions — máximo de sugerencias visibles (default 7)
 */
export default function DescriptionAutocomplete({
  value,
  onChange,
  suggestions = [],
  placeholder = '',
  autoFocus = false,
  onClick,
  maxSuggestions = 7,
}) {
  const [open, setOpen]         = useState(false)
  const [activeIdx, setActiveIdx] = useState(-1)
  const containerRef            = useRef(null)
  const inputRef                = useRef(null)

  // Filtrar sugerencias que contengan el texto (mínimo 1 carácter)
  const filtered = value.trim().length === 0
    ? []
    : suggestions
        .filter((s) => s.toLowerCase().includes(value.toLowerCase().trim()))
        .filter((s) => s.toLowerCase() !== value.toLowerCase().trim())
        .slice(0, maxSuggestions)

  const showDropdown = open && filtered.length > 0

  // Cerrar al hacer click fuera
  useEffect(() => {
    const handler = (e) => {
      if (containerRef.current && !containerRef.current.contains(e.target)) {
        setOpen(false)
        setActiveIdx(-1)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const select = (s) => {
    onChange(s)
    setOpen(false)
    setActiveIdx(-1)
    inputRef.current?.blur()
  }

  const handleChange = (e) => {
    onChange(e.target.value)
    setOpen(true)
    setActiveIdx(-1)
  }

  const handleKeyDown = (e) => {
    if (!showDropdown) return
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActiveIdx((i) => Math.min(i + 1, filtered.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActiveIdx((i) => Math.max(i - 1, -1))
    } else if (e.key === 'Enter' && activeIdx >= 0) {
      e.preventDefault()
      select(filtered[activeIdx])
    } else if (e.key === 'Escape') {
      setOpen(false)
      setActiveIdx(-1)
    }
  }

  return (
    <div ref={containerRef} className="relative">
      <input
        ref={inputRef}
        type="text"
        value={value}
        onChange={handleChange}
        onFocus={() => setOpen(true)}
        onKeyDown={handleKeyDown}
        onClick={onClick}
        placeholder={placeholder}
        className={FIELD}
        autoFocus={autoFocus}
        autoComplete="off"
      />

      {showDropdown && (
        <ul className="absolute z-50 mt-1 w-full rounded-xl border border-[#2e3350] bg-[#1c2030] shadow-2xl overflow-hidden">
          {filtered.map((s, i) => (
            <li
              key={s}
              onMouseDown={(e) => { e.preventDefault(); select(s) }}
              className={`px-3 py-2 text-sm cursor-pointer transition-colors ${
                i === activeIdx
                  ? 'bg-blue-600/20 text-white'
                  : 'text-[#94a3b8] hover:bg-[#252d3d] hover:text-white'
              }`}
            >
              {s}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/**
 * Helper: toma un array de strings y devuelve un array deduplicado
 * ordenado de mayor a menor frecuencia de aparición.
 */
export function toFreqSorted(arr) {
  const freq = {}
  arr.forEach((s) => { if (s) freq[s] = (freq[s] || 0) + 1 })
  return Object.entries(freq)
    .sort((a, b) => b[1] - a[1])
    .map(([s]) => s)
}
