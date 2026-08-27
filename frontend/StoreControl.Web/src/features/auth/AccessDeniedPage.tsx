import { Link } from 'react-router-dom'

export function AccessDeniedPage() {
  return (
    <section className="empty-state panel">
      <span className="empty-code">403</span>
      <h2>Acesso não autorizado</h2>
      <p>Seu perfil não possui a permissão exigida para esta área.</p>
      <Link className="button button-secondary" to="/">
        Voltar ao início
      </Link>
    </section>
  )
}
