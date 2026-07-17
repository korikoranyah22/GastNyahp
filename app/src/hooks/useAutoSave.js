import { useState, useEffect } from 'react'
import {
  getAutoSaveStatus,
  subscribeAutoSave,
  pickAndConnect,
  disconnect,
  saveNow,
  isAutoSaveSupported,
} from '../lib/autoSave'

/**
 * Hook React para consumir el estado de auto-guardado.
 *
 * Retorna:
 *   fileName    — nombre del archivo conectado (null si no hay)
 *   lastSaved   — Date del último guardado exitoso (null si nunca)
 *   isSaving    — true mientras se está escribiendo el archivo
 *   error       — mensaje de error del último intento (null si ok)
 *   isSupported — true si el navegador soporta la File System Access API
 *   pickAndConnect — fn: abre el picker y conecta el archivo
 *   disconnect  — fn: desconecta el archivo
 *   saveNow     — fn: guarda inmediatamente sin debounce
 */
export function useAutoSave() {
  const [status, setStatus] = useState(getAutoSaveStatus)

  useEffect(() => subscribeAutoSave(setStatus), [])

  return {
    ...status,
    isSupported: isAutoSaveSupported(),
    pickAndConnect,
    disconnect,
    saveNow,
  }
}
