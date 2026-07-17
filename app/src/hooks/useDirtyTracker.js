import { useState, useEffect } from 'react'
import { getDirtyState, subscribeDirty } from '../lib/dirtyTracker'

/**
 * Devuelve true cuando hay cambios no guardados desde el último markClean().
 * Se actualiza en tiempo real conforme cambia el store.
 */
export function useDirtyTracker() {
  const [isDirty, setIsDirty] = useState(getDirtyState)
  useEffect(() => subscribeDirty(setIsDirty), [])
  return isDirty
}
