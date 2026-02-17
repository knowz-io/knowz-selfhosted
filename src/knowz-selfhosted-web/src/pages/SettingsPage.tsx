import { useState, useEffect } from 'react'
import { api } from '../lib/api-client'
import { Save, Eye, EyeOff, CheckCircle, XCircle, Loader2 } from 'lucide-react'

export default function SettingsPage() {
  const [apiUrl, setApiUrl] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [showKey, setShowKey] = useState(false)
  const [saved, setSaved] = useState(false)
  const [testStatus, setTestStatus] = useState<'idle' | 'testing' | 'success' | 'error'>('idle')
  const [testError, setTestError] = useState('')

  useEffect(() => {
    setApiUrl(localStorage.getItem('apiUrl') || '')
    setApiKey(localStorage.getItem('apiKey') || '')
  }, [])

  const handleSave = () => {
    if (apiUrl) {
      localStorage.setItem('apiUrl', apiUrl)
    } else {
      localStorage.removeItem('apiUrl')
    }
    if (apiKey) {
      localStorage.setItem('apiKey', apiKey)
    } else {
      localStorage.removeItem('apiKey')
    }
    setSaved(true)
    setTimeout(() => setSaved(false), 2000)
  }

  const handleTest = async () => {
    setTestStatus('testing')
    setTestError('')
    try {
      await api.testConnection()
      setTestStatus('success')
    } catch (err) {
      setTestStatus('error')
      setTestError(err instanceof Error ? err.message : 'Connection failed')
    }
  }

  return (
    <div className="space-y-6 max-w-lg">
      <h1 className="text-2xl font-bold">Settings</h1>

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium mb-1">API URL</label>
          <input
            type="text"
            value={apiUrl}
            onChange={(e) => setApiUrl(e.target.value)}
            placeholder={window.location.origin}
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
          />
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
            Leave empty to use the current origin ({window.location.origin})
          </p>
        </div>

        <div>
          <label className="block text-sm font-medium mb-1">API Key</label>
          <div className="relative">
            <input
              type={showKey ? 'text' : 'password'}
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder="Enter API key..."
              className="w-full px-3 py-2 pr-10 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
            />
            <button
              type="button"
              onClick={() => setShowKey(!showKey)}
              aria-label={showKey ? "Hide API key" : "Show API key"}
              className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
            >
              {showKey ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
            Required if the API has authentication enabled. Sent as X-Api-Key header.
          </p>
        </div>

        <div className="flex gap-3">
          <button
            onClick={handleSave}
            className="inline-flex items-center gap-2 px-4 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium"
          >
            <Save size={16} /> {saved ? 'Saved!' : 'Save'}
          </button>
          <button
            onClick={handleTest}
            disabled={testStatus === 'testing'}
            className="inline-flex items-center gap-2 px-4 py-2 border border-gray-300 dark:border-gray-700 rounded-md text-sm font-medium disabled:opacity-50"
          >
            {testStatus === 'testing' && <Loader2 size={16} className="animate-spin" />}
            {testStatus === 'success' && <CheckCircle size={16} className="text-green-600" />}
            {testStatus === 'error' && <XCircle size={16} className="text-red-600" />}
            {testStatus === 'idle' && null}
            Test Connection
          </button>
        </div>

        {testStatus === 'success' && (
          <p className="text-green-600 dark:text-green-400 text-sm">
            Connection successful!
          </p>
        )}
        {testStatus === 'error' && (
          <p className="text-red-600 dark:text-red-400 text-sm">
            {testError}
          </p>
        )}
      </div>
    </div>
  )
}
