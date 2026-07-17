import { useState } from 'react'
import { Copy, X, ChevronRight } from 'lucide-react'
import { formatMonthLong } from '../../lib/formatters'
import useStore from '../../store/useStore'

/**
 * Banner que aparece cuando el mes actual no tiene datos estimados,
 * ofreciendo copiar del mes anterior más reciente que sí tenga.
 * Se muestra solo una vez por mes (se puede ignorar).
 */
export default function CopyMonthBanner() {
  const currentMonth    = useStore((s) => s.currentMonth)
  const fixedExpenses   = useStore((s) => s.fixedExpenses)
  const budgets         = useStore((s) => s.budgets)
  const copyMonthData   = useStore((s) => s.copyMonthData)

  const [dismissed, setDismissed] = useState(false)
  const [copied, setCopied]       = useState(false)

  // ¿Ya tiene datos el mes actual?
  const hasData = (
    !!budgets[currentMonth] ||
    fixedExpenses.some(
      (f) => !f.recurring && f.months.some((m) => m.month === currentMonth && m.amount > 0)
    )
  )

  // Encontrar el mes anterior más reciente con datos
  const candidateMonths = [
    ...Object.keys(budgets),
    ...fixedExpenses.flatMap((f) => f.months.map((m) => m.month)),
  ]
    .filter((m) => m < currentMonth)
    .sort()
    .reverse()

  const fromMonth = candidateMonths[0] || null

  if (hasData || dismissed || copied || !fromMonth) return null

  const handleCopy = () => {
    copyMonthData(fromMonth, currentMonth)
    setCopied(true)
  }

  return (
    <div className="mx-6 mt-4 flex items-center justify-between gap-4 px-4 py-3 bg-blue-500/10 border border-blue-500/30 rounded-xl">
      <div className="flex items-center gap-3 min-w-0">
        <Copy size={15} className="text-blue-400 shrink-0" />
        <p className="text-sm text-[#94a3b8] truncate">
          <span className="text-blue-400 font-medium">{formatMonthLong(currentMonth)}</span>
          {' '}no tiene estimaciones.{' '}
          ¿Copiar de{' '}
          <span className="text-white font-medium">{formatMonthLong(fromMonth)}</span>?
        </p>
      </div>
      <div className="flex items-center gap-2 shrink-0">
        <button
          onClick={handleCopy}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium transition-colors"
        >
          Copiar
          <ChevronRight size={12} />
        </button>
        <button
          onClick={() => setDismissed(true)}
          className="p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#1c2030] transition-colors"
          title="Ignorar"
        >
          <X size={14} />
        </button>
      </div>
    </div>
  )
}
