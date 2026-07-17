import { useState, useEffect } from 'react'
import SlideOver from '../../components/ui/SlideOver'
import AmountInput from '../../components/ui/AmountInput'
import MonthPicker from '../../components/ui/MonthPicker'
import useStore from '../../store/useStore'
import { formatAmount } from '../../lib/formatters'

const BLANK = {
  bankId: '',
  description: '',
  totalAmount: '',
  monthlyInstallment: '',
  startDate: new Date().toISOString().slice(0, 7) + '-01',
  totalInstallments: 12,
}

export default function LoanForm({ open, onClose, loan = null }) {
  const banks = useStore((s) => s.banks)
  const addLoan = useStore((s) => s.addLoan)
  const updateLoan = useStore((s) => s.updateLoan)

  const [form, setForm] = useState({ ...BLANK, bankId: banks[0]?.id || '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const isEdit = !!loan

  useEffect(() => {
    if (open) {
      setForm(loan ? {
        bankId: loan.bankId,
        description: loan.description,
        totalAmount: loan.totalAmount,
        monthlyInstallment: loan.monthlyInstallment,
        startDate: loan.startDate,
        totalInstallments: loan.totalInstallments,
      } : { ...BLANK, bankId: banks[0]?.id || '' })
      setError('')
    }
  }, [open, loan])

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.bankId) { setError('Seleccioná un banco'); return }
    if (!form.description.trim()) { setError('La descripción es requerida'); return }
    if (!form.monthlyInstallment) { setError('La cuota mensual es requerida'); return }
    setError(''); setBusy(true)
    const result = await (isEdit ? updateLoan(loan.id, form) : addLoan(form))
    setBusy(false)
    if (result.error) setError(result.error)
    else onClose()
  }

  const estimatedTotal = form.monthlyInstallment && form.totalInstallments
    ? form.monthlyInstallment * form.totalInstallments
    : null

  return (
    <SlideOver open={open} onClose={onClose} title={isEdit ? 'Editar préstamo' : 'Nuevo préstamo'}>
      <form onSubmit={handleSubmit} className="space-y-5">

        {/* Banco */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Banco *</label>
          {banks.length === 0 ? (
            <p className="text-xs text-red-400">Primero debés crear un banco.</p>
          ) : (
            <select
              value={form.bankId}
              onChange={(e) => set('bankId', e.target.value)}
              disabled={isEdit}
              className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500"
            >
              {banks.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
          )}
          {isEdit && <p className="mt-1 text-xs text-[#64748b]">El banco no puede cambiarse después de crear el préstamo.</p>}
        </div>

        {/* Descripción */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Descripción *</label>
          <input
            type="text"
            value={form.description}
            onChange={(e) => set('description', e.target.value)}
            placeholder="ej. Préstamo BBVA Personal"
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500 placeholder:text-[#3d4466]"
          />
        </div>

        {/* Monto total */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Monto total del préstamo</label>
          <AmountInput value={form.totalAmount} onChange={(v) => set('totalAmount', v)} />
        </div>

        {/* Cuota mensual */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Cuota mensual *</label>
          <AmountInput value={form.monthlyInstallment} onChange={(v) => set('monthlyInstallment', v)} />
          <p className="text-[10px] text-[#64748b] mt-1">Podés editar el monto de cada mes individualmente después.</p>
        </div>

        {/* Fecha de inicio */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Mes de inicio</label>
          <MonthPicker
            value={form.startDate.slice(0, 7)}
            onChange={(ym) => set('startDate', ym + '-01')}
          />
        </div>

        {/* Cantidad de cuotas */}
        <div>
          <label className="block text-xs font-medium text-[#94a3b8] mb-1.5">Cantidad de cuotas</label>
          <input
            type="number"
            min={1} max={360}
            value={form.totalInstallments}
            onChange={(e) => set('totalInstallments', Number(e.target.value))}
            className="w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm focus:outline-none focus:border-blue-500"
          />
        </div>

        {/* Summary */}
        {estimatedTotal && (
          <div className="p-3 rounded-lg bg-[#1c2030] border border-[#2e3350] space-y-1">
            <div className="flex justify-between">
              <span className="text-xs text-[#64748b]">Cuota mensual</span>
              <span className="text-xs font-semibold text-white mono">{formatAmount(form.monthlyInstallment)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-xs text-[#64748b]">Cantidad de cuotas</span>
              <span className="text-xs font-semibold text-white">{form.totalInstallments}</span>
            </div>
            <div className="flex justify-between border-t border-[#2e3350] pt-1 mt-1">
              <span className="text-xs text-[#64748b]">Total estimado</span>
              <span className="text-xs font-bold text-orange-400 mono">{formatAmount(estimatedTotal)}</span>
            </div>
          </div>
        )}

        {error && <p className="text-xs text-red-400">{error}</p>}

        <div className="flex gap-2 pt-2">
          <button type="button" onClick={onClose}
            className="flex-1 px-4 py-2 rounded-lg border border-[#2e3350] text-sm text-[#94a3b8] hover:text-white transition-colors">
            Cancelar
          </button>
          <button type="submit" disabled={busy}
            className="flex-1 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors">
            {isEdit ? 'Guardar' : 'Crear préstamo'}
          </button>
        </div>
      </form>
    </SlideOver>
  )
}
