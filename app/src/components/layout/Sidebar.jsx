import { NavLink } from 'react-router-dom'
import {
  Home, CreditCard, Landmark, Settings,
  ChevronLeft, ChevronRight, Building2,
  Zap, LayoutDashboard, ShoppingCart, PiggyBank, Calculator,
  Sun, Moon, Users, TrendingUp
} from 'lucide-react'
import { useState, Fragment } from 'react'
import useStore from '../../store/useStore'
import clsx from 'clsx'
import { useTheme } from '../../hooks/useTheme'

// Grupos de navegación
const NAV_GROUPS = [
  {
    label: null,  // sin encabezado
    items: [
      { to: '/', label: 'Inicio', icon: Home, end: true },
      { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
      { to: '/dualpay', label: 'Dual Pay', icon: Calculator },
    ],
  },
  {
    label: 'Gastos',
    items: [
      { to: '/expenses', label: 'Diarios', icon: ShoppingCart },
      { to: '/fixed-expenses', label: 'Reservas', icon: PiggyBank },
      { to: '/price-history', label: 'Precios', icon: TrendingUp },
    ],
  },
  {
    label: 'Finanzas',
    items: [
      { to: '/banks', label: 'Bancos', icon: Building2 },
      { to: '/cards', label: 'Tarjetas', icon: CreditCard },
      { to: '/loans', label: 'Préstamos', icon: Landmark },
      { to: '/services', label: 'Servicios', icon: Zap },
    ],
  },
  {
    label: 'Personal',
    items: [
      { to: '/people', label: 'Familia', icon: Users },
    ],
  },
]

export default function Sidebar() {
  const [collapsed, setCollapsed] = useState(false)
  const creditCards = useStore((s) => s.creditCards)
  const { isCozy, toggleTheme } = useTheme()

  return (
    <aside
      className={clsx(
        'relative flex flex-col bg-[#0d0f14] border-r border-[#1c2030] transition-all duration-200 shrink-0',
        collapsed ? 'w-14' : 'w-52'
      )}
    >
      {/* Logo */}
      <div className="flex items-center gap-3 px-4 py-4 border-b border-[#1c2030]">
        <img
          src="/logo.png"
          alt="GastNyahp"
          className="w-7 h-7 rounded-lg object-cover shrink-0"
          onError={(e) => { e.currentTarget.style.display = 'none' }}
        />
        {!collapsed && (
          <span className="text-sm font-bold text-white tracking-wide">GastNyahp</span>
        )}
      </div>

      {/* Nav groups */}
      <nav className="flex-1 px-2 py-3 overflow-y-auto space-y-4">
        {NAV_GROUPS.map((group, gi) => (
          <div key={gi}>
            {/* Group label */}
            {group.label && !collapsed && (
              <p className="px-2.5 mb-1.5 text-[10px] font-bold text-[#3d4466] uppercase tracking-widest">
                {group.label}
              </p>
            )}
            <div className="space-y-0.5">
              {group.items.map(({ to, label, icon: Icon, end }) => (
                <Fragment key={to}>
                  <NavLink
                    to={to}
                    end={end}
                    className={({ isActive }) =>
                      clsx(
                        'flex items-center gap-3 px-2.5 py-2 rounded-lg text-sm font-medium transition-colors',
                        isActive
                          ? 'bg-blue-600/20 text-blue-400 border border-blue-600/20'
                          : 'text-[#94a3b8] hover:text-white hover:bg-[#1c2030]'
                      )
                    }
                  >
                    <Icon size={16} className="shrink-0" />
                    {!collapsed && <span className="truncate">{label}</span>}
                  </NavLink>

                  {/* Cuotas sub-nav — justo debajo del botón Tarjetas */}
                  {to === '/cards' && !collapsed && creditCards.length > 0 && (
                    <div className="pt-0.5 pl-4 space-y-0.5">
                      {creditCards.map((card) => (
                        <NavLink
                          key={card.id}
                          to={`/cards/${card.id}/installments`}
                          className={({ isActive }) =>
                            clsx(
                              'flex items-center gap-2 px-2 py-1.5 rounded-md text-xs font-medium transition-colors',
                              isActive
                                ? (isCozy ? 'text-[#9d174d]' : 'text-white')
                                : (isCozy
                                    ? 'text-[#a05878] hover:text-[#9d174d]'
                                    : 'text-[#64748b] hover:text-[#94a3b8]')
                            )
                          }
                        >
                          <span
                            className="w-2 h-2 rounded-full shrink-0"
                            style={{ backgroundColor: card.color || '#3b82f6' }}
                          />
                          <span className="truncate">{card.label}</span>
                        </NavLink>
                      ))}
                    </div>
                  )}
                </Fragment>
              ))}
            </div>
          </div>
        ))}
      </nav>

      {/* Bottom: theme toggle + settings */}
      <div className="px-2 py-3 border-t border-[#1c2030] space-y-0.5">
        {/* Theme toggle */}
        <button
          onClick={toggleTheme}
          title={isCozy ? 'Cambiar a modo oscuro' : 'Cambiar a modo cozy'}
          className={clsx(
            'flex items-center gap-3 px-2.5 py-2 rounded-lg text-sm font-medium transition-colors w-full',
            'text-[#64748b] hover:text-white hover:bg-[#1c2030]'
          )}
        >
          {isCozy ? <Sun size={16} className="shrink-0" /> : <Moon size={16} className="shrink-0" />}
          {!collapsed && <span>{isCozy ? 'Modo oscuro' : 'Modo cozy'}</span>}
        </button>

        <NavLink
          to="/settings"
          className={({ isActive }) =>
            clsx(
              'flex items-center gap-3 px-2.5 py-2 rounded-lg text-sm font-medium transition-colors',
              isActive
                ? 'bg-blue-600/20 text-blue-400'
                : 'text-[#64748b] hover:text-white hover:bg-[#1c2030]'
            )
          }
        >
          <Settings size={16} className="shrink-0" />
          {!collapsed && <span>Ajustes</span>}
        </NavLink>
      </div>

      {/* Collapse toggle */}
      <button
        onClick={() => setCollapsed(!collapsed)}
        className="absolute -right-3 top-16 w-6 h-6 rounded-full bg-[#1c2030] border border-[#2e3350] flex items-center justify-center text-[#64748b] hover:text-white transition-colors z-10"
      >
        {collapsed ? <ChevronRight size={12} /> : <ChevronLeft size={12} />}
      </button>
    </aside>
  )
}
