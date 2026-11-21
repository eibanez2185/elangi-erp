import { useEffect, useState, type FormEvent } from 'react'
import type { Session } from '@supabase/supabase-js'
import {
  changeCompanyStatus,
  createCompany,
  getCompanies,
  inviteInitialAdministrator,
  updateCompany,
  type Company,
  type CompanyInput,
} from './features/platform-companies/platformCompaniesApi'
import { supabase } from './shared/supabase'
import './App.css'

const emptyCompany: CompanyInput = {
  legalName: '',
  countryCode: '',
  tradeName: '',
  legalIdentifier: '',
  address: '',
  contactDetails: { email: '', phone: '' },
  subscriptionPlanReference: '',
  subscriptionStatus: '',
  subscriptionStartedAt: '',
  subscriptionEndsAt: '',
}

function App() {
  const [session, setSession] = useState<Session | null>(null)
  const [authReady, setAuthReady] = useState(supabase === null)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [companies, setCompanies] = useState<Company[]>([])
  const [selectedCompany, setSelectedCompany] = useState<Company | null>(null)
  const [companyForm, setCompanyForm] = useState<CompanyInput>(emptyCompany)
  const [inviteEmail, setInviteEmail] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!supabase) {
      return
    }

    let active = true
    void supabase.auth.getSession().then(({ data, error: sessionError }) => {
      if (!active) return
      if (sessionError) setError(sessionError.message)
      setSession(data.session)
      setAuthReady(true)
    })

    const { data } = supabase.auth.onAuthStateChange((_event, currentSession) => {
      setSession(currentSession)
      if (!currentSession) {
        setCompanies([])
        setSelectedCompany(null)
      }
    })

    return () => {
      active = false
      data.subscription.unsubscribe()
    }
  }, [])

  useEffect(() => {
    if (!session) {
      return
    }

    let active = true
    void getCompanies(session.access_token)
      .then((items) => {
        if (active) setCompanies(items)
      })
      .catch((cause: unknown) => {
        if (active) setError(errorMessage(cause))
      })
      .finally(() => {
        if (active) setBusy(false)
      })

    return () => {
      active = false
    }
  }, [session])

  async function handleSignIn(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!supabase) return
    setBusy(true)
    setError('')
    try {
      const { data, error: signInError } = await supabase.auth.signInWithPassword({
        email,
        password,
      })
      if (signInError) throw signInError
      setSession(data.session)
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setBusy(false)
    }
  }

  async function handleSignOut() {
    if (!supabase) return
    setError('')
    const { error: signOutError } = await supabase.auth.signOut()
    if (signOutError) {
      setError(signOutError.message)
      return
    }
    setSession(null)
    setCompanies([])
    setSelectedCompany(null)
    setNotice('')
  }

  async function handleCompanySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!session) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const input = normalizeCompanyInput(companyForm)
      const saved = selectedCompany
        ? await updateCompany(session.access_token, selectedCompany.id, input)
        : await createCompany(session.access_token, input)
      setCompanies((current) => {
        const remaining = current.filter((company) => company.id !== saved.id)
        return [saved, ...remaining]
      })
      setSelectedCompany(saved)
      setCompanyForm(toCompanyInput(saved))
      setNotice(selectedCompany ? 'Empresa actualizada.' : 'Empresa creada en estado Suspendida.')
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setBusy(false)
    }
  }

  async function handleSelectCompany(company: Company) {
    setSelectedCompany(company)
    setCompanyForm(toCompanyInput(company))
    setInviteEmail('')
    setNotice('')
    setError('')
  }

  async function handleStatusChange(company: Company) {
    if (!session) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const status = company.status === 'Active' ? 'Suspended' : 'Active'
      const updated = await changeCompanyStatus(session.access_token, company.id, status)
      setCompanies((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      )
      if (selectedCompany?.id === updated.id) {
        setSelectedCompany(updated)
        setCompanyForm(toCompanyInput(updated))
      }
      setNotice(`Empresa ${status === 'Active' ? 'activada' : 'suspendida'}.`)
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setBusy(false)
    }
  }

  async function handleInvite(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!session || !selectedCompany) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const invitation = await inviteInitialAdministrator(
        session.access_token,
        selectedCompany.id,
        inviteEmail,
      )
      setNotice(`Invitación enviada a ${invitation.inviteeEmail}. El enlace llegará por correo.`)
      setInviteEmail('')
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setBusy(false)
    }
  }

  if (!authReady) {
    return <main className="shell"><p role="status">Comprobando sesión...</p></main>
  }

  if (!supabase) {
    return (
      <main className="shell">
        <section className="card">
          <h1>ELANGI ERP</h1>
          <p>Configura VITE_SUPABASE_URL y VITE_SUPABASE_ANON_KEY para iniciar sesión.</p>
        </section>
      </main>
    )
  }

  if (!session) {
    return (
      <main className="shell auth-shell">
        <section className="card auth-card">
          <p className="eyebrow">ELANGI ERP</p>
          <h1>Administración de plataforma</h1>
          <p>Inicia sesión con tu cuenta de Platform Owner.</p>
          <form onSubmit={handleSignIn} className="stack">
            <label>
              Correo electrónico
              <input
                autoComplete="username"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
              />
            </label>
            <label>
              Contraseña
              <input
                autoComplete="current-password"
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
            </label>
            {error && <p className="feedback error" role="alert">{error}</p>}
            <button disabled={busy} type="submit">
              {busy ? 'Ingresando...' : 'Iniciar sesión'}
            </button>
          </form>
        </section>
      </main>
    )
  }

  return (
    <main className="shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">ELANGI ERP</p>
          <h1>Administración de empresas</h1>
        </div>
        <button className="secondary" onClick={handleSignOut} type="button">Cerrar sesión</button>
      </header>

      {error && <p className="feedback error" role="alert">{error}</p>}
      {notice && <p className="feedback success" role="status">{notice}</p>}

      <div className="dashboard">
        <section className="card company-list">
          <div className="section-heading">
            <div>
              <h2>Empresas</h2>
              <p>Gestión global sin acceso a los datos operativos.</p>
            </div>
            <button
              className="secondary"
              onClick={() => {
                setSelectedCompany(null)
                setCompanyForm(emptyCompany)
                setInviteEmail('')
                setNotice('')
                setError('')
              }}
              type="button"
            >
              Nueva empresa
            </button>
          </div>
          {busy && companies.length === 0 ? (
            <p role="status">Cargando empresas...</p>
          ) : companies.length === 0 ? (
            <p className="empty">Todavía no hay empresas registradas.</p>
          ) : (
            <ul className="company-items">
              {companies.map((company) => (
                <li className={selectedCompany?.id === company.id ? 'selected' : ''} key={company.id}>
                  <button
                    className="company-select"
                    onClick={() => void handleSelectCompany(company)}
                    type="button"
                  >
                    <span>{company.tradeName || company.legalName}</span>
                    <span className={`status status-${company.status.toLowerCase()}`}>
                      {company.status === 'Active' ? 'Activa' : 'Suspendida'}
                    </span>
                    <span className="company-meta">
                      {company.subscriptionPlanReference || 'Sin plan'}
                      {company.subscriptionStatus ? ` · ${company.subscriptionStatus}` : ''}
                    </span>
                  </button>
                  <button
                    className="status-action"
                    disabled={busy}
                    onClick={() => void handleStatusChange(company)}
                    type="button"
                  >
                    {company.status === 'Active' ? 'Suspender' : 'Activar'}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="card company-editor">
          <div className="section-heading">
            <div>
              <h2>{selectedCompany ? 'Editar empresa' : 'Crear empresa'}</h2>
              <p>Los nuevos registros comienzan suspendidos.</p>
            </div>
          </div>
          <form className="form-grid" onSubmit={handleCompanySubmit}>
            <label>
              Razón social
              <input
                value={companyForm.legalName}
                onChange={(event) => setCompanyForm({ ...companyForm, legalName: event.target.value })}
                required
              />
            </label>
            <label>
              País (ISO 3166-1 alpha-2)
              <input
                maxLength={2}
                value={companyForm.countryCode}
                onChange={(event) => setCompanyForm({ ...companyForm, countryCode: event.target.value.toUpperCase() })}
                required
              />
            </label>
            <label>
              Nombre comercial
              <input
                value={companyForm.tradeName ?? ''}
                onChange={(event) => setCompanyForm({ ...companyForm, tradeName: event.target.value })}
              />
            </label>
            <label>
              Identificación legal
              <input
                value={companyForm.legalIdentifier ?? ''}
                onChange={(event) => setCompanyForm({ ...companyForm, legalIdentifier: event.target.value })}
              />
            </label>
            <label className="wide">
              Dirección
              <input
                value={companyForm.address ?? ''}
                onChange={(event) => setCompanyForm({ ...companyForm, address: event.target.value })}
              />
            </label>
            <label>
              Correo de contacto
              <input
                type="email"
                value={companyForm.contactDetails?.email ?? ''}
                onChange={(event) =>
                  setCompanyForm({
                    ...companyForm,
                    contactDetails: {
                      phone: companyForm.contactDetails?.phone ?? null,
                      email: event.target.value,
                    },
                  })
                }
              />
            </label>
            <label>
              Teléfono de contacto
              <input
                value={companyForm.contactDetails?.phone ?? ''}
                onChange={(event) =>
                  setCompanyForm({
                    ...companyForm,
                    contactDetails: {
                      email: companyForm.contactDetails?.email ?? null,
                      phone: event.target.value,
                    },
                  })
                }
              />
            </label>
            <label>
              Referencia de plan (metadato)
              <input
                value={companyForm.subscriptionPlanReference ?? ''}
                onChange={(event) => setCompanyForm({ ...companyForm, subscriptionPlanReference: event.target.value })}
              />
            </label>
            <label>
              Estado de suscripción (metadato)
              <input
                value={companyForm.subscriptionStatus ?? ''}
                onChange={(event) => setCompanyForm({ ...companyForm, subscriptionStatus: event.target.value })}
              />
            </label>
            <label>
              Inicio de suscripción
              <input
                type="datetime-local"
                value={companyForm.subscriptionStartedAt ?? ''}
                onChange={(event) => setCompanyForm({ ...companyForm, subscriptionStartedAt: event.target.value })}
              />
            </label>
            <label>
              Fin de suscripción
              <input
                type="datetime-local"
                value={companyForm.subscriptionEndsAt ?? ''}
                onChange={(event) => setCompanyForm({ ...companyForm, subscriptionEndsAt: event.target.value })}
              />
            </label>
            <button className="wide" disabled={busy} type="submit">
              {busy ? 'Guardando...' : selectedCompany ? 'Guardar cambios' : 'Crear empresa'}
            </button>
          </form>

          {selectedCompany && (
            <form className="invite-form" onSubmit={handleInvite}>
              <h3>Administrador inicial</h3>
              <label>
                Correo del administrador
                <input
                  autoComplete="email"
                  type="email"
                  value={inviteEmail}
                  onChange={(event) => setInviteEmail(event.target.value)}
                  required
                />
              </label>
              <button className="secondary" disabled={busy} type="submit">
                Enviar invitación por correo
              </button>
            </form>
          )}
        </section>
      </div>
    </main>
  )
}

function normalizeCompanyInput(input: CompanyInput): CompanyInput {
  return {
    ...input,
    legalName: input.legalName.trim(),
    countryCode: input.countryCode.trim().toUpperCase(),
    tradeName: emptyToNull(input.tradeName),
    legalIdentifier: emptyToNull(input.legalIdentifier),
    address: emptyToNull(input.address),
    contactDetails: {
      email: emptyToNull(input.contactDetails?.email),
      phone: emptyToNull(input.contactDetails?.phone),
    },
    subscriptionPlanReference: emptyToNull(input.subscriptionPlanReference),
    subscriptionStatus: emptyToNull(input.subscriptionStatus),
    subscriptionStartedAt: localDateTimeToIso(input.subscriptionStartedAt),
    subscriptionEndsAt: localDateTimeToIso(input.subscriptionEndsAt),
  }
}

function toCompanyInput(company: Company): CompanyInput {
  return {
    legalName: company.legalName,
    countryCode: company.countryCode,
    tradeName: company.tradeName,
    legalIdentifier: company.legalIdentifier,
    address: company.address,
    contactDetails: company.contactDetails,
    subscriptionPlanReference: company.subscriptionPlanReference,
    subscriptionStatus: company.subscriptionStatus,
    subscriptionStartedAt: isoToLocalDateTime(company.subscriptionStartedAt),
    subscriptionEndsAt: isoToLocalDateTime(company.subscriptionEndsAt),
  }
}

function emptyToNull(value: string | null | undefined): string | null {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

function localDateTimeToIso(value: string | null | undefined): string | null {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.valueOf())) {
    throw new Error('Las fechas de suscripción deben ser válidas.')
  }
  return date.toISOString()
}

function isoToLocalDateTime(value: string | null): string {
  if (!value) return ''
  const date = new Date(value)
  const pad = (part: number) => String(part).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function errorMessage(cause: unknown): string {
  return cause instanceof Error ? cause.message : 'Ocurrió un error inesperado.'
}

export default App
