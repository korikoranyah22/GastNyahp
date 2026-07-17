import { useState, useEffect } from 'react'
import {
  getDriveSyncStatus,
  subscribeDriveSync,
  connectFolder,
  reconnect,
  saveToDrive,
  disconnect,
  getClientId,
  setClientId,
  resolveMerge,
  toggleAutoSave,
} from '../lib/driveSync'

/**
 * Hook React que expone el estado de sincronización con Google Drive
 * y las acciones disponibles.
 */
export function useDriveSync() {
  const [status, setStatus] = useState(getDriveSyncStatus)
  useEffect(() => subscribeDriveSync(setStatus), [])
  return {
    ...status,
    connectFolder,
    reconnect,
    saveToDrive,
    disconnect,
    getClientId,
    setClientId,
    resolveMerge,
    toggleAutoSave,
  }
}
