import { Link } from 'react-router-dom'
import { useAuth } from './useAuth'

export function DashboardPage() {
  const { user } = useAuth()
  if (!user) {
    return null
  }

  return (
    <section className="dashboard-grid">
      <article className="panel welcome-panel">
        <span className="eyebrow">Sessão autenticada</span>
        <h2>Olá, {user.fullName}.</h2>
        <p>
          A fundação de identidade está ativa. As áreas exibidas no menu refletem as permissões
          desta sessão; a API repete todas as verificações antes de atender cada operação.
        </p>
        <Link className="button button-secondary" to="/profile">
          Ver identidade e claims
        </Link>
      </article>
      <article className="panel metric-card">
        <span className="metric-value">{user.roles.length}</span>
        <strong>perfis efetivos</strong>
        <small>globais e associados a lojas</small>
      </article>
      <article className="panel metric-card">
        <span className="metric-value">{user.stores.length}</span>
        <strong>lojas atribuídas</strong>
        <small>o administrador possui escopo global</small>
      </article>
      <article className="panel metric-card">
        <span className="metric-value">{user.permissions.length}</span>
        <strong>policies autorizadas</strong>
        <small>claims emitidas pela API</small>
      </article>
    </section>
  )
}
