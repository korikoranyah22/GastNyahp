import { Link, useLocation } from 'react-router-dom'
import { ShieldAlert, ChevronRight } from 'lucide-react'
import useStore from '../../store/useStore'

/**
 * El cartel de la etapa 1 (docs/DISENO_CUENTAS_LOGIN.md §3.3). Los miembros que ya existían entran solo porque
 * este dispositivo guarda su token: si lo pierden quedan afuera para siempre y ni un admin puede recuperarlos.
 *
 * No se puede descartar a propósito. Es la única palanca que tenemos para que todos tengan cuenta antes de la
 * etapa 2 (`Auth:RequireCredentials`) — si se puede ignorar, el corte encuentra gente sin cuenta y la deja afuera.
 */
export default function CreateAccountBanner() {
  const family = useStore((s) => s.family)
  const location = useLocation()

  const me = family?.members.find((m) => m.memberId === family.meId)
  // En Ajustes el formulario está a la vista: repetir el cartel arriba sería ruido.
  if (!me || me.hasCredentials || location.pathname === '/settings') return null

  return (
    <div className="shrink-0 bg-amber-500/10 border-b border-amber-500/30 px-4 py-2">
      <Link to="/settings" className="flex items-center gap-2 text-xs text-amber-300 hover:text-amber-200 transition-colors">
        <ShieldAlert size={14} className="shrink-0" />
        <span className="min-w-0">
          <span className="font-medium">Creá tu cuenta para no perder el acceso.</span>{' '}
          <span className="text-amber-300/70">Hoy entrás solo desde este dispositivo: si borrás los datos del navegador, no hay vuelta atrás.</span>
        </span>
        <ChevronRight size={14} className="ml-auto shrink-0" />
      </Link>
    </div>
  )
}
