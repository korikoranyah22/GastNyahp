import { Outlet, useLocation } from 'react-router-dom'
import Sidebar from './Sidebar'
import Header from './Header'
import BottomNav from './BottomNav'
import CreateAccountBanner from '../ui/CreateAccountBanner'
import { useKeyboardShortcuts } from '../../hooks/useKeyboardShortcuts'

const pageTitles = {
  '/': 'Inicio',
  '/dashboard': 'Dashboard',
  '/dualpay': 'Dual Pay',
  '/banks': 'Bancos',
  '/cards': 'Tarjetas',
  '/loans': 'Préstamos',
  '/services': 'Servicios',
  '/expenses': 'Gastos diarios',
  '/fixed-expenses': 'Reservas mensuales',
  '/price-history': 'Historial de precios',
  '/settings': 'Ajustes',
}

function getTitle(pathname) {
  if (pathname.includes('/installments')) return 'Cuotas'
  return pageTitles[pathname] || 'GastNyahp'
}

export default function Shell() {
  const location = useLocation()
  const title = getTitle(location.pathname)

  // Atajos de teclado globales: ← → meses, Ctrl+S exportar
  useKeyboardShortcuts()

  return (
    <div className="flex h-screen bg-[#0d0f14] text-[#e2e8f0] overflow-hidden">
      {/* Sidebar: oculto en mobile, visible en tablet+ */}
      <div className="hidden md:flex shrink-0">
        <Sidebar />
      </div>

      <div className="flex flex-col flex-1 min-w-0">
        <Header title={title} />
        <CreateAccountBanner />
        {/* pb-14 en mobile para que el contenido no quede bajo el bottom nav */}
        <main className="flex-1 overflow-auto pb-14 md:pb-0">
          <Outlet />
        </main>
      </div>

      {/* Bottom nav: solo visible en mobile (md:hidden interno) */}
      <BottomNav />
    </div>
  )
}
