import { useState, useEffect, useRef } from 'react'
import { CloudOff, RefreshCw, X, AlertTriangle } from 'lucide-react'
import { useDriveSync }     from '../../hooks/useDriveSync'
import { useDirtyTracker }  from '../../hooks/useDirtyTracker'

/**
 * DriveDisconnectedModal
 *
 * Aparece automáticamente cuando Drive estaba conectado (hay folderName guardada)
 * pero la sesión expiró o se perdió la conexión (connected=false + error presente).
 *
 * El usuario puede:
 *   - Reconectar → llama a connectFolder() (modal se cierra solo al éxito)
 *   - Ignorar    → descarta el modal hasta la próxima desconexión
 */
export default function DriveDisconnectedModal() {
  const {
    connected,
    folderName,
    isLoading,
    error,
    reconnect,
  } = useDriveSync()

  const isDirty = useDirtyTracker()

  // Detectar si la condición de desconexión está activa
  const isDisconnected = !connected && !!folderName && !!error && !isLoading

  const [dismissed,  setDismissed]  = useState(false)
  const [connecting, setConnecting] = useState(false)
  const [retryError, setRetryError] = useState(null)
  const prevErrorRef  = useRef(error)
  const connectingRef = useRef(false) // true mientras hay un reintento en curso

  // Resetear dismissed/retryError SOLO ante una desconexión externa genuina,
  // no ante cambios de error causados por nuestros propios intentos de reconexión.
  useEffect(() => {
    if (error !== prevErrorRef.current) {
      const wasNull = !prevErrorRef.current
      prevErrorRef.current = error
      if (wasNull && error && !connectingRef.current) {
        // Error fresco que no viene de un reintento nuestro → nueva desconexión
        setDismissed(false)
        setRetryError(null)
      }
    }
  }, [error])

  // Si reconectó con éxito, limpiar estado de intento
  useEffect(() => {
    if (connected) {
      setConnecting(false)
      connectingRef.current = false
      setRetryError(null)
    }
  }, [connected])

  if (!isDisconnected || dismissed) return null

  const handleReconnect = async () => {
    connectingRef.current = true
    setConnecting(true)
    setRetryError(null)
    try {
      await reconnect()
      // Si llega acá, isDisconnected pasará a false → modal desaparece solo
    } catch (e) {
      setRetryError(e.message || 'No se pudo reconectar.')
      setConnecting(false)
      connectingRef.current = false
    }
  }

  return (
    /* Overlay */
    <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="relative w-full max-w-sm rounded-2xl bg-[#151820] border border-[#2e3350] shadow-2xl p-6 flex flex-col gap-4">

        {/* Botón cerrar */}
        <button
          onClick={() => setDismissed(true)}
          className="absolute top-3 right-3 p-1.5 rounded-lg text-[#64748b] hover:text-white hover:bg-[#1c2030] transition-colors"
          title="Ignorar"
        >
          <X size={15} />
        </button>

        {/* Ícono + título */}
        <div className="flex items-start gap-3">
          <div className="shrink-0 w-10 h-10 rounded-xl bg-red-500/10 flex items-center justify-center">
            <CloudOff size={20} className="text-red-400" />
          </div>
          <div>
            <h2 className="text-sm font-semibold text-white leading-snug">
              Drive desconectado
            </h2>
            <p className="mt-0.5 text-xs text-[#64748b] leading-relaxed">
              La sesión con{' '}
              <span className="text-[#94a3b8] font-medium">"{folderName}"</span>{' '}
              expiró. El auto-guardado está pausado.
            </p>
          </div>
        </div>

        {/* Advertencia de cambios no guardados */}
        {isDirty && (
          <div className="flex items-start gap-2 px-3 py-2.5 rounded-lg bg-amber-500/10 border border-amber-500/20">
            <AlertTriangle size={13} className="text-amber-400 mt-0.5 shrink-0" />
            <p className="text-xs text-amber-300/90 leading-relaxed">
              Tenés cambios no guardados. Si no reconectás, pueden perderse si cerrás la app.
            </p>
          </div>
        )}

        {/* Error de reintento */}
        {retryError && (
          <p className="text-xs text-red-400/90 leading-relaxed px-1">
            {retryError}
          </p>
        )}

        {/* Acciones */}
        <div className="flex flex-col gap-2 mt-1">
          <button
            onClick={handleReconnect}
            disabled={connecting}
            className="flex items-center justify-center gap-2 w-full px-4 py-2.5 rounded-xl text-sm font-semibold bg-blue-600 hover:bg-blue-500 disabled:opacity-60 disabled:cursor-not-allowed text-white transition-colors"
          >
            <RefreshCw size={14} className={connecting ? 'animate-spin' : ''} />
            {connecting ? 'Conectando…' : 'Reconectar con Drive'}
          </button>

          <button
            onClick={() => setDismissed(true)}
            className="w-full px-4 py-2 rounded-xl text-xs font-medium text-[#64748b] hover:text-white hover:bg-[#1c2030] transition-colors"
          >
            Ignorar por ahora
          </button>
        </div>
      </div>
    </div>
  )
}
