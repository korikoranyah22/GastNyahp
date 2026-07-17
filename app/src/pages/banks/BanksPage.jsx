import { useState } from 'react'
import { Plus, Pencil, Trash2, CreditCard, Landmark, Building2 } from 'lucide-react'
import useStore from '../../store/useStore'
import BankForm from './BankForm'
import EmptyState from '../../components/ui/EmptyState'
import Badge from '../../components/ui/Badge'

export default function BanksPage() {
  const banks = useStore((s) => s.banks)
  const creditCards = useStore((s) => s.creditCards)
  const loans = useStore((s) => s.loans)
  const deleteBank = useStore((s) => s.deleteBank)

  const [formOpen, setFormOpen] = useState(false)
  const [editBank, setEditBank] = useState(null)
  const [deleteError, setDeleteError] = useState('')

  const getCardCount = (bankId) => creditCards.filter((c) => c.bankId === bankId).length
  const getLoanCount = (bankId) => loans.filter((l) => l.bankId === bankId).length

  const handleDelete = async (bank) => {
    if (!window.confirm(`¿Eliminar el banco "${bank.name}"?`)) return
    const result = await deleteBank(bank.id)
    if (result?.error) setDeleteError(result.error)
    else setDeleteError('')
  }

  const handleEdit = (bank) => {
    setEditBank(bank)
    setFormOpen(true)
  }

  const handleCloseForm = () => {
    setFormOpen(false)
    setEditBank(null)
  }

  return (
    <div className="p-6">
      {/* Page header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-lg font-semibold text-white">Bancos</h2>
          <p className="text-xs text-[#64748b] mt-0.5">Administrá las entidades bancarias</p>
        </div>
        <button
          onClick={() => setFormOpen(true)}
          className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
        >
          <Plus size={15} />
          Nuevo banco
        </button>
      </div>

      {deleteError && (
        <div className="mb-4 p-3 rounded-lg bg-red-500/10 border border-red-500/20 text-sm text-red-400">
          {deleteError}
        </div>
      )}

      {/* Banks grid */}
      {banks.length === 0 ? (
        <EmptyState
          icon={Building2}
          title="No hay bancos configurados"
          description="Agregá tu primer banco para empezar a gestionar tus tarjetas y préstamos."
          action={
            <button
              onClick={() => setFormOpen(true)}
              className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium transition-colors"
            >
              <Plus size={15} />
              Agregar banco
            </button>
          }
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {banks.map((bank) => {
            const cardCount = getCardCount(bank.id)
            const loanCount = getLoanCount(bank.id)
            return (
              <div
                key={bank.id}
                className="relative group bg-[#151820] border border-[#2e3350] rounded-xl p-5 hover:border-[#3d4466] transition-colors"
              >
                {/* Color accent */}
                <div
                  className="absolute top-0 left-0 right-0 h-1 rounded-t-xl"
                  style={{ backgroundColor: bank.color }}
                />

                {/* Bank icon + name */}
                <div className="flex items-start justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div
                      className="w-10 h-10 rounded-xl flex items-center justify-center text-always-white font-bold text-lg"
                      style={{ backgroundColor: bank.color }}
                    >
                      {bank.name.charAt(0).toUpperCase()}
                    </div>
                    <div>
                      <p className="text-sm font-semibold text-white">{bank.name}</p>
                      {bank.alias && <p className="text-xs text-[#64748b]">{bank.alias}</p>}
                    </div>
                  </div>
                  {/* Actions */}
                  <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button
                      onClick={() => handleEdit(bank)}
                      className="p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#2e3350] transition-colors"
                    >
                      <Pencil size={13} />
                    </button>
                    <button
                      onClick={() => handleDelete(bank)}
                      className="p-1.5 rounded-lg text-[#64748b] hover:text-red-400 hover:bg-red-500/10 transition-colors"
                    >
                      <Trash2 size={13} />
                    </button>
                  </div>
                </div>

                {/* Stats */}
                <div className="flex gap-2 flex-wrap">
                  <Badge variant="blue">
                    <CreditCard size={10} className="mr-1" />
                    {cardCount} tarjeta{cardCount !== 1 ? 's' : ''}
                  </Badge>
                  <Badge variant="orange">
                    <Landmark size={10} className="mr-1" />
                    {loanCount} préstamo{loanCount !== 1 ? 's' : ''}
                  </Badge>
                </div>
              </div>
            )
          })}

          {/* Add card */}
          <button
            onClick={() => setFormOpen(true)}
            className="border-2 border-dashed border-[#2e3350] rounded-xl p-5 flex flex-col items-center justify-center gap-2 text-[#3d4466] hover:text-[#64748b] hover:border-[#3d4466] transition-colors min-h-[120px]"
          >
            <Plus size={20} />
            <span className="text-xs font-medium">Agregar banco</span>
          </button>
        </div>
      )}

      {/* Form SlideOver */}
      <BankForm
        open={formOpen}
        onClose={handleCloseForm}
        bank={editBank}
      />
    </div>
  )
}
