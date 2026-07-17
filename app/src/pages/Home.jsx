import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  CreditCard, Landmark, ArrowRight,
  ShoppingCart, PiggyBank, TrendingUp, TrendingDown,
  ChevronDown, ChevronUp, Pencil, Check, X, Wallet, DollarSign,
} from 'lucide-react'
import useStore from '../store/useStore'
import { formatAmount, formatMonthLong } from '../lib/formatters'
import CopyMonthBanner from '../components/ui/CopyMonthBanner'

// ── Inline editable number field ──────────────────────────────────────────────
function InlineNumber({ value, onSave, prefix = '$', suffix = '', className = '' }) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft]     = useState('')

  const start  = () => { setDraft(String(value)); setEditing(true) }
  const commit = () => { onSave(Number(draft) || 0); setEditing(false) }
  const cancel = () => setEditing(false)

  if (editing) {
    return (
      <span className="inline-flex items-center gap-1">
        {prefix && <span className="text-sm text-[#64748b]">{prefix}</span>}
        <input
          autoFocus
          type="number"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') commit(); if (e.key === 'Escape') cancel() }}
          className="w-32 px-2 py-0.5 bg-[#1c2030] border border-blue-500/60 rounded text-sm text-white font-mono focus:outline-none"
        />
        {suffix && <span className="text-sm text-[#64748b]">{suffix}</span>}
        <button onClick={commit} className="p-0.5 text-green-400 hover:text-green-300"><Check size={13} /></button>
        <button onClick={cancel} className="p-0.5 text-[#64748b] hover:text-white"><X size={13} /></button>
      </span>
    )
  }

  return (
    <span
      className={`cursor-pointer group/ie inline-flex items-center gap-1 ${className}`}
      onClick={start}
      title="Click para editar"
    >
      {prefix && <span className="text-sm text-[#64748b]">{prefix}</span>}
      <span>{formatAmount(value)}</span>
      {suffix && <span className="text-sm text-[#64748b]">{suffix}</span>}
      <Pencil size={11} className="text-[#3d4466] opacity-0 group-hover/ie:opacity-100 transition-opacity" />
    </span>
  )
}

// ── Section block (collapsible) ───────────────────────────────────────────────
function SectionBlock({ title, total, color, icon: Icon, defaultOpen = false, children, onNavigate }) {
  const [open, setOpen] = useState(defaultOpen)

  return (
    <div className="bg-[#151820] border border-[#2e3350] rounded-xl overflow-hidden">
      <div
        className="flex items-center justify-between px-4 py-3.5 cursor-pointer hover:bg-[#1c2030] transition-colors"
        onClick={() => setOpen((v) => !v)}
      >
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 rounded-lg flex items-center justify-center shrink-0" style={{ backgroundColor: `${color}22` }}>
            <Icon size={14} style={{ color }} />
          </div>
          <span className="text-xs font-bold text-[#94a3b8] uppercase tracking-wider">{title}</span>
        </div>
        <div className="flex items-center gap-3">
          <span className="text-sm font-bold font-mono" style={{ color }}>{formatAmount(total)}</span>
          {onNavigate && (
            <button
              onClick={(e) => { e.stopPropagation(); onNavigate() }}
              className="p-1 rounded text-[#3d4466] hover:text-[#94a3b8] transition-colors"
              title="Ver detalle"
            >
              <ArrowRight size={13} />
            </button>
          )}
          <span className="text-[#3d4466]">{open ? <ChevronUp size={14} /> : <ChevronDown size={14} />}</span>
        </div>
      </div>
      {open && <div className="border-t border-[#1c2030]">{children}</div>}
    </div>
  )
}

// ── Detail row ────────────────────────────────────────────────────────────────
function DetailRow({ label, sub, amount, color, badge, onClick }) {
  return (
    <div
      className={`flex items-center justify-between px-4 py-2.5 ${onClick ? 'hover:bg-[#1c2030] cursor-pointer' : ''} transition-colors`}
      onClick={onClick}
    >
      <div className="flex items-center gap-2.5">
        {badge && <span className="w-2.5 h-2.5 rounded-full shrink-0" style={{ backgroundColor: badge }} />}
        <div>
          <p className="text-sm text-white">{label}</p>
          {sub && <p className="text-xs text-[#64748b]">{sub}</p>}
        </div>
      </div>
      <span className={`text-sm font-semibold font-mono ${color || (amount > 0 ? 'text-white' : 'text-[#3d4466]')}`}>
        {amount > 0 ? formatAmount(amount) : '—'}
      </span>
    </div>
  )
}

// ── Main Dashboard ────────────────────────────────────────────────────────────
export default function Home() {
  const navigate = useNavigate()

  const banks                       = useStore((s) => s.banks)
  const creditCards                 = useStore((s) => s.creditCards)
  const installments                = useStore((s) => s.installments)
  const loans                       = useStore((s) => s.loans)
  const services                    = useStore((s) => s.services)
  const fixedExpenses               = useStore((s) => s.fixedExpenses)
  const income                      = useStore((s) => s.income)
  const setIncome                   = useStore((s) => s.setIncome)
  const currentMonth                = useStore((s) => s.currentMonth)

  const getCardTotal                = useStore((s) => s.getCardTotal)
  const getIndependentServicesTotal = useStore((s) => s.getIndependentServicesTotal)
  const getMonthExpenseTotal        = useStore((s) => s.getMonthExpenseTotal)
  const getFixedExpenseTotal        = useStore((s) => s.getFixedExpenseTotal)

  // Totals
  const totalCards    = creditCards.reduce((s, c) => s + getCardTotal(c.id, currentMonth), 0)
  const totalIndepSvc = getIndependentServicesTotal(currentMonth)
  const totalCuotasSvc = totalCards + totalIndepSvc
  const totalLoans    = loans.reduce((sum, loan) => {
    const m = loan.months.find((m) => m.month === currentMonth)
    return sum + (m ? m.amount : 0)
  }, 0)
  const totalFixed    = getFixedExpenseTotal(currentMonth)
  const totalDailyExp = getMonthExpenseTotal(currentMonth)
  const totalEgresos  = totalLoans + totalCuotasSvc + totalFixed + totalDailyExp

  // Ingresos
  const neto    = income.netMonthly || 0
  const usdCCL  = income.usdRateCCL || 0
  const sobra   = neto - totalEgresos
  const sobraOk = sobra >= 0
  const usdSobra = usdCCL > 0 ? Math.round(Math.abs(sobra) / usdCCL) : 0

  // Helpers
  const loansDetail = loans.map((loan) => {
    const bank = banks.find((b) => b.id === loan.bankId)
    const m = loan.months.find((m) => m.month === currentMonth)
    return { ...loan, bank, amount: m?.amount || 0, paid: m?.paid || false }
  })

  const fixedDetail = fixedExpenses.map((f) => {
    const m = f.months.find((m) => m.month === currentMonth)
    const amount = m ? m.amount : (f.recurring ? (f.baseAmount || 0) : 0)
    return { ...f, amount }
  })

  return (
    <div className="pb-8">
      {/* Copy month banner */}
      <CopyMonthBanner />

      <div className="p-6 max-w-3xl">
        {/* Title */}
        <div className="mb-6">
          <h2 className="text-xl font-bold text-white mb-1">
            Dashboard — <span className="text-blue-400">{formatMonthLong(currentMonth)}</span>
          </h2>
          <p className="text-sm text-[#64748b]">Resumen financiero consolidado del mes</p>
        </div>

        {/* Egress sections */}
        <div className="space-y-3 mb-4">
          {/* PRÉSTAMOS */}
          <SectionBlock title="Préstamos" total={totalLoans} color="#f97316" icon={Landmark} onNavigate={() => navigate('/loans')}>
            {loansDetail.map((loan) => (
              <DetailRow
                key={loan.id}
                label={loan.description}
                sub={`${loan.bank?.name} · ${loan.months.filter((m) => m.paid).length}/${loan.totalInstallments} pagadas${loan.months.find((m) => m.month === currentMonth)?.paid ? ' · ✓' : ''}`}
                amount={loan.amount}
                color={loan.months.find((m) => m.month === currentMonth)?.paid ? 'text-green-400' : loan.amount > 0 ? 'text-orange-300' : undefined}
                badge={loan.bank?.color || '#f97316'}
                onClick={() => navigate('/loans')}
              />
            ))}
            {loansDetail.length === 0 && <p className="px-4 py-3 text-xs text-[#3d4466]">Sin préstamos activos.</p>}
          </SectionBlock>

          {/* CUOTAS + SERVICIOS */}
          <SectionBlock title="Cuotas + Servicios" total={totalCuotasSvc} color="#3b82f6" icon={CreditCard} onNavigate={() => navigate('/dashboard')}>
            {creditCards.map((card) => {
              const bank = banks.find((b) => b.id === card.bankId)
              const total = getCardTotal(card.id, currentMonth)
              const itemCount = installments.filter(
                (i) => i.cardId === card.id && i.months.some((m) => m.month === currentMonth && m.amount > 0)
              ).length
              return (
                <DetailRow
                  key={card.id}
                  label={card.label}
                  sub={`${bank?.name} · ${itemCount} ítem${itemCount !== 1 ? 's' : ''}`}
                  amount={total}
                  badge={card.color}
                  onClick={() => navigate(`/cards/${card.id}/installments`)}
                />
              )
            })}
            {totalIndepSvc > 0 && (
              <DetailRow
                label="Servicios independientes"
                sub={`${services.filter((s) => !s.linkedCardId && s.active !== false).length} servicios`}
                amount={totalIndepSvc}
                color="text-purple-400"
                onClick={() => navigate('/services')}
              />
            )}
            {creditCards.length === 0 && totalIndepSvc === 0 && <p className="px-4 py-3 text-xs text-[#3d4466]">Sin tarjetas ni servicios.</p>}
          </SectionBlock>

          {/* RESERVAS */}
          <SectionBlock title="Reservas" total={totalFixed} color="#10b981" icon={PiggyBank} onNavigate={() => navigate('/fixed-expenses')}>
            {fixedDetail.map((f) => {
              const typeMeta = { reserve: '#3b82f6', cash: '#f59e0b', debt: '#ef4444', other: '#8b5cf6' }
              return (
                <DetailRow
                  key={f.id}
                  label={`${f.icon} ${f.label}`}
                  amount={f.amount}
                  badge={typeMeta[f.type] || '#8b5cf6'}
                  onClick={() => navigate('/fixed-expenses')}
                />
              )
            })}
            {fixedDetail.length === 0 && <p className="px-4 py-3 text-xs text-[#3d4466]">Sin reservas configuradas.</p>}
          </SectionBlock>

          {/* GASTOS DIARIOS */}
          <SectionBlock title="Gastos diarios" total={totalDailyExp} color="#f59e0b" icon={ShoppingCart} onNavigate={() => navigate('/expenses')}>
            <div className="px-4 py-3">
              <p className="text-xs text-[#64748b]">
                Ver detalle en{' '}
                <button onClick={() => navigate('/expenses')} className="text-amber-400 hover:text-amber-300 transition-colors">
                  Gastos diarios →
                </button>
              </p>
            </div>
          </SectionBlock>
        </div>

        {/* ══ TOTAL EGRESOS ══ */}
        <div className="bg-[#151820] border border-[#2e3350] rounded-xl px-4 py-3.5 mb-4">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold text-[#94a3b8] uppercase tracking-wider">Total egresos</span>
            <span className="text-lg font-bold font-mono text-red-400">{formatAmount(totalEgresos)}</span>
          </div>
        </div>

        {/* ══ INGRESOS / DUALPAY / SOBRA ══ */}
        <div className="bg-[#151820] border border-[#2e3350] rounded-xl overflow-hidden divide-y divide-[#1c2030]">

          {/* Ingreso neto */}
          <div className="flex items-center justify-between px-4 py-3">
            <div className="flex items-center gap-2">
              <Wallet size={14} className="text-green-400" />
              <span className="text-sm font-medium text-[#94a3b8]">Ingresos (neto)</span>
            </div>
            <InlineNumber
              value={neto}
              onSave={(v) => setIncome({ netMonthly: v })}
              className="text-lg font-bold font-mono text-green-400"
            />
          </div>

          {/* Sobra / Falta */}
          <div className="flex items-center justify-between px-4 py-4">
            <div className="flex items-center gap-2">
              {sobraOk
                ? <TrendingUp size={14} className="text-green-400" />
                : <TrendingDown size={14} className="text-red-400" />
              }
              <span className="text-sm font-semibold text-white">
                {sobraOk ? 'Sobra' : 'Falta'}
              </span>
            </div>
            <div className="text-right">
              <p className={`text-xl font-bold font-mono ${sobraOk ? 'text-green-400' : 'text-red-400'}`}>
                {sobraOk ? '' : '−'}{formatAmount(Math.abs(sobra))}
              </p>
              {usdCCL > 0 && sobra !== 0 && (
                <p className="text-xs text-[#64748b]">
                  ≈ USD {usdSobra.toLocaleString('es-AR')} CCL
                </p>
              )}
            </div>
          </div>

          {/* Tipo de cambio CCL + settings link */}
          <div className="flex items-center justify-between px-4 py-2.5 bg-[#0d0f14]/50">
            <div className="flex items-center gap-1.5">
              <DollarSign size={12} className="text-[#64748b]" />
              <span className="text-xs text-[#64748b]">CCL</span>
            </div>
            <div className="flex items-center gap-3">
              <InlineNumber
                value={usdCCL}
                onSave={(v) => setIncome({ usdRateCCL: v })}
                prefix=""
                suffix="$/USD"
                className="text-xs font-mono text-[#94a3b8]"
              />
              <button
                onClick={() => navigate('/settings')}
                className="text-[10px] text-[#3d4466] hover:text-[#64748b] transition-colors"
                title="Configurar en Ajustes"
              >
                Ajustes →
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
