import { useAuth } from './useAuth'

export function ProfilePage() {
  const { user } = useAuth()
  if (!user) {
    return null
  }

  return (
    <section className="content-grid">
      <article className="panel">
        <span className="eyebrow">Identidade atual</span>
        <h2>{user.fullName}</h2>
        <p>{user.email}</p>
        <div className="tag-list">
          {user.roles.map((role) => (
            <span className="tag tag-strong" key={role}>
              {role}
            </span>
          ))}
        </div>
      </article>
      <article className="panel">
        <span className="eyebrow">Escopo por loja</span>
        <h2>Lojas disponíveis</h2>
        {user.stores.length === 0 ? (
          <p>O perfil global não depende de uma atribuição por loja.</p>
        ) : (
          <ul className="detail-list">
            {user.stores.map((store) => (
              <li key={store.storeId}>
                <strong>{store.storeName}</strong>
                <span>
                  {store.storeCode} · {store.role}
                </span>
              </li>
            ))}
          </ul>
        )}
      </article>
      <article className="panel panel-wide">
        <span className="eyebrow">Claims efetivas</span>
        <h2>Permissões desta sessão</h2>
        <div className="tag-list">
          {user.permissions.map((permission) => (
            <code className="tag" key={permission}>
              {permission}
            </code>
          ))}
        </div>
      </article>
    </section>
  )
}
