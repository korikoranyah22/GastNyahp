import { APP_CATEGORIES } from '../../pages/expenses/expensesConfig'

/**
 * Selector de categoría universal — grilla de 4 columnas.
 *
 * Props:
 *   value    — categoría actualmente seleccionada (string)
 *   onChange — (string) => void
 */
export default function CategoryPicker({ value, onChange }) {
  return (
    <div className="grid grid-cols-4 gap-1.5">
      {APP_CATEGORIES.map((cat) => (
        <button
          key={cat.value}
          type="button"
          onClick={() => onChange(cat.value)}
          className={`flex flex-col items-center gap-0.5 px-1 py-2 rounded-lg border text-[10px] font-medium transition-all leading-tight text-center ${
            value === cat.value
              ? 'border-blue-500 bg-blue-500/15 text-blue-300'
              : 'border-[#2e3350] text-[#64748b] hover:border-[#3d4466] hover:text-white'
          }`}
        >
          <span className="text-base leading-none">{cat.icon}</span>
          <span>{cat.value}</span>
        </button>
      ))}
    </div>
  )
}
