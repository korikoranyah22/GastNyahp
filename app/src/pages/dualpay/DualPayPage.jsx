import { useState } from 'react'
import { Copy, Check } from 'lucide-react'
import useStore from '../../store/useStore'
import { formatAmount } from '../../lib/formatters'

// ── Input numérico ────────────────────────────────────────────────────────────
function NumInput({ label, value, onChange, prefix = '$', suffix = '', hint, placeholder = '0' }) {
  return (
    <div>
      <label className="block text-xs font-medium text-[#64748b] mb-1.5">{label}</label>
      <div className="flex items-center gap-2">
        {prefix && <span className="text-sm text-[#64748b] shrink-0">{prefix}</span>}
        <input
          type="number"
          value={value || ''}
          onChange={(e) => onChange(Number(e.target.value) || 0)}
          min="0"
          placeholder={placeholder}
          className="flex-1 px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm font-mono focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30 placeholder-[#3d4466]"
        />
        {suffix && <span className="text-sm text-[#64748b] shrink-0">{suffix}</span>}
      </div>
      {hint && <p className="text-[10px] text-[#3d4466] mt-1">{hint}</p>}
    </div>
  )
}

// ── Fila de resultado ─────────────────────────────────────────────────────────
function ResultRow({ label, formula, badge, amount, hasResult }) {
  return (
    <div className="flex items-center justify-between px-4 py-3 border-b border-[#1c2030]">
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          {badge && (
            <span
              className="text-[10px] font-bold px-1.5 py-0.5 rounded shrink-0"
              style={{ backgroundColor: `${badge}22`, color: badge }}
            >
              {label}
            </span>
          )}
          {!badge && <p className="text-sm text-[#94a3b8]">{label}</p>}
        </div>
        {formula && (
          <p className="text-[10px] text-[#3d4466] mt-1 font-mono">{formula}</p>
        )}
      </div>
      <span className="text-sm font-semibold font-mono text-white shrink-0 ml-4">
        {hasResult ? formatAmount(amount) : '—'}
      </span>
    </div>
  )
}

// ── Página ────────────────────────────────────────────────────────────────────
export default function DualPayPage() {
  const income = useStore((s) => s.income)

  const [brutoNeto,    setBrutoNeto]    = useState(0)
  const [dolarOficial, setDolarOficial] = useState(income.usdRateOfficial || 0)
  const [dolarCCL,     setDolarCCL]     = useState(income.usdRateCCL     || 0)
  const [copied,       setCopied]       = useState(false)

  // Cálculo
  const pesos      = Math.round(brutoNeto * 0.30)
  const usdAmount  = dolarOficial > 0 ? brutoNeto * 0.70 / dolarOficial : 0
  const ccl        = Math.round(usdAmount * dolarCCL)
  const total      = pesos + ccl

  const hasResult = brutoNeto > 0 && dolarOficial > 0 && dolarCCL > 0

  const handleCopy = async () => {
    await navigator.clipboard.writeText(String(Math.round(total)))
    setCopied(true)
    setTimeout(() => setCopied(false), 2500)
  }

  const fmt = (n) => n > 0 ? n.toLocaleString('es-AR') : '0'

  return (
    <div className="p-6 max-w-xl">
      <div className="mb-6">
        <h2 className="text-xl font-bold text-white mb-1">Dual Pay</h2>
        <p className="text-sm text-[#64748b]">
          Calculá tu ingreso neto efectivo considerando la porción dolarizada.
        </p>
      </div>

      {/* ── Parámetros ──────────────────────────────────────────────────────── */}
      <section className="mb-6">
        <h3 className="text-xs font-bold text-[#94a3b8] uppercase tracking-wider mb-4">Parámetros</h3>
        <div className="bg-[#151820] border border-[#2e3350] rounded-xl p-4 space-y-4">

          <NumInput
            label="Bruto Neto (BN)"
            value={brutoNeto}
            onChange={setBrutoNeto}
            hint="Sueldo bruto neto sobre el que se aplica la conversión"
          />

          <div className="grid grid-cols-2 gap-4">
            <NumInput
              label="Dólar oficial"
              value={dolarOficial}
              onChange={setDolarOficial}
              prefix=""
              suffix="$/USD"
            />
            <NumInput
              label="Dólar CCL"
              value={dolarCCL}
              onChange={setDolarCCL}
              prefix=""
              suffix="$/USD"
            />
          </div>
        </div>
      </section>

      {/* ── Resultado ───────────────────────────────────────────────────────── */}
      <section>
        <h3 className="text-xs font-bold text-[#94a3b8] uppercase tracking-wider mb-4">Cálculo</h3>
        <div className="bg-[#151820] border border-[#2e3350] rounded-xl overflow-hidden">

          {/* Porción en pesos (30%) */}
          <ResultRow
            label="30%"
            formula={hasResult ? `${formatAmount(brutoNeto)} × 30% = ${formatAmount(pesos)}` : 'BN × 30%'}
            badge="#10b981"
            amount={pesos}
            hasResult={hasResult}
          />

          {/* Porción dolarizada (70%) */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-[#1c2030]">
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span
                  className="text-[10px] font-bold px-1.5 py-0.5 rounded shrink-0"
                  style={{ backgroundColor: '#3b82f622', color: '#3b82f6' }}
                >
                  70%
                </span>
              </div>
              {hasResult ? (
                <>
                  <p className="text-[10px] text-[#3d4466] mt-1 font-mono">
                    {formatAmount(Math.round(brutoNeto * 0.70))} ÷ {fmt(dolarOficial)} ofic. = USD {Math.round(usdAmount).toLocaleString('es-AR')}
                  </p>
                  <p className="text-[10px] text-blue-400/70 mt-0.5 font-mono">
                    USD {Math.round(usdAmount).toLocaleString('es-AR')} × {fmt(dolarCCL)} CCL
                  </p>
                </>
              ) : (
                <p className="text-[10px] text-[#3d4466] mt-1">
                  BN × 70% ÷ oficial × CCL
                </p>
              )}
            </div>
            <span className="text-sm font-semibold font-mono text-white shrink-0 ml-4">
              {hasResult ? formatAmount(ccl) : '—'}
            </span>
          </div>

          {/* Total */}
          <div className="flex items-center justify-between px-4 py-5 bg-[#0d0f14]/60">
            <div>
              <p className="text-xs font-bold text-[#94a3b8] uppercase tracking-wider">Ingreso neto efectivo</p>
              <p className="text-[10px] text-[#3d4466] mt-0.5">
                Copiá este valor → pegalo en Inicio como ingreso mensual
              </p>
            </div>
            <div className="flex items-center gap-3 shrink-0 ml-4">
              <span className={`text-2xl font-bold font-mono ${hasResult ? 'text-green-400' : 'text-[#3d4466]'}`}>
                {hasResult ? formatAmount(total) : '—'}
              </span>
              {hasResult && (
                <button
                  onClick={handleCopy}
                  title="Copiar valor"
                  className={`flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-medium border transition-all ${
                    copied
                      ? 'bg-green-600/20 border-green-500/40 text-green-400'
                      : 'border-[#2e3350] text-[#64748b] hover:text-white hover:border-[#3d4466]'
                  }`}
                >
                  {copied ? <Check size={13} /> : <Copy size={13} />}
                  {copied ? 'Copiado' : 'Copiar'}
                </button>
              )}
            </div>
          </div>
        </div>

        {/* Nota */}
        <p className="text-[10px] text-[#3d4466] mt-3 leading-relaxed">
          Fórmula: (BN × 30%) + (BN × 70% ÷ oficial × CCL). El 30% queda en pesos y
          el 70% se convierte a USD al oficial y se devuelve a pesos al CCL.
        </p>
      </section>
    </div>
  )
}
