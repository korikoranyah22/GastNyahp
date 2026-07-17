import { useState, useEffect } from 'react'

const toRaw = (v) => {
  if (v === '' || v === null || v === undefined) return ''
  return String(v).replace('.', ',')
}

export default function AmountInput({ value, onChange, placeholder = '0', className = '' }) {
  const [raw, setRaw] = useState(toRaw(value))

  // Sync when the parent updates `value` (e.g. when populating an edit form),
  // but skip if the change originated from our own handleChange (avoids losing trailing zeros like 1500,50 → 1500,5)
  useEffect(() => {
    if (value === '' || value === null || value === undefined) {
      setRaw('')
      return
    }
    const rawAsNum = raw !== '' ? Number(raw.replace(',', '.')) : NaN
    if (rawAsNum !== value) {
      setRaw(toRaw(value))
    }
  }, [value])

  const handleChange = (e) => {
    let v = e.target.value
    // Strip thousands-separator dots (digit·dot·digit, e.g. "1.500" → "1500")
    // Loop handles multiple separators like "1.500.000"
    while (/\d\.\d/.test(v)) {
      v = v.replace(/(\d)\.(\d)/g, '$1$2')
    }
    // Convert remaining dots to commas (numpad "." → decimal separator)
    v = v.replace(/\./g, ',')
    // Keep only digits and comma
    v = v.replace(/[^\d,]/g, '')

    // Only one comma allowed; limit decimal part to 2 digits
    const commaIdx = v.indexOf(',')
    if (commaIdx !== -1) {
      const dec = v.slice(commaIdx + 1).replace(/,/g, '').slice(0, 2)
      v = v.slice(0, commaIdx + 1) + dec
    }

    setRaw(v)
    const num = v ? Number(v.replace(',', '.')) : ''
    onChange(isNaN(num) ? '' : num)
  }

  const display = (() => {
    if (!raw) return ''
    if (raw.includes(',')) {
      const [intPart, decPart] = raw.split(',')
      const formattedInt = intPart ? Number(intPart).toLocaleString('es-AR') : '0'
      return formattedInt + ',' + decPart
    }
    return Number(raw).toLocaleString('es-AR')
  })()

  return (
    <div className={`relative ${className}`}>
      <span className="absolute left-3 top-1/2 -translate-y-1/2 text-[#64748b] text-sm">$</span>
      <input
        type="text"
        inputMode="decimal"
        value={display}
        onChange={handleChange}
        placeholder={placeholder}
        className="w-full pl-7 pr-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm font-mono focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30 placeholder:text-[#3d4466]"
      />
    </div>
  )
}
