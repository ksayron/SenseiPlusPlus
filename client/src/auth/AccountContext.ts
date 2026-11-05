import { createContext, useContext } from 'react'

export interface Account { id: string; email: string; displayName: string; uiLocale: string; answerLanguage: string; timeZone: string }
export const AccountContext = createContext<Account | null>(null)
export function useAccount() {
  const value = useContext(AccountContext)
  if (!value) throw new Error('Account context is required')
  return value
}
