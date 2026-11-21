export type Company = {
  id: string
  legalName: string
  tradeName: string | null
  countryCode: string
  legalIdentifier: string | null
  address: string | null
  contactDetails: { email: string | null; phone: string | null }
  status: 'Active' | 'Suspended'
  subscriptionPlanReference: string | null
  subscriptionStatus: string | null
  subscriptionStartedAt: string | null
  subscriptionEndsAt: string | null
  createdAt: string
  updatedAt: string
}

export type CompanyInput = {
  legalName: string
  countryCode: string
  tradeName?: string | null
  legalIdentifier?: string | null
  address?: string | null
  contactDetails?: { email: string | null; phone: string | null } | null
  subscriptionPlanReference?: string | null
  subscriptionStatus?: string | null
  subscriptionStartedAt?: string | null
  subscriptionEndsAt?: string | null
}

export type InitialAdministratorInvitation = {
  id: string
  companyId: string
  inviteeEmail: string
  status: 'Pending'
  expiresAt: string
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000')
  .replace(/\/+$/, '')

export function getCompanies(accessToken: string): Promise<Company[]> {
  return request('/api/v1/platform/companies', accessToken)
}

export function createCompany(accessToken: string, company: CompanyInput): Promise<Company> {
  return request('/api/v1/platform/companies', accessToken, {
    method: 'POST',
    body: JSON.stringify(company),
  })
}

export function updateCompany(
  accessToken: string,
  companyId: string,
  company: CompanyInput,
): Promise<Company> {
  return request(`/api/v1/platform/companies/${companyId}`, accessToken, {
    method: 'PATCH',
    body: JSON.stringify(company),
  })
}

export function changeCompanyStatus(
  accessToken: string,
  companyId: string,
  status: Company['status'],
): Promise<Company> {
  return request(`/api/v1/platform/companies/${companyId}/status`, accessToken, {
    method: 'PUT',
    body: JSON.stringify({ status }),
  })
}

export function inviteInitialAdministrator(
  accessToken: string,
  companyId: string,
  email: string,
): Promise<InitialAdministratorInvitation> {
  return request(
    `/api/v1/platform/companies/${companyId}/initial-administrator-invitations`,
    accessToken,
    {
      method: 'POST',
      body: JSON.stringify({ email }),
    },
  )
}

async function request<T>(
  path: string,
  accessToken: string,
  init: RequestInit = {},
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${accessToken}`,
      ...(init.body ? { 'Content-Type': 'application/json' } : {}),
      ...init.headers,
    },
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as
      | { detail?: string; title?: string }
      | null
    throw new Error(problem?.detail || problem?.title || 'La solicitud no pudo completarse.')
  }

  return response.json() as Promise<T>
}
