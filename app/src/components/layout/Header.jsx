import { Download, RefreshCw, LogOut, Users } from 'lucide-react'
import { useState } from 'react'
import MonthSelector from '../ui/MonthSelector'
import useStore from '../../store/useStore'

export default function Header({ title }) {
  const exportData = useStore((s) => s.exportData)
  const loadAll = useStore((s) => s.loadAll)
  const logout = useStore((s) => s.logout)
  const family = useStore((s) => s.family)
  const [refreshing, setRefreshing] = useState(false)

  // ── Backup local de lo cargado (los datos viven en el servidor) ────────────
  const handleDownload = () => {
    const json = exportData()
    const blob = new Blob([json], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `gastnyahp-${new Date().toISOString().slice(0, 10)}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  // ── Refrescar desde el servidor (otro dispositivo pudo haber cargado datos) ─
  const handleRefresh = async () => {
    setRefreshing(true)
    try { await loadAll() } finally { setRefreshing(false) }
  }

  const handleLogout = () => {
    if (window.confirm('¿Salir de la familia en este dispositivo? Vas a necesitar una invitación o tu credencial para volver a entrar.')) logout()
  }

  return (
    <header className="flex items-center justify-between px-4 py-2.5 md:px-6 md:py-3 border-b border-[#1c2030] bg-[#0d0f14] shrink-0">
      <h1 className="text-sm font-semibold text-white">{title}</h1>

      <div className="flex items-center gap-2 md:gap-3">
        <MonthSelector />

        {/* Familia activa */}
        {family && (
          <div className="hidden sm:flex items-center gap-1.5 text-[11px] text-[#64748b]" title={`Familia ${family.name}`}>
            <Users size={12} />
            <span className="max-w-[110px] truncate">{family.name}</span>
          </div>
        )}

        <div className="hidden sm:block w-px h-5 bg-[#1c2030]" />

        <button
          onClick={handleRefresh}
          className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-medium text-[#94a3b8] hover:text-white hover:bg-[#1c2030] border border-[#2e3350] transition-colors"
          title="Refrescar desde el servidor"
        >
          <RefreshCw size={13} className={refreshing ? 'animate-spin' : ''} />
          <span className="hidden md:inline">Refrescar</span>
        </button>

        <button
          onClick={handleDownload}
          className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-medium text-[#94a3b8] hover:text-white hover:bg-[#1c2030] border border-[#2e3350] transition-colors"
          title="Descargar backup JSON"
        >
          <Download size={13} />
          <span className="hidden md:inline">Backup</span>
        </button>

        <button
          onClick={handleLogout}
          className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-medium text-[#94a3b8] hover:text-red-300 hover:bg-[#1c2030] border border-[#2e3350] transition-colors"
          title="Salir de la familia en este dispositivo"
        >
          <LogOut size={13} />
          <span className="hidden lg:inline">Salir</span>
        </button>
      </div>
    </header>
  )
}
