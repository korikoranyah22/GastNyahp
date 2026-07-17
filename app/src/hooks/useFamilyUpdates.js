import { useEffect } from 'react'
import { HubConnectionBuilder } from '@microsoft/signalr'
import useStore from '../store/useStore'
import { getToken } from '../lib/api'

const BURST_WINDOW_MS = 200

function affectedReloads(path, state) {
  if (path.startsWith('/mcp')) return [state.loadAll]
  if (path.startsWith('/api/banks')) return [state.reloadBanks]
  if (path.startsWith('/api/cards')) return [state.reloadCards]
  if (path.startsWith('/api/installments')) return [state.reloadInstallments]
  if (path.startsWith('/api/loans')) return [state.reloadLoans]
  if (path.startsWith('/api/services')) return [state.reloadServices]
  if (path.startsWith('/api/reserves')) return [state.reloadReserves]
  if (path.startsWith('/api/people')) return [state.reloadPeople]
  if (path.startsWith('/api/planning/budget')) return [state.reloadBudgets]
  if (path.startsWith('/api/planning/income')) return [state.reloadIncome]
  if (path.startsWith('/api/planning/copy-month')) return [state.reloadBudgets, state.reloadReserves]
  if (path.startsWith('/api/drafts')) return [state.reloadDrafts, () => state.reloadExpensesMonth(state.currentMonth)]
  if (path.startsWith('/api/expenses') || path.startsWith('/api/tickets')) {
    return [() => state.reloadExpensesMonth(state.currentMonth)]
  }
  if (path.startsWith('/api/families')) return [state.refreshFamily]
  return []
}

/** Mantiene sincronizados los slices afectados por escrituras de otros clientes de la misma familia. */
export function useFamilyUpdates(enabled) {
  useEffect(() => {
    if (!enabled || !getToken()) return undefined

    const pending = new Set()
    let timer
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/updates', { accessTokenFactory: getToken })
      .withAutomaticReconnect()
      .build()

    const flush = () => {
      timer = undefined
      const reloads = [...pending]
      pending.clear()
      void Promise.allSettled(reloads.map((reload) => reload()))
    }

    connection.on('dataChanged', ({ path = '' }) => {
      const state = useStore.getState()
      affectedReloads(path, state).forEach((reload) => pending.add(reload))
      if (!timer && pending.size) timer = window.setTimeout(flush, BURST_WINDOW_MS)
    })

    void connection.start().catch(() => {})

    return () => {
      if (timer) window.clearTimeout(timer)
      pending.clear()
      void connection.stop()
    }
  }, [enabled])
}
