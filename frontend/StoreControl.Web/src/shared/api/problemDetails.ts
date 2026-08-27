import axios from 'axios'

export type ProblemDetails = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export function getProblemMessage(error: unknown) {
  if (!axios.isAxiosError<ProblemDetails>(error)) {
    return 'Ocorreu uma falha inesperada.'
  }

  const problem = error.response?.data
  const validationMessage = problem?.errors
    ? Object.values(problem.errors).flat().join(' ')
    : undefined
  return validationMessage ?? problem?.detail ?? problem?.title ?? error.message
}
