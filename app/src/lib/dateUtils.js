import { addMonths, format, parseISO, isBefore, isAfter, startOfMonth } from 'date-fns'

// Advance YYYY-MM by N months
export function addMonthsToYM(ym, n) {
  const [y, m] = ym.split('-').map(Number)
  const d = addMonths(new Date(y, m - 1, 1), n)
  return format(d, 'yyyy-MM')
}

// Compare two YYYY-MM strings
export function compareYM(a, b) {
  if (a < b) return -1
  if (a > b) return 1
  return 0
}

export function ymToDate(ym) {
  const [y, m] = ym.split('-').map(Number)
  return new Date(y, m - 1, 1)
}

export function dateToYM(date) {
  return format(date, 'yyyy-MM')
}

// Generate array of YYYY-MM strings from start for N months
export function generateMonths(startYM, count) {
  return Array.from({ length: count }, (_, i) => addMonthsToYM(startYM, i))
}

// Given a purchase date (YYYY-MM-DD) determine start month
// (usually next month after purchase if after closing day, or same month if before)
export function getStartMonth(purchaseDateStr, closingDay = 15) {
  try {
    const d = parseISO(purchaseDateStr)
    const sameMonth = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
    if (d.getDate() <= closingDay) return sameMonth
    return addMonthsToYM(sameMonth, 1)
  } catch {
    return format(new Date(), 'yyyy-MM')
  }
}

export function currentYM() {
  return format(new Date(), 'yyyy-MM')
}
