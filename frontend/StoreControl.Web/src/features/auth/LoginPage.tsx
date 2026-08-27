import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { getProblemMessage } from '../../shared/api/problemDetails'
import { useAuth } from './useAuth'

type LoginForm = {
  email: string
  password: string
}

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [error, setError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({
    defaultValues: { email: '', password: '' },
  })

  if (user) {
    return <Navigate to="/" replace />
  }

  const destination =
    typeof location.state === 'object' &&
    location.state &&
    'from' in location.state &&
    typeof location.state.from === 'string'
      ? location.state.from
      : '/'

  return (
    <main className="login-page">
      <section className="login-intro">
        <div className="brand brand-login">
          <span className="brand-mark">SC</span>
          <div>
            <strong>StoreControl</strong>
            <small>Gestão empresarial de lojas</small>
          </div>
        </div>
        <div>
          <span className="eyebrow">Ambiente local de estudos</span>
          <h1>Entre no ambiente de gestão.</h1>
          <p>
            A autenticação é validada pela API, com senha protegida pelo ASP.NET Core Identity,
            access token curto e renovação por cookie HttpOnly.
          </p>
        </div>
        <ul className="security-notes">
          <li>Roles e permissões avaliadas no servidor</li>
          <li>Acesso associado às lojas autorizadas</li>
          <li>Sessões revogáveis e rastreáveis</li>
        </ul>
      </section>

      <section className="login-card">
        <span className="eyebrow">Acesso ao sistema</span>
        <h2>Boas-vindas</h2>
        <p>Use uma das contas demonstrativas criadas pelo migrador.</p>

        <form
          className="form-stack"
          onSubmit={handleSubmit(async (values) => {
            setError(null)
            try {
              await login(values.email, values.password)
              navigate(destination, { replace: true })
            } catch (requestError) {
              setError(getProblemMessage(requestError))
            }
          })}
        >
          <label>
            E-mail
            <input
              autoComplete="username"
              {...register('email', {
                required: 'Informe o e-mail.',
                pattern: { value: /^\S+@\S+\.\S+$/, message: 'Informe um e-mail válido.' },
              })}
            />
            {errors.email && <small className="field-error">{errors.email.message}</small>}
          </label>
          <label>
            Senha
            <input
              type="password"
              autoComplete="current-password"
              {...register('password', {
                required: 'Informe a senha.',
                minLength: { value: 12, message: 'A senha deve possuir ao menos 12 caracteres.' },
              })}
            />
            {errors.password && <small className="field-error">{errors.password.message}</small>}
          </label>
          {error && <div className="form-error">{error}</div>}
          <button className="button button-primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Validando…' : 'Entrar'}
          </button>
        </form>
      </section>
    </main>
  )
}
