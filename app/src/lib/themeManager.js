/**
 * themeManager.js — Gestión del tema de color (dark / cozy)
 *
 * El tema se aplica como atributo `data-theme` en <html>.
 * Se persiste en localStorage bajo la clave 'gastnyahp_theme'.
 *
 * Patrón de módulo igual que autoSave.js: estado + Set de listeners + notify().
 */

const LS_KEY  = 'gastnyahp_theme'
const DEFAULT = 'dark'

// ── Estado ─────────────────────────────────────────────────────────────────────
let current   = localStorage.getItem(LS_KEY) || DEFAULT
const listeners = new Set()

function notify() {
  listeners.forEach((fn) => fn(current))
}

function applyToDOM(theme) {
  document.documentElement.setAttribute('data-theme', theme)
}

// Aplicar inmediatamente al importar el módulo
applyToDOM(current)

// ── API pública ────────────────────────────────────────────────────────────────

export const getTheme = () => current

export function subscribeTheme(fn) {
  listeners.add(fn)
  return () => listeners.delete(fn)
}

export function setTheme(theme) {
  current = theme
  localStorage.setItem(LS_KEY, theme)
  applyToDOM(theme)
  notify()
}

export const toggleTheme = () =>
  setTheme(current === 'dark' ? 'cozy' : 'dark')
