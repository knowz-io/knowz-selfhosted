import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams, Link } from 'react-router-dom'
import { Loader2, AlertCircle } from 'lucide-react'
import { useAuth } from '../lib/auth'
import { api, ApiError } from '../lib/api-client'

export default function SSOCallbackPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const { loginWithToken } = useAuth()
  const [error, setError] = useState<string | null>(null)
  const [processing, setProcessing] = useState(true)

  useEffect(() => {
    const handleCallback = async () => {
      // Check for error from Entra
      const errorParam = searchParams.get('error')
      const errorDescription = searchParams.get('error_description')
      if (errorParam) {
        const message = errorDescription
          ? decodeURIComponent(errorDescription.replace(/\+/g, ' '))
          : errorParam === 'access_denied'
            ? 'Authentication was cancelled.'
            : `Authentication error: ${errorParam}`
        setError(message)
        setProcessing(false)
        return
      }

      const code = searchParams.get('code')
      const state = searchParams.get('state')

      if (!code || !state) {
        setError('Missing authorization parameters. Please try signing in again.')
        setProcessing(false)
        return
      }

      try {
        const response = await api.ssoCallback(code, state)
        if (response.success && response.data.token) {
          await loginWithToken(response.data.token)
          navigate('/', { replace: true })
        } else {
          setError('Authentication failed. Please try again.')
          setProcessing(false)
        }
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message)
        } else {
          setError('An unexpected error occurred during sign-in.')
        }
        setProcessing(false)
      }
    }

    handleCallback()
  }, [searchParams, loginWithToken, navigate])

  if (processing) {
    return (
      <div className="flex flex-col items-center justify-center min-h-screen bg-gray-50 dark:bg-gray-950 px-4">
        <Loader2 size={32} className="animate-spin text-gray-400 dark:text-gray-500 mb-4" />
        <p className="text-sm text-gray-600 dark:text-gray-400">Completing sign-in...</p>
      </div>
    )
  }

  return (
    <div className="flex items-center justify-center min-h-screen bg-gray-50 dark:bg-gray-950 px-4">
      <div className="w-full max-w-sm">
        <div className="bg-white dark:bg-gray-900 border border-red-200 dark:border-red-800 rounded-xl shadow-sm p-6">
          <div className="flex items-start gap-3">
            <AlertCircle size={20} className="text-red-600 dark:text-red-400 mt-0.5 shrink-0" />
            <div>
              <h2 className="text-sm font-semibold text-red-800 dark:text-red-300 mb-1">
                Sign-in failed
              </h2>
              <p className="text-sm text-red-600 dark:text-red-400 mb-4">{error}</p>
              <Link
                to="/login"
                className="inline-flex px-4 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium hover:bg-gray-800 dark:hover:bg-gray-100 transition-colors"
              >
                Back to login
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
