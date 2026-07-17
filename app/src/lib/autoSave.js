/**
 * autoSave.js — Auto-guardado en tiempo real al dispositivo
 *
 * Usa la File System Access API (Chrome/Edge 86+) para escribir el estado
 * del store en un archivo JSON del dispositivo cada vez que algo cambia.
 * El FileSystemFileHandle vive solo en memoria (se pierde al recargar).
 */

import useStore from '../store/useStore'

// ── Estado interno del módulo ──────────────────────────────────────────────────
let fileHandle = null   // FileSystemFileHandle | null
let storeUnsub = null   // función de cleanup del subscribe de Zustand
let saveTimer  = null   // id del debounce timer

let status = { fileName: null, lastSaved: null, isSaving: false, error: null }
const listeners = new Set()

function notify() {
  listeners.forEach((fn) => fn({ ...status }))
}

// ── API pública ────────────────────────────────────────────────────────────────

/** Estado actual (snapshot inmutable) */
export function getAutoSaveStatus() {
  return { ...status }
}

/** Suscribirse a cambios de estado. Devuelve función de cleanup. */
export function subscribeAutoSave(fn) {
  listeners.add(fn)
  return () => listeners.delete(fn)
}

/** Verdadero si el navegador soporta la File System Access API */
export function isAutoSaveSupported() {
  return 'showSaveFilePicker' in window
}

/** Escribe el JSON al archivo de forma inmediata (sin debounce). */
export async function saveNow() {
  clearTimeout(saveTimer)
  return _doSave()
}

/**
 * Abre el picker nativo del OS, guarda el handle elegido y
 * suscribe al store para auto-guardar en cada cambio.
 * Lanza error si el usuario cancela el picker.
 */
export async function pickAndConnect() {
  const handle = await window.showSaveFilePicker({
    suggestedName: `gastnyahp-${new Date().toISOString().slice(0, 10)}.json`,
    types: [{ description: 'GastNyahp JSON', accept: { 'application/json': ['.json'] } }],
  })

  fileHandle = handle
  status = { fileName: handle.name, lastSaved: null, isSaving: false, error: null }
  notify()

  // Suscribir al store: cualquier cambio de estado → guardado con debounce
  if (storeUnsub) storeUnsub()
  storeUnsub = useStore.subscribe(_scheduleSave)

  // Guardado inicial inmediato
  await _doSave()
}

/** Desconecta el archivo: limpia handle, unsubscribe y timer. */
export function disconnect() {
  fileHandle = null
  if (storeUnsub) { storeUnsub(); storeUnsub = null }
  clearTimeout(saveTimer)
  saveTimer = null
  status = { fileName: null, lastSaved: null, isSaving: false, error: null }
  notify()
}

// ── Internals ──────────────────────────────────────────────────────────────────

function _scheduleSave() {
  clearTimeout(saveTimer)
  saveTimer = setTimeout(_doSave, 1500)
}

async function _doSave() {
  if (!fileHandle) return
  try {
    status = { ...status, isSaving: true, error: null }
    notify()

    const json = useStore.getState().exportData()
    const writable = await fileHandle.createWritable()
    await writable.write(json)
    await writable.close()

    status = { ...status, isSaving: false, lastSaved: new Date() }
    notify()
  } catch (e) {
    console.error('[autoSave]', e)
    status = { ...status, isSaving: false, error: e.message || 'Error al guardar' }
    notify()
  }
}
