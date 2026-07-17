/**
 * dirtyTracker.js — Rastreador de cambios no guardados
 *
 * Compara un snapshot JSON de los 9 slices de datos del store contra el
 * estado en cada cambio de Zustand. Cuando difieren → isDirty = true.
 *
 * Llamar markClean() después de: Drive save, exportar/descargar, importar.
 * Llamar initDirtyTracker() una sola vez al arrancar la app.
 */

import useStore from '../store/useStore'

// ── Estado interno ─────────────────────────────────────────────────────────────
let isDirty        = false
let cleanSnapshot  = null
let storeUnsub     = null
const listeners    = new Set()

// ── Helpers ────────────────────────────────────────────────────────────────────
function getSnapshot() {
  const s = useStore.getState()
  return JSON.stringify([
    s.banks,
    s.creditCards,
    s.installments,
    s.loans,
    s.services,
    s.expenses,
    s.budgets,
    s.fixedExpenses,
    s.income,
  ])
}

function notify() {
  listeners.forEach((fn) => fn(isDirty))
}

// ── API pública ────────────────────────────────────────────────────────────────

/** Estado actual (booleano). */
export const getDirtyState = () => isDirty

/** Suscribirse a cambios de isDirty. Devuelve función de cleanup. */
export function subscribeDirty(fn) {
  listeners.add(fn)
  return () => listeners.delete(fn)
}

/**
 * Marcar el estado actual como "guardado".
 * Resetea el snapshot y notifica si el estado cambia.
 */
export function markClean() {
  cleanSnapshot = getSnapshot()
  if (isDirty) {
    isDirty = false
    notify()
  }
}

/**
 * Inicializar el tracker. Llamar una sola vez al montar la app.
 * Suscribe al store para detectar cualquier cambio en los datos.
 */
export function initDirtyTracker() {
  cleanSnapshot = getSnapshot()
  isDirty = false

  if (storeUnsub) storeUnsub()

  storeUnsub = useStore.subscribe(() => {
    const current  = getSnapshot()
    const newDirty = current !== cleanSnapshot
    if (newDirty !== isDirty) {
      isDirty = newDirty
      notify()
    }
  })
}
