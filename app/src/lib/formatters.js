import { format, parseISO } from 'date-fns'
import { es } from 'date-fns/locale'

export function formatAmount(amount) {
  if (amount === null || amount === undefined || amount === '') return '—'
  return new Intl.NumberFormat('es-AR', {
    style: 'currency',
    currency: 'ARS',
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(amount)
}

export function formatAmountShort(amount) {
  if (!amount) return '—'
  if (amount >= 1_000_000) return `$${(amount / 1_000_000).toFixed(1)}M`
  if (amount >= 1_000) return `$${(amount / 1_000).toFixed(0)}k`
  return `$${amount}`
}

// YYYY-MM → "Feb 2026"
export function formatMonth(ym) {
  if (!ym) return ''
  const [y, m] = ym.split('-')
  const d = new Date(Number(y), Number(m) - 1, 1)
  return format(d, 'MMM yyyy', { locale: es })
    .replace(/^\w/, c => c.toUpperCase())
}

// YYYY-MM → "Febrero 2026"
export function formatMonthLong(ym) {
  if (!ym) return ''
  const [y, m] = ym.split('-')
  const d = new Date(Number(y), Number(m) - 1, 1)
  return format(d, 'MMMM yyyy', { locale: es })
    .replace(/^\w/, c => c.toUpperCase())
}

// Date string YYYY-MM-DD → "14 dic 2024"
export function formatDate(dateStr) {
  if (!dateStr) return ''
  try {
    return format(parseISO(dateStr), 'd MMM yyyy', { locale: es })
  } catch { return dateStr }
}

export function currentYearMonth() {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
}
