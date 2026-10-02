const API_URL = (import.meta.env.VITE_API_URL || 'http://localhost:5050/api').replace(/\/$/, '')

export function getToken() {
  return localStorage.getItem('roomly_token')
}

export function setToken(token) {
  if (token) localStorage.setItem('roomly_token', token)
  else localStorage.removeItem('roomly_token')
}

async function request(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) }
  const token = getToken()
  if (token) headers.Authorization = `Bearer ${token}`

  const response = await fetch(`${API_URL}${path}`, { ...options, headers })
  const contentType = response.headers.get('content-type') || ''
  const data = contentType.includes('application/json') ? await response.json() : null

  if (!response.ok) {
    if (response.status === 401) setToken(null)
    throw new Error(data?.message || 'Something went wrong. Please try again.')
  }
  return data
}

export const api = {
  login: (email, password) => request('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  me: () => request('/auth/me'),
  dashboard: (date) => request(`/dashboard?date=${encodeURIComponent(date)}`),
  createBooking: (booking) => request('/bookings', { method: 'POST', body: JSON.stringify(booking) }),
  cancelBooking: (id) => request(`/bookings/${id}`, { method: 'DELETE' }),
}
