import { useState, useMemo } from 'react'
import { ChevronDown, ChevronUp, Search, X, TrendingUp, TrendingDown, Minus } from 'lucide-react'
import useStore from '../../store/useStore'
import { formatAmount } from '../../lib/formatters'
import { formatDay, getExpenseAmount } from '../expenses/expensesConfig'

// ── Helpers ────────────────────────────────────────────────────────────────────

function buildProductGroups(expenses) {
  const map = new Map()
  expenses
    .filter((e) => e.type === 'ticket')
    .forEach((ticket) => {
      ;(ticket.items || []).forEach((item) => {
        if (!item.description?.trim() || !(item.amount > 0)) return
        const key = `${item.description.trim()}|||${ticket.description?.trim() || '—'}`
        if (!map.has(key)) {
          map.set(key, {
            key,
            itemDescription:  item.description.trim(),
            storeDescription: ticket.description?.trim() || '—',
            occurrences: [],
          })
        }
        map.get(key).occurrences.push({ date: ticket.date, amount: item.amount })
      })
    })

  return [...map.values()]
    .map((g) => enrich(g))
    .filter((g) => g.count >= 2)
    .sort((a, b) => b.occurrences[0].date.localeCompare(a.occurrences[0].date))
}

function buildExpenseGroups(expenses) {
  const map = new Map()
  expenses.forEach((e) => {
    if (!e.description?.trim()) return
    const amount = getExpenseAmount(e)
    if (!(amount > 0)) return
    const key = e.description.trim()
    if (!map.has(key)) {
      map.set(key, {
        key,
        description: e.description.trim(),
        occurrences: [],
      })
    }
    map.get(key).occurrences.push({ date: e.date, amount })
  })

  return [...map.values()]
    .map((g) => enrich(g))
    .filter((g) => g.count >= 2)
    .sort((a, b) => b.occurrences[0].date.localeCompare(a.occurrences[0].date))
}

/** Ordena ocurrencias y calcula métricas */
function enrich(g) {
  const sorted = [...g.occurrences].sort((a, b) => b.date.localeCompare(a.date))
  const last5  = sorted.slice(0, 5)
  const avg    = last5.reduce((s, o) => s + o.amount, 0) / last5.length
  return {
    ...g,
    occurrences: sorted,
    lastPrice:   sorted[0].amount,
    avgLast5:    Math.round(avg),
    avgN:        last5.length,
    count:       sorted.length,
  }
}

function pctDelta(current, prev) {
  if (!prev || prev === 0) return null
  return ((current - prev) / prev) * 100
}

// ── Subcomponentes ─────────────────────────────────────────────────────────────

function OccurrenceRow({ occ, prev }) {
  const delta = pctDelta(occ.amount, prev?.amount)
  return (
    <div className="flex items-center gap-3 px-4 py-2.5 bg-[#0d0f14]">
      <span className="text-xs text-[#64748b] w-28 shrink-0">{formatDay(occ.date)}</span>
      <span className="flex-1 text-sm mono text-white">{formatAmount(occ.amount)}</span>
      {delta !== null ? (
        <span className={`flex items-center gap-0.5 text-xs mono shrink-0 ${
          delta > 0 ? 'text-red-400' : delta < 0 ? 'text-green-400' : 'text-[#64748b]'
        }`}>
          {delta > 0
            ? <TrendingUp  size={11} />
            : delta < 0
            ? <TrendingDown size={11} />
            : <Minus size={11} />}
          {delta > 0 ? '+' : ''}{delta.toFixed(1)}%
        </span>
      ) : (
        <span className="text-xs text-[#3d4466] shrink-0">—</span>
      )}
    </div>
  )
}

function GroupCard({ group, isProduct, expanded, onToggle }) {
  const isOpen = expanded.has(group.key)
  const avgN   = group.avgN

  return (
    <div className="border border-[#2e3350] rounded-xl overflow-hidden">
      {/* ── Header ── */}
      <div
        className="flex items-start gap-3 px-4 py-3 bg-[#151820] cursor-pointer select-none hover:bg-[#1c2030] transition-colors"
        onClick={() => onToggle(group.key)}
      >
        {/* Ícono toggle */}
        <span className="text-[#3d4466] mt-0.5 shrink-0">
          {isOpen ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
        </span>

        {/* Nombre */}
        <div className="flex-1 min-w-0">
          <p className="text-sm text-white font-medium truncate">
            {isProduct ? group.itemDescription : group.description}
          </p>
          {isProduct && (
            <p className="text-xs text-[#64748b] mt-0.5 truncate">
              🏪 {group.storeDescription}
            </p>
          )}
          <p className="text-[10px] text-[#3d4466] mt-0.5">
            {group.count} {group.count === 1 ? 'vez' : 'veces'}
          </p>
        </div>

        {/* Métricas */}
        <div className="flex items-center gap-4 shrink-0 text-right">
          <div>
            <p className="text-[10px] text-[#64748b]">Último</p>
            <p className="text-sm mono text-white">{formatAmount(group.lastPrice)}</p>
          </div>
          <div>
            <p className="text-[10px] text-[#64748b]">
              Prom. {avgN < 5 ? `últ. ${avgN}` : 'últ. 5'}
            </p>
            <p className="text-sm font-bold mono text-blue-400">
              {formatAmount(group.avgLast5)}
            </p>
          </div>
        </div>
      </div>

      {/* ── Historial ── */}
      {isOpen && (
        <div className="divide-y divide-[#1c2030]">
          {group.occurrences.map((occ, i) => (
            <OccurrenceRow
              key={`${occ.date}-${i}`}
              occ={occ}
              prev={group.occurrences[i + 1]}
            />
          ))}
        </div>
      )}
    </div>
  )
}

// ── Página principal ───────────────────────────────────────────────────────────

export default function PriceHistoryPage() {
  const expenses = useStore((s) => s.expenses)

  const [tab,      setTab]      = useState('products') // 'products' | 'expenses'
  const [search,   setSearch]   = useState('')
  const [expanded, setExpanded] = useState(new Set())

  const productGroups = useMemo(() => buildProductGroups(expenses), [expenses])
  const expenseGroups = useMemo(() => buildExpenseGroups(expenses), [expenses])

  const q = search.toLowerCase().trim()

  const filteredProducts = q
    ? productGroups.filter(
        (g) =>
          g.itemDescription.toLowerCase().includes(q) ||
          g.storeDescription.toLowerCase().includes(q)
      )
    : productGroups

  const filteredExpenses = q
    ? expenseGroups.filter((g) => g.description.toLowerCase().includes(q))
    : expenseGroups

  const toggleExpand = (key) =>
    setExpanded((prev) => {
      const next = new Set(prev)
      next.has(key) ? next.delete(key) : next.add(key)
      return next
    })

  const groups    = tab === 'products' ? filteredProducts : filteredExpenses
  const isEmpty   = groups.length === 0
  const isProduct = tab === 'products'

  return (
    <div className="max-w-2xl mx-auto px-4 py-6 space-y-5">

      {/* Header */}
      <div>
        <h2 className="text-lg font-bold text-white">Historial de precios</h2>
        <p className="text-xs text-[#64748b] mt-0.5">
          Seguí la evolución de precios de tus gastos repetidos
        </p>
      </div>

      {/* Tabs */}
      <div className="flex rounded-xl border border-[#2e3350] overflow-hidden">
        {[
          { key: 'products', label: `Productos (${productGroups.length})` },
          { key: 'expenses', label: `Gastos (${expenseGroups.length})` },
        ].map(({ key, label }) => (
          <button
            key={key}
            onClick={() => { setTab(key); setSearch('') }}
            className={`flex-1 py-2.5 text-xs font-semibold transition-colors ${
              tab === key
                ? 'bg-blue-600 text-white'
                : 'text-[#64748b] hover:text-white hover:bg-[#1c2030]'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {/* Buscador */}
      <div className="relative">
        <Search size={13} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#64748b]" />
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={isProduct ? 'Buscar producto o local…' : 'Buscar descripción…'}
          className="w-full pl-8 pr-8 py-2 bg-[#151820] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 placeholder:text-[#3d4466]"
        />
        {search && (
          <button
            onClick={() => setSearch('')}
            className="absolute right-2.5 top-1/2 -translate-y-1/2 text-[#64748b] hover:text-white"
          >
            <X size={13} />
          </button>
        )}
      </div>

      {/* Lista */}
      {isEmpty ? (
        <div className="text-center py-16 text-[#3d4466]">
          <p className="text-3xl mb-3">{isProduct ? '🧾' : '🛒'}</p>
          {search ? (
            <p className="text-sm">Sin resultados para "{search}"</p>
          ) : (
            <>
              <p className="text-sm font-medium text-[#64748b]">Todavía no hay historial</p>
              <p className="text-xs mt-1">
                {isProduct
                  ? 'Cargá tickets con ítems repetidos para ver la evolución de precios.'
                  : 'Cargá el mismo gasto varias veces para ver su historial.'}
              </p>
            </>
          )}
        </div>
      ) : (
        <div className="space-y-2">
          {groups.map((g) => (
            <GroupCard
              key={g.key}
              group={g}
              isProduct={isProduct}
              expanded={expanded}
              onToggle={toggleExpand}
            />
          ))}
        </div>
      )}
    </div>
  )
}
