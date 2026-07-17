import { useState } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import {
  Home, LayoutDashboard, ShoppingCart, CreditCard,
  MoreHorizontal, Building2, Landmark, Zap,
  PiggyBank, Calculator, Settings, Sun, Moon, X, Users, TrendingUp
} from 'lucide-react'
import useStore from '../../store/useStore'
import { useTheme } from '../../hooks/useTheme'

// Ítems en los 5 spots del bottom nav
const BOTTOM_ITEMS = [
  { to: '/',          label: 'Inicio',   icon: Home,           end: true },
  { to: '/dashboard', label: 'Cuotas',   icon: LayoutDashboard           },
  { to: '/expenses',  label: 'Gastos',   icon: ShoppingCart              },
  { to: '/cards',     label: 'Tarjetas', icon: CreditCard                },
]

// Ítems del bottom sheet "Más"
const MORE_ITEMS = [
  { to: '/banks',          label: 'Bancos',    icon: Building2  },
  { to: '/loans',          label: 'Préstamos', icon: Landmark   },
  { to: '/services',       label: 'Servicios', icon: Zap        },
  { to: '/fixed-expenses', label: 'Reservas',  icon: PiggyBank  },
  { to: '/price-history',  label: 'Precios',   icon: TrendingUp },
  { to: '/dualpay',        label: 'Dual Pay',  icon: Calculator },
  { to: '/people',         label: 'Familia',   icon: Users      },
]

export default function BottomNav() {
  const [sheetOpen, setSheetOpen] = useState(false)
  const { isCozy, toggleTheme } = useTheme()
  const navigate = useNavigate()

  const handleMoreNav = (to) => {
    setSheetOpen(false)
    navigate(to)
  }

  return (
    <>
      {/* ── Bottom Nav Bar ───────────────────────────────────────────────── */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 z-40 h-14 bg-[#0d0f14] border-t border-[#1c2030] flex items-stretch">
        {BOTTOM_ITEMS.map(({ to, label, icon: Icon, end }) => (
          <NavLink
            key={to}
            to={to}
            end={end}
            className={({ isActive }) =>
              `flex-1 flex flex-col items-center justify-center gap-0.5 text-[10px] font-medium transition-colors ${
                isActive ? 'text-blue-400' : 'text-[#64748b]'
              }`
            }
          >
            {({ isActive }) => (
              <>
                <Icon size={19} />
                <span>{label}</span>
              </>
            )}
          </NavLink>
        ))}

        {/* Botón "Más" */}
        <button
          onClick={() => setSheetOpen(true)}
          className={`flex-1 flex flex-col items-center justify-center gap-0.5 text-[10px] font-medium transition-colors ${
            sheetOpen ? 'text-blue-400' : 'text-[#64748b]'
          }`}
        >
          <MoreHorizontal size={19} />
          <span>Más</span>
        </button>
      </nav>

      {/* ── Bottom Sheet "Más" ───────────────────────────────────────────── */}
      {sheetOpen && (
        <div className="md:hidden fixed inset-0 z-50 flex flex-col justify-end">
          {/* Backdrop */}
          <div
            className="absolute inset-0 bg-black/60 fade-in"
            onClick={() => setSheetOpen(false)}
          />

          {/* Panel */}
          <div className="relative bg-[#151820] rounded-t-2xl border-t border-[#2e3350] shadow-2xl slide-in-up pb-safe">
            {/* Handle */}
            <div className="flex items-center justify-between px-5 pt-4 pb-2">
              <div className="w-10 h-1 rounded-full bg-[#2e3350] mx-auto absolute left-1/2 -translate-x-1/2 top-2" />
              <span className="text-sm font-semibold text-white">Más</span>
              <button
                onClick={() => setSheetOpen(false)}
                className="p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#2e3350] transition-colors"
              >
                <X size={15} />
              </button>
            </div>

            {/* Nav items */}
            <div className="px-3 pb-3 space-y-0.5">
              {MORE_ITEMS.map(({ to, label, icon: Icon }) => (
                <button
                  key={to}
                  onClick={() => handleMoreNav(to)}
                  className="w-full flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium text-[#94a3b8] hover:text-white hover:bg-[#1c2030] transition-colors text-left"
                >
                  <Icon size={17} className="shrink-0" />
                  {label}
                </button>
              ))}

              {/* Divider */}
              <div className="my-2 border-t border-[#2e3350]" />

              {/* Theme toggle */}
              <button
                onClick={() => { toggleTheme(); setSheetOpen(false) }}
                className="w-full flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium text-[#64748b] hover:text-white hover:bg-[#1c2030] transition-colors text-left"
              >
                {isCozy
                  ? <Sun size={17} className="shrink-0" />
                  : <Moon size={17} className="shrink-0" />
                }
                {isCozy ? 'Modo oscuro' : 'Modo cozy'}
              </button>

              {/* Settings */}
              <button
                onClick={() => handleMoreNav('/settings')}
                className="w-full flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium text-[#64748b] hover:text-white hover:bg-[#1c2030] transition-colors text-left"
              >
                <Settings size={17} className="shrink-0" />
                Ajustes
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
