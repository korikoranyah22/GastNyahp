import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { useEffect } from 'react'
import { Loader2 } from 'lucide-react'
import useStore from './store/useStore'
import Shell from './components/layout/Shell'
import Home from './pages/Home'
import BanksPage from './pages/banks/BanksPage'
import CardsPage from './pages/cards/CardsPage'
import InstallmentsPage from './pages/installments/InstallmentsPage'
import LoansPage from './pages/loans/LoansPage'
import ServicesPage from './pages/services/ServicesPage'
import CardsDashboard from './pages/dashboard/CardsDashboard'
import ExpensesPage from './pages/expenses/ExpensesPage'
import FixedExpensesPage from './pages/fixed/FixedExpensesPage'
import SettingsPage from './pages/settings/SettingsPage'
import DualPayPage from './pages/dualpay/DualPayPage'
import PeoplePage from './pages/people/PeoplePage'
import PriceHistoryPage from './pages/price-history/PriceHistoryPage'
import AuthScreen from './pages/auth/AuthScreen'

// El backend es la fuente de verdad: sin credencial de familia no hay app. La credencial vive en este
// dispositivo (localStorage) — posesión = identidad, la traducción moderna de "poseer el JSON".
export default function App() {
  const authStatus = useStore((s) => s.authStatus)
  const initAuth = useStore((s) => s.initAuth)

  useEffect(() => { initAuth() }, [initAuth])

  if (authStatus === 'init' || authStatus === 'loading') {
    return (
      <div className="min-h-screen flex items-center justify-center bg-[#0d0f14] text-[#64748b]">
        <div className="flex items-center gap-2 text-sm">
          <Loader2 size={16} className="animate-spin" /> Cargando GastNyahp...
        </div>
      </div>
    )
  }

  if (authStatus === 'anon') return <AuthScreen />

  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Shell />}>
          <Route path="/" element={<Home />} />
          <Route path="/banks" element={<BanksPage />} />
          <Route path="/cards" element={<CardsPage />} />
          <Route path="/cards/:cardId/installments" element={<InstallmentsPage />} />
          <Route path="/loans" element={<LoansPage />} />
          <Route path="/services" element={<ServicesPage />} />
          <Route path="/dashboard" element={<CardsDashboard />} />
          <Route path="/expenses" element={<ExpensesPage />} />
          <Route path="/fixed-expenses" element={<FixedExpensesPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/dualpay" element={<DualPayPage />} />
          <Route path="/people" element={<PeoplePage />} />
          <Route path="/price-history" element={<PriceHistoryPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
