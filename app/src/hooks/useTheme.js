import { useState, useEffect } from 'react'
import { getTheme, subscribeTheme, toggleTheme, setTheme } from '../lib/themeManager'

/**
 * Devuelve el tema activo y las acciones para cambiarlo.
 */
export function useTheme() {
  const [theme, setLocalTheme] = useState(getTheme)
  useEffect(() => subscribeTheme(setLocalTheme), [])
  return { theme, isDark: theme === 'dark', isCozy: theme === 'cozy', toggleTheme, setTheme }
}
