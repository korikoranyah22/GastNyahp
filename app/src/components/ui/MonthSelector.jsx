import { ChevronLeft, ChevronRight } from 'lucide-react'
import { addMonthsToYM } from '../../lib/dateUtils'
import { formatMonthLong } from '../../lib/formatters'
import useStore from '../../store/useStore'

export default function MonthSelector() {
  const currentMonth = useStore((s) => s.currentMonth)
  const setCurrentMonth = useStore((s) => s.setCurrentMonth)

  return (
    <div className="flex items-center gap-1">
      <button
        onClick={() => setCurrentMonth(addMonthsToYM(currentMonth, -1))}
        className="p-1.5 rounded-lg text-[#94a3b8] hover:text-white hover:bg-[#2e3350] transition-colors"
      >
        <ChevronLeft size={15} />
      </button>
      <span className="text-sm font-semibold text-white min-w-[130px] text-center">
        {formatMonthLong(currentMonth)}
      </span>
      <button
        onClick={() => setCurrentMonth(addMonthsToYM(currentMonth, 1))}
        className="p-1.5 rounded-lg text-[#94a3b8] hover:text-white hover:bg-[#2e3350] transition-colors"
      >
        <ChevronRight size={15} />
      </button>
    </div>
  )
}
