import { useState, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ChevronDown, ChevronUp, ChevronsDownUp, ChevronsUpDown,
  CreditCard, Zap, ExternalLink
} from 'lucide-react'
import { Fragment } from 'react'
import useStore from '../../store/useStore'
import { formatAmount, formatMonth } from '../../lib/formatters'
import { generateMonths, addMonthsToYM } from '../../lib/dateUtils'
import { SERVICE_CATEGORIES } from '../services/ServiceForm'
import OwnerBadge, { SHARED_OWNER } from '../../components/ui/OwnerBadge'

// ─── Helpers ──────────────────────────────────────────────────────────────────
function getColumns(currentMonth) {
  const start = addMonthsToYM(currentMonth, -6)
  return generateMonths(start, 19) // 6 past + current + 12 future
}

function getCatIcon(category) {
  return SERVICE_CATEGORIES.find((c) => c.value === category)?.icon || '📌'
}

// ─── Amount cell ──────────────────────────────────────────────────────────────
function AmountCell({ amount, isCurrent, isPast, teal, bold }) {
  if (!amount) {
    return (
      <td className={`px-2 sm:px-3 py-2.5 text-center ${isCurrent ? 'bg-blue-500/5' : ''}`}>
        <span className="text-[#1c2030]">—</span>
      </td>
    )
  }
  const color = teal
    ? isCurrent ? 'text-teal-300' : 'text-teal-600'
    : bold
      ? isCurrent ? 'text-blue-400' : isPast ? 'text-[#64748b]' : 'text-[#94a3b8]'
      : isCurrent ? 'text-blue-300' : isPast ? 'text-[#4a5568]' : 'text-[#64748b]'

  return (
    <td className={`px-2 sm:px-3 py-2.5 text-center ${isCurrent ? 'bg-blue-500/5' : ''}`}>
      <span className={`text-xs ${bold ? 'font-bold' : 'font-semibold'} mono ${color}`}>
        {formatAmount(amount)}
      </span>
    </td>
  )
}

// ─── Section header row ────────────────────────────────────────────────────────
function SectionHeaderRow({ icon: Icon, label, color, colSpan, action }) {
  return (
    <tr>
      <td
        colSpan={colSpan}
        className="sticky left-0 px-4 py-2 bg-[#0d0f14] border-t-2 border-b border-[#2e3350]"
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Icon size={12} className={color} />
            <span className={`text-[10px] font-bold uppercase tracking-widest ${color}`}>{label}</span>
          </div>
          {action}
        </div>
      </td>
    </tr>
  )
}

// ─── Main component ────────────────────────────────────────────────────────────
export default function CardsDashboard() {
  const navigate = useNavigate()
  const banks        = useStore((s) => s.banks)
  const creditCards  = useStore((s) => s.creditCards)
  const installments = useStore((s) => s.installments)
  const services     = useStore((s) => s.services)
  const people       = useStore((s) => s.people)
  const currentMonth = useStore((s) => s.currentMonth)
  const getCardTotal              = useStore((s) => s.getCardTotal)
  const getIndependentServicesTotal = useStore((s) => s.getIndependentServicesTotal)

  const columns = useMemo(() => getColumns(currentMonth), [currentMonth])

  const [expandedCards, setExpandedCards] = useState(new Set())
  const [allOpen, setAllOpen] = useState(false)

  const toggleCard = (cardId) => {
    setExpandedCards((prev) => {
      const next = new Set(prev)
      if (next.has(cardId)) next.delete(cardId)
      else next.add(cardId)
      return next
    })
  }

  const handleToggleAll = () => {
    if (allOpen) {
      setAllOpen(false)
      setExpandedCards(new Set())
    } else {
      setAllOpen(true)
      setExpandedCards(new Set(creditCards.map((c) => c.id)))
    }
  }

  // Independent services that have at least one amount in the visible columns
  const indSvcs = useMemo(() =>
    services.filter(
      (s) => !s.linkedCardId && s.active !== false &&
        columns.some((col) => s.amounts.find((a) => a.month === col && a.amount > 0))
    ),
    [services, columns]
  )

  // Grand total per column
  const grandTotals = useMemo(() =>
    columns.map((col) =>
      creditCards.reduce((s, c) => s + getCardTotal(c.id, col), 0) +
      getIndependentServicesTotal(col)
    ),
    [columns, creditCards, getCardTotal, getIndependentServicesTotal]
  )

  const currentColIdx = columns.indexOf(currentMonth)

  return (
    <div className="flex flex-col h-full">
      {/* Toolbar */}
      <div className="px-6 py-3 border-b border-[#1c2030] flex items-center justify-between shrink-0 bg-[#0d0f14]">
        <p className="text-xs text-[#64748b]">
          Cuotas y servicios · {columns.length} meses
        </p>
        <div className="flex items-center gap-2">
          <button
            onClick={() => navigate('/cards')}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border border-[#2e3350] text-[#64748b] hover:text-white hover:border-[#3d4466] transition-colors"
          >
            <CreditCard size={12} />
            Tarjetas
          </button>
          <button
            onClick={() => navigate('/services')}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border border-[#2e3350] text-[#64748b] hover:text-white hover:border-[#3d4466] transition-colors"
          >
            <Zap size={12} />
            Servicios
          </button>
          <button
            onClick={handleToggleAll}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border border-[#2e3350] text-[#64748b] hover:text-white hover:border-[#3d4466] transition-colors"
          >
            {allOpen ? <ChevronsDownUp size={12} /> : <ChevronsUpDown size={12} />}
            {allOpen ? 'Colapsar' : 'Expandir'}
          </button>
        </div>
      </div>

      {/* MonthGrid */}
      <div
        className="flex-1 overflow-auto"
      >
        <table
          className="w-full border-collapse"
        >
          {/* ── Header ─────────────────────────────────────────────────────── */}
          <thead>
            <tr className="sticky top-0 z-20 bg-[#0d0f14]">
              <th className="sticky left-0 z-30 bg-[#0d0f14] border-b border-r border-[#1c2030] px-4 py-3 text-left w-40 sm:w-64 shrink-0">
                <span className="text-xs font-semibold text-[#64748b] uppercase tracking-wider">Descripción</span>
              </th>
              {columns.map((col) => {
                const isCurrent = col === currentMonth
                const isPast    = col < currentMonth
                return (
                  <th
                    key={col}
                    className={`px-2 sm:px-3 py-3 text-center min-w-[80px] sm:min-w-[110px] border-b border-[#1c2030] ${
                      isCurrent ? 'border-b-2 border-b-blue-500' : ''
                    }`}
                  >
                    <span className={`text-xs font-semibold uppercase tracking-wider ${
                      isCurrent ? 'text-blue-400' : isPast ? 'text-[#4a5568]' : 'text-[#64748b]'
                    }`}>
                      {formatMonth(col)}
                    </span>
                  </th>
                )
              })}
            </tr>
          </thead>

          <tbody>
            {/* ══ TARJETAS section ══════════════════════════════════════════ */}
            <SectionHeaderRow
              icon={CreditCard}
              label="Tarjetas"
              color="text-blue-400"
              colSpan={columns.length + 1}
              action={
                <button
                  onClick={() => navigate('/cards')}
                  className="text-[10px] text-[#4a5568] hover:text-blue-400 flex items-center gap-1 transition-colors"
                >
                  Ver <ExternalLink size={10} />
                </button>
              }
            />

            {creditCards.map((card) => {
              const bank = banks.find((b) => b.id === card.bankId)
              const isExpanded = allOpen || expandedCards.has(card.id)

              // Active installments with amounts in visible columns
              const cardInst = installments.filter(
                (i) => i.cardId === card.id && i.active !== false &&
                  columns.some((col) => i.months.find((m) => m.month === col && m.amount > 0))
              )
              // Services linked to this card
              const cardSvc = services.filter(
                (s) => s.linkedCardId === card.id && s.active !== false &&
                  columns.some((col) => s.amounts.find((a) => a.month === col && a.amount > 0))
              )

              const hasItems = cardInst.length > 0 || cardSvc.length > 0
              const subCount = cardInst.length + cardSvc.length

              return (
                <Fragment key={card.id}>
                  {/* ── Card total row ─────────────────────────────────────── */}
                  <tr
                    className={`group border-b border-[#1c2030] transition-colors bg-[#151820] ${
                      hasItems ? 'cursor-pointer hover:bg-[#1a1f2e]' : ''
                    }`}
                    onClick={() => hasItems && toggleCard(card.id)}
                  >
                    <td className="sticky left-0 z-10 bg-[#151820] group-hover:bg-[#1a1f2e] border-r border-[#1c2030] px-4 py-3 w-40 sm:w-64">
                      <div className="flex items-center justify-between gap-2">
                        <div className="flex items-center gap-2 min-w-0">
                          {hasItems
                            ? isExpanded
                              ? <ChevronUp size={13} className="text-[#4a5568] shrink-0" />
                              : <ChevronDown size={13} className="text-[#4a5568] shrink-0" />
                            : <span className="w-[13px] shrink-0" />
                          }
                          <span
                            className="w-2 h-2 rounded-full shrink-0"
                            style={{ backgroundColor: card.color }}
                          />
                          <div className="min-w-0">
                            <p className="text-sm font-semibold text-white truncate">{card.label}</p>
                            {bank && (
                              <p className="text-[10px] text-[#64748b]">
                                {bank.name}
                                {subCount > 0 && ` · ${subCount} concepto${subCount !== 1 ? 's' : ''}`}
                              </p>
                            )}
                          </div>
                        </div>
                        <button
                          onClick={(e) => { e.stopPropagation(); navigate(`/cards/${card.id}/installments`) }}
                          className="opacity-0 group-hover:opacity-100 p-1 rounded text-[#4a5568] hover:text-blue-400 transition-all shrink-0"
                          title="Ver cuotas"
                        >
                          <ExternalLink size={11} />
                        </button>
                      </div>
                    </td>
                    {columns.map((col) => {
                      const isCurrent = col === currentMonth
                      const isPast    = col < currentMonth
                      const total     = getCardTotal(card.id, col)
                      return (
                        <AmountCell
                          key={col}
                          amount={total}
                          isCurrent={isCurrent}
                          isPast={isPast}
                          bold
                        />
                      )
                    })}
                  </tr>

                  {/* ── Installment sub-rows ────────────────────────────────── */}
                  {isExpanded && cardInst.map((inst) => {
                    const paidCount = inst.months.filter((m) => m.paid).length
                    const totalInst = inst.frequency === 'fixed' ? inst.totalInstallments : null
                    return (
                      <tr
                        key={inst.id}
                        className="border-b border-[#1c2030]/60 bg-[#151820] hover:bg-[#1a1f2e] transition-colors"
                      >
                        <td className="sticky left-0 z-10 bg-[#151820] hover:bg-[#1a1f2e] border-r border-[#1c2030] px-4 py-2 w-40 sm:w-64 pl-10">
                          <div className="flex items-center gap-1.5">
                            <p className="text-xs font-medium text-[#94a3b8] truncate">{inst.description}</p>
                            {inst.ownerId && <OwnerBadge person={inst.ownerId === 'shared' ? SHARED_OWNER : people.find((p) => p.id === inst.ownerId)} size="xs" />}
                          </div>
                          <div className="flex items-center gap-1.5">
                            <span className="text-[9px] text-[#64748b]">{inst.category}</span>
                            {totalInst
                              ? <span className="text-[9px] text-[#64748b]">· {paidCount}/{totalInst} c</span>
                              : <span className="text-[9px] text-[#64748b]">· ∞</span>
                            }
                          </div>
                        </td>
                        {columns.map((col) => {
                          const isCurrent = col === currentMonth
                          const isPast    = col < currentMonth
                          const m = inst.months.find((m) => m.month === col)
                          const amount = m?.amount || 0
                          return (
                            <AmountCell
                              key={col}
                              amount={amount}
                              isCurrent={isCurrent}
                              isPast={isPast}
                            />
                          )
                        })}
                      </tr>
                    )
                  })}

                  {/* ── Service sub-rows ────────────────────────────────────── */}
                  {isExpanded && cardSvc.map((svc) => (
                    <tr
                      key={svc.id}
                      className="border-b border-[#1c2030]/60 bg-[#151820] hover:bg-[#1a1f2e] transition-colors"
                    >
                      <td className="sticky left-0 z-10 bg-[#151820] hover:bg-[#1a1f2e] border-r border-[#1c2030] px-4 py-2 w-40 sm:w-64 pl-10">
                        <div className="flex items-center gap-1.5">
                          <span className="text-xs">{getCatIcon(svc.category)}</span>
                          <div>
                            <div className="flex items-center gap-1">
                              <p className="text-xs font-medium text-[#94a3b8] truncate">{svc.name}</p>
                              <span className="text-[9px] px-1 rounded bg-teal-500/15 text-teal-500 font-bold">Svc</span>
                              {svc.ownerId && <OwnerBadge person={svc.ownerId === 'shared' ? SHARED_OWNER : people.find((p) => p.id === svc.ownerId)} size="xs" />}
                            </div>
                            <p className="text-[9px] text-[#64748b]">{svc.category} · ∞</p>
                          </div>
                        </div>
                      </td>
                      {columns.map((col) => {
                        const isCurrent = col === currentMonth
                        const isPast    = col < currentMonth
                        const amount = svc.amounts.find((a) => a.month === col)?.amount || 0
                        return (
                          <AmountCell
                            key={col}
                            amount={amount}
                            isCurrent={isCurrent}
                            isPast={isPast}
                            teal
                          />
                        )
                      })}
                    </tr>
                  ))}
                </Fragment>
              )
            })}

            {/* ══ SERVICIOS INDEPENDIENTES section ══════════════════════════ */}
            {indSvcs.length > 0 && (
              <>
                <SectionHeaderRow
                  icon={Zap}
                  label="Servicios independientes"
                  color="text-purple-400"
                  colSpan={columns.length + 1}
                  action={
                    <button
                      onClick={() => navigate('/services')}
                      className="text-[10px] text-[#4a5568] hover:text-purple-400 flex items-center gap-1 transition-colors"
                    >
                      Ver <ExternalLink size={10} />
                    </button>
                  }
                />

                {indSvcs.map((svc) => (
                  <tr
                    key={svc.id}
                    className="group border-b border-[#1c2030] bg-[#151820] hover:bg-[#1a1f2e] transition-colors"
                  >
                    <td className="sticky left-0 z-10 bg-[#151820] group-hover:bg-[#1a1f2e] border-r border-[#1c2030] px-4 py-2.5 w-40 sm:w-64">
                      <div className="flex items-center gap-2">
                        <span className="text-sm">{getCatIcon(svc.category)}</span>
                        <div>
                          <div className="flex items-center gap-1.5">
                            <p className="text-sm font-medium text-white truncate">{svc.name}</p>
                            {svc.ownerId && <OwnerBadge person={svc.ownerId === 'shared' ? SHARED_OWNER : people.find((p) => p.id === svc.ownerId)} size="xs" />}
                          </div>
                          <p className="text-[10px] text-[#64748b]">{svc.category} · ∞ mensual</p>
                        </div>
                      </div>
                    </td>
                    {columns.map((col) => {
                      const isCurrent = col === currentMonth
                      const isPast    = col < currentMonth
                      const amount = svc.amounts.find((a) => a.month === col)?.amount || 0
                      return (
                        <AmountCell
                          key={col}
                          amount={amount}
                          isCurrent={isCurrent}
                          isPast={isPast}
                          teal
                        />
                      )
                    })}
                  </tr>
                ))}
              </>
            )}

            {/* ══ Grand total row (sticky bottom) ══════════════════════════ */}
            <tr className="sticky bottom-0 z-10 bg-[#0d0f14] border-t-2 border-[#2e3350]">
              <td className="sticky left-0 z-20 bg-[#0d0f14] border-r border-[#2e3350] px-4 py-3">
                <span className="text-xs font-bold text-[#94a3b8] uppercase tracking-wider">Gran total</span>
              </td>
              {grandTotals.map((total, i) => {
                const col       = columns[i]
                const isCurrent = col === currentMonth
                return (
                  <td key={col} className={`px-3 py-3 text-center ${isCurrent ? 'bg-blue-500/5' : ''}`}>
                    <span className={`text-xs font-bold mono ${
                      total > 0
                        ? isCurrent ? 'text-blue-400' : 'text-[#94a3b8]'
                        : 'text-[#2e3350]'
                    }`}>
                      {total > 0 ? formatAmount(total) : '—'}
                    </span>
                  </td>
                )
              })}
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  )
}
