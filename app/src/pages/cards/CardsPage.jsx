import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Plus, Pencil, Trash2, CreditCard, ChevronRight } from 'lucide-react'
import useStore from '../../store/useStore'
import CardForm from './CardForm'
import EmptyState from '../../components/ui/EmptyState'
import Badge from '../../components/ui/Badge'
import { formatAmount } from '../../lib/formatters'

// Estado del mes para la tarjeta: 'paid' | 'pending' | 'none'
function getCardMonthStatus(installments, cardId, month) {
  const cardInst = installments.filter(
    (i) => i.cardId === cardId && i.active !== false &&
      i.months.some((m) => m.month === month && m.amount > 0)
  )
  if (cardInst.length === 0) return 'none'
  const allPaid = cardInst.every(
    (i) => !!i.months.find((m) => m.month === month)?.paid
  )
  return allPaid ? 'paid' : 'pending'
}

function CreditCardVisual({ card, onClick, onEdit, onDelete, monthTotal, status }) {
  const network = card.network

  return (
    <div className="relative group cursor-pointer" onClick={onClick}>
      {/* Physical card */}
      <div
        className="relative h-40 rounded-2xl p-5 overflow-hidden transition-transform hover:scale-[1.02] hover:shadow-2xl"
        style={{
          background: `linear-gradient(135deg, ${card.color}ff 0%, ${card.color}aa 60%, ${card.color}66 100%)`,
          boxShadow: `0 8px 32px ${card.color}33`,
        }}
      >
        {/* Shine */}
        <div className="absolute inset-0 opacity-20"
          style={{ backgroundImage: 'radial-gradient(ellipse at 20% 20%, white 0%, transparent 60%)' }}
        />
        {/* Chip */}
        <div className="absolute top-5 left-5 w-8 h-6 rounded bg-yellow-400/80" />

        {/* Status dot — esquina superior izquierda */}
        {status !== 'none' && (
          <div
            className={`absolute top-3 left-3 w-3 h-3 rounded-full border-2 border-black/30 ${
              status === 'paid' ? 'bg-green-400' : 'bg-red-400'
            }`}
            title={status === 'paid' ? '✓ Cuotas del mes pagadas' : '⏳ Cuotas pendientes este mes'}
          />
        )}

        {/* Network logo */}
        <div className="absolute top-4 right-4">
          {network === 'VISA' ? (
            <span className="text-white font-black text-xl italic tracking-tight">VISA</span>
          ) : (
            <div className="flex -space-x-2">
              <div className="w-7 h-7 rounded-full bg-red-500 opacity-90" />
              <div className="w-7 h-7 rounded-full bg-yellow-400 opacity-90" />
            </div>
          )}
        </div>

        {/* Card label */}
        <div className="absolute bottom-4 left-5 right-5">
          <p className="text-white/70 text-xs mb-0.5">
            {card.type === 'credit' ? 'Crédito' : 'Débito'} · Cierre día {card.closingDay}
          </p>
          <p className="text-always-white font-semibold text-sm">{card.label}</p>
        </div>

        {/* Actions overlay */}
        <div
          className="absolute top-3 right-3 flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity"
          onClick={(e) => e.stopPropagation()}
        >
          <button
            onClick={(e) => { e.stopPropagation(); onEdit() }}
            className="p-1.5 rounded-lg bg-black/40 text-white/70 hover:text-white hover:bg-black/60 transition-colors"
          >
            <Pencil size={12} />
          </button>
          <button
            onClick={(e) => { e.stopPropagation(); onDelete() }}
            className="p-1.5 rounded-lg bg-black/40 text-white/70 hover:text-red-400 hover:bg-black/60 transition-colors"
          >
            <Trash2 size={12} />
          </button>
        </div>
      </div>

      {/* Month total + status text */}
      <div className="mt-3 flex items-center justify-between px-1">
        <div>
          <p className="text-xs text-[#64748b]">Cuotas del mes</p>
          <div className="flex items-center gap-1.5">
            <p className={`text-sm font-semibold font-mono ${monthTotal > 0 ? 'text-white' : 'text-[#3d4466]'}`}>
              {monthTotal > 0 ? formatAmount(monthTotal) : '—'}
            </p>
            {status === 'paid' && (
              <span className="text-[10px] text-green-400 font-medium">✓</span>
            )}
            {status === 'pending' && (
              <span className="text-[10px] text-red-400 font-medium">pendiente</span>
            )}
          </div>
        </div>
        <div className="flex items-center gap-1 text-xs text-[#64748b] hover:text-white transition-colors">
          <span>Ver cuotas</span>
          <ChevronRight size={12} />
        </div>
      </div>
    </div>
  )
}

export default function CardsPage() {
  const banks        = useStore((s) => s.banks)
  const creditCards  = useStore((s) => s.creditCards)
  const installments = useStore((s) => s.installments)
  const deleteCard   = useStore((s) => s.deleteCard)
  const currentMonth = useStore((s) => s.currentMonth)
  const getCardInstallmentsTotal = useStore((s) => s.getCardInstallmentsTotal)

  const [formOpen, setFormOpen] = useState(false)
  const [editCard, setEditCard] = useState(null)
  const navigate = useNavigate()

  const handleDelete = async (card) => {
    if (!window.confirm(`¿Eliminar la tarjeta "${card.label}"?`)) return
    const result = await deleteCard(card.id)
    if (result?.error) alert(result.error)
  }

  const handleEdit = (card) => { setEditCard(card); setFormOpen(true) }
  const handleClose = () => { setFormOpen(false); setEditCard(null) }

  const cardsByBank = banks.map((bank) => ({
    bank,
    cards: creditCards.filter((c) => c.bankId === bank.id),
  })).filter((g) => g.cards.length > 0)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-lg font-semibold text-white">Tarjetas</h2>
          <p className="text-xs text-[#64748b] mt-0.5">
            {creditCards.length} tarjeta{creditCards.length !== 1 ? 's' : ''} configuradas
          </p>
        </div>
        <button
          onClick={() => setFormOpen(true)}
          className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
        >
          <Plus size={15} />
          Nueva tarjeta
        </button>
      </div>

      {creditCards.length === 0 ? (
        <EmptyState
          icon={CreditCard}
          title="No hay tarjetas configuradas"
          description="Agregá tus tarjetas de crédito y débito para gestionar cuotas."
          action={
            <button
              onClick={() => setFormOpen(true)}
              className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium"
            >
              <Plus size={15} /> Agregar tarjeta
            </button>
          }
        />
      ) : (
        <div className="space-y-8">
          {cardsByBank.map(({ bank, cards }) => (
            <div key={bank.id}>
              <div className="flex items-center gap-2 mb-4">
                <div
                  className="w-5 h-5 rounded-md flex items-center justify-center text-always-white text-xs font-bold"
                  style={{ backgroundColor: bank.color }}
                >
                  {bank.name.charAt(0)}
                </div>
                <h3 className="text-sm font-semibold text-[#94a3b8]">{bank.name}</h3>
                <div className="flex-1 h-px bg-[#1c2030]" />
                <Badge variant="gray">{cards.length} tarjeta{cards.length !== 1 ? 's' : ''}</Badge>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                {cards.map((card) => (
                  <CreditCardVisual
                    key={card.id}
                    card={card}
                    monthTotal={getCardInstallmentsTotal(card.id, currentMonth)}
                    status={getCardMonthStatus(installments, card.id, currentMonth)}
                    onClick={() => navigate(`/cards/${card.id}/installments`)}
                    onEdit={() => handleEdit(card)}
                    onDelete={() => handleDelete(card)}
                  />
                ))}
              </div>
            </div>
          ))}
          <button
            onClick={() => setFormOpen(true)}
            className="flex items-center gap-2 px-4 py-2 border-2 border-dashed border-[#2e3350] rounded-xl text-[#3d4466] hover:text-[#64748b] hover:border-[#3d4466] transition-colors text-sm"
          >
            <Plus size={15} /> Agregar tarjeta
          </button>
        </div>
      )}

      <CardForm open={formOpen} onClose={handleClose} card={editCard} />
    </div>
  )
}
