import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const { auth } = vi.hoisted(() => ({
  auth: {
    getSession: vi.fn(),
    onAuthStateChange: vi.fn(),
    signInWithPassword: vi.fn(),
    signOut: vi.fn(),
  },
}))

vi.mock('./shared/supabase', () => ({
  supabase: { auth },
}))

describe('platform company administration', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    auth.getSession.mockResolvedValue({ data: { session: null }, error: null })
    auth.onAuthStateChange.mockReturnValue({
      data: { subscription: { unsubscribe: vi.fn() } },
    })
    auth.signInWithPassword.mockResolvedValue({
      data: { session: { access_token: 'test-access-token' } },
      error: null,
    })
    const newCompany = {
      id: 'company-test-id',
      legalName: 'Test Company',
      tradeName: null,
      countryCode: 'US',
      legalIdentifier: null,
      address: null,
      contactDetails: { email: null, phone: null },
      status: 'Suspended',
      subscriptionPlanReference: null,
      subscriptionStatus: null,
      subscriptionStartedAt: null,
      subscriptionEndsAt: null,
      createdAt: '2026-11-08T00:00:00Z',
      updatedAt: '2026-11-08T00:00:00Z',
    }
    vi.stubGlobal(
      'fetch',
      vi.fn().mockImplementation((_url: string, init?: RequestInit) =>
        new Response(JSON.stringify(init?.method === 'POST' ? newCompany : []), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    )
  })

  it('authenticates with Supabase and creates a suspended company through the API', async () => {
    render(<App />)

    fireEvent.change(await screen.findByLabelText('Correo electrónico'), {
      target: { value: 'owner@example.test' },
    })
    fireEvent.change(screen.getByLabelText('Contraseña'), {
      target: { value: 'not-a-business-database-password' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }))

    expect(await screen.findByRole('heading', { name: 'Administración de empresas' })).toBeTruthy()
    expect(auth.signInWithPassword).toHaveBeenCalledWith({
      email: 'owner@example.test',
      password: 'not-a-business-database-password',
    })
    await waitFor(() => {
      expect(fetch).toHaveBeenCalledWith(
        'http://localhost:5000/api/v1/platform/companies',
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'Bearer test-access-token',
          }),
        }),
      )
    })

    fireEvent.change(screen.getByLabelText('Razón social'), {
      target: { value: 'Test Company' },
    })
    fireEvent.change(screen.getByLabelText('País (ISO 3166-1 alpha-2)'), {
      target: { value: 'US' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Crear empresa' }))

    expect(await screen.findByText('Empresa creada en estado Suspendida.')).toBeTruthy()
    expect(screen.getByText('Test Company')).toBeTruthy()
    expect(fetch).toHaveBeenLastCalledWith(
      'http://localhost:5000/api/v1/platform/companies',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          legalName: 'Test Company',
          countryCode: 'US',
          tradeName: null,
          legalIdentifier: null,
          address: null,
          contactDetails: { email: null, phone: null },
          subscriptionPlanReference: null,
          subscriptionStatus: null,
          subscriptionStartedAt: null,
          subscriptionEndsAt: null,
        }),
      }),
    )
  })
})
