import React, { useEffect, useMemo, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { api, getToken, setToken } from './lib/api'
import './styles.css'

const formatDate = (value, options = { weekday: 'long', month: 'long', day: 'numeric' }) =>
  new Intl.DateTimeFormat('en-US', options).format(new Date(`${value}T00:00:00`))

const formatTime = (value) =>
  new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' }).format(new Date(value))

const hourPosition = (value) => {
  const date = new Date(value)
  return Math.max(8, Math.min(18, date.getHours() + date.getMinutes() / 60))
}

function Icon({ name, size = 18 }) {
  const paths = {
    calendar: <><rect x="3" y="4" width="18" height="17" rx="3" /><path d="M16 2v4M8 2v4M3 10h18" /></>,
    grid: <><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></>,
    building: <><path d="M4 21V5a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v16M2 21h20M8 7h2M14 7h2M8 11h2M14 11h2M8 15h2M14 15h2" /></>,
    plus: <><path d="M12 5v14M5 12h14" /></>,
    arrow: <><path d="M5 12h14M13 6l6 6-6 6" /></>,
    chevron: <path d="m9 18 6-6-6-6" />,
    logout: <><path d="M10 17l5-5-5-5M15 12H3M21 19V5a2 2 0 0 0-2-2h-5" /></>,
    users: <><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" /></>,
    clock: <><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></>,
    close: <><path d="M6 6l12 12M18 6 6 18" /></>,
    check: <path d="m5 12 4 4L19 6" />,
    monitor: <><rect x="3" y="4" width="18" height="13" rx="2" /><path d="M8 21h8M12 17v4" /></>,
    search: <><circle cx="11" cy="11" r="7" /><path d="m20 20-4-4" /></>,
  }
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>
}

function Login({ onLogin }) {
  const [email, setEmail] = useState('admin@meetingroom.local')
  const [password, setPassword] = useState('Admin123!')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  async function submit(event) {
    event.preventDefault()
    setError('')
    setLoading(true)
    try {
      const result = await api.login(email, password)
      setToken(result.token)
      onLogin(result.user)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  return <main className="login-shell">
    <section className="login-art">
      <div className="brand brand-light"><span className="brand-mark"><Icon name="building" size={19} /></span> roomly</div>
      <div className="art-copy">
        <p className="eyebrow">WORKSPACE, SIMPLIFIED</p>
        <h1>Make space for<br /><em>good work.</em></h1>
        <p>Find the right room, bring your team together, and keep every meeting moving.</p>
      </div>
      <div className="art-shape shape-one" /><div className="art-shape shape-two" />
      <div className="art-footer"><span>ROOMLY HQ</span><span>01 / 04</span></div>
    </section>
    <section className="login-panel">
      <div className="login-content">
        <div className="brand brand-dark"><span className="brand-mark"><Icon name="building" size={19} /></span> roomly</div>
        <div className="login-heading"><p className="eyebrow">WELCOME BACK</p><h2>Sign in to your workspace</h2><p>Book a room and see what is happening today.</p></div>
        <form onSubmit={submit} className="login-form">
          <label>Email address<input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="email" required /></label>
          <label>Password<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required /></label>
          {error && <div className="form-error">{error}</div>}
          <button className="button button-primary button-wide" disabled={loading}>{loading ? 'Signing in…' : 'Sign in'} <Icon name="arrow" size={17} /></button>
        </form>
        <div className="demo-hint"><span className="hint-dot" /><div><strong>Demo access</strong><p>admin@meetingroom.local · Admin123!</p></div></div>
        <p className="login-footnote">By continuing, you agree to the workspace terms and privacy policy.</p>
      </div>
    </section>
  </main>
}

function StatCard({ label, value, detail, icon, accent }) {
  return <div className={`stat-card ${accent || ''}`}><div className="stat-icon"><Icon name={icon} size={19} /></div><div><p>{label}</p><strong>{value}</strong><span>{detail}</span></div></div>
}

function BookingChip({ booking }) {
  const left = ((hourPosition(booking.startTime) - 8) / 10) * 100
  const width = ((hourPosition(booking.endTime) - hourPosition(booking.startTime)) / 10) * 100
  return <div className={`booking-chip ${booking.status === 'Cancelled' ? 'is-cancelled' : ''}`} style={{ left: `${left}%`, width: `${Math.max(width, 8)}%` }} title={`${booking.title} · ${formatTime(booking.startTime)} - ${formatTime(booking.endTime)}`}><strong>{booking.title}</strong><span>{formatTime(booking.startTime)} - {formatTime(booking.endTime)}</span></div>
}

function Schedule({ rooms, bookings }) {
  const hours = Array.from({ length: 11 }, (_, i) => 8 + i)
  return <div className="schedule-card">
    <div className="schedule-head"><div><p className="eyebrow">ROOM OCCUPANCY</p><h3>Today's schedule</h3></div><span className="schedule-meta"><span className="legend-dot" /> Live workspace view</span></div>
    <div className="schedule-scroll"><div className="schedule-grid">
      <div className="schedule-corner" />
      <div className="time-labels">{hours.map((hour) => <span key={hour}>{hour === 12 ? '12 PM' : hour > 12 ? `${hour - 12} PM` : `${hour} AM`}</span>)}</div>
      {rooms.map((room) => <div className="schedule-row" key={room.id}>
        <div className="room-label"><span className={`room-dot ${room.status === 'Maintenance' ? 'muted' : ''}`} /><div><strong>{room.name}</strong><span>{room.capacity} seats · Floor {room.floor}</span></div></div>
        <div className="room-track">{hours.slice(0, -1).map((hour) => <i key={hour} />)}{bookings.filter((booking) => booking.roomId === room.id).map((booking) => <BookingChip key={booking.id} booking={booking} />)}</div>
      </div>)}
    </div></div>
  </div>
}

function BookingModal({ rooms, date, onClose, onCreated }) {
  const availableRooms = rooms.filter((room) => room.status === 'Available')
  const [form, setForm] = useState({ roomId: availableRooms[0]?.id || '', title: '', startTime: '09:00', endTime: '10:00', attendees: 2, notes: '' })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const update = (key, value) => setForm((current) => ({ ...current, [key]: value }))

  async function submit(event) {
    event.preventDefault(); setError(''); setLoading(true)
    try {
      const booking = await api.createBooking({ ...form, attendees: Number(form.attendees), startTime: new Date(`${date}T${form.startTime}:00`).toISOString(), endTime: new Date(`${date}T${form.endTime}:00`).toISOString() })
      onCreated(booking)
    } catch (err) { setError(err.message) } finally { setLoading(false) }
  }

  return <div className="modal-backdrop" onMouseDown={(e) => e.target === e.currentTarget && onClose()}><div className="modal-card" role="dialog" aria-modal="true">
    <div className="modal-head"><div><p className="eyebrow">NEW RESERVATION</p><h2>Book a meeting room</h2><p>{formatDate(date)}</p></div><button className="icon-button" onClick={onClose} aria-label="Close"><Icon name="close" /></button></div>
    <form className="modal-form" onSubmit={submit}>
      <label>Meeting title<input value={form.title} onChange={(e) => update('title', e.target.value)} placeholder="e.g. Product planning" required /></label>
      <div className="form-row"><label>Room<select value={form.roomId} onChange={(e) => update('roomId', e.target.value)}>{availableRooms.map((room) => <option key={room.id} value={room.id}>{room.name} · {room.capacity} seats</option>)}</select></label><label>Attendees<input type="number" min="1" value={form.attendees} onChange={(e) => update('attendees', e.target.value)} required /></label></div>
      <div className="form-row"><label>Starts<select value={form.startTime} onChange={(e) => update('startTime', e.target.value)}>{timeOptions().map((time) => <option key={time}>{time}</option>)}</select></label><label>Ends<select value={form.endTime} onChange={(e) => update('endTime', e.target.value)}>{timeOptions().map((time) => <option key={time}>{time}</option>)}</select></label></div>
      <label>Notes <span className="optional">Optional</span><textarea rows="3" value={form.notes} onChange={(e) => update('notes', e.target.value)} placeholder="Add a note for your team…" /></label>
      {error && <div className="form-error">{error}</div>}
      <div className="modal-actions"><button type="button" className="button button-quiet" onClick={onClose}>Cancel</button><button className="button button-primary" disabled={loading || !availableRooms.length}>{loading ? 'Booking…' : 'Confirm booking'} <Icon name="check" size={17} /></button></div>
    </form>
  </div></div>
}

function timeOptions() { return Array.from({ length: 19 }, (_, i) => { const total = 8 * 60 + i * 30; return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}` }) }

function Dashboard({ user, onLogout }) {
  const today = new Date().toISOString().slice(0, 10)
  const [date, setDate] = useState(today)
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [filter, setFilter] = useState('All rooms')
  const [showModal, setShowModal] = useState(false)
  const [toast, setToast] = useState('')

  async function load() { setLoading(true); setError(''); try { setData(await api.dashboard(date)) } catch (err) { setError(err.message); if (err.message.includes('authorized')) onLogout() } finally { setLoading(false) } }
  useEffect(() => { load() }, [date])
  useEffect(() => { if (!toast) return; const timer = setTimeout(() => setToast(''), 3000); return () => clearTimeout(timer) }, [toast])

  const visibleRooms = useMemo(() => data?.rooms?.filter((room) => filter === 'All rooms' || room.status === filter) || [], [data, filter])
  const upcoming = useMemo(() => (data?.bookings || []).filter((booking) => booking.status !== 'Cancelled').sort((a, b) => new Date(a.startTime) - new Date(b.startTime)), [data])

  async function cancelBooking(id) { try { await api.cancelBooking(id); setToast('Booking cancelled'); await load() } catch (err) { setToast(err.message) } }
  function moveDate(days) { const next = new Date(`${date}T00:00:00`); next.setDate(next.getDate() + days); setDate(next.toISOString().slice(0, 10)) }

  return <div className="app-shell">
    <aside className="sidebar"><div className="brand brand-dark"><span className="brand-mark"><Icon name="building" size={19} /></span> roomly</div><nav><p className="nav-label">WORKSPACE</p><button className="nav-item active"><Icon name="grid" /> Overview</button><button className="nav-item" onClick={() => setToast('Room directory is coming next.') }><Icon name="building" /> Room directory</button><button className="nav-item" onClick={() => setToast('Team management is available to admins.') }><Icon name="users" /> Team</button></nav><div className="sidebar-bottom"><div className="help-card"><span>Need a hand?</span><strong>View booking guide <Icon name="arrow" size={14} /></strong></div><button className="profile-card" onClick={onLogout}><span className="avatar">{user.name.split(' ').map((part) => part[0]).join('').slice(0, 2)}</span><span className="profile-copy"><strong>{user.name}</strong><small>{user.role}</small></span><Icon name="logout" size={16} /></button></div></aside>
    <main className="main-content"><header className="topbar"><div><p className="eyebrow">{formatDate(date, { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' }).toUpperCase()}</p><h1>Good morning, {user.name.split(' ')[0]} <span>✦</span></h1></div><div className="top-actions"><button className="date-picker"><Icon name="calendar" size={17} /><span>{formatDate(date, { month: 'short', day: 'numeric', year: 'numeric' })}</span><input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></button><button className="button button-primary" onClick={() => setShowModal(true)}><Icon name="plus" size={17} /> Book a room</button></div></header>
      <section className="stats-grid"><StatCard label="Available rooms" value={data?.stats.availableRooms ?? '—'} detail={`of ${data?.stats.totalRooms ?? '—'} total rooms`} icon="building" accent="accent-purple" /><StatCard label="Bookings today" value={data?.stats.bookingsToday ?? '—'} detail="across the workspace" icon="calendar" accent="accent-orange" /><StatCard label="My bookings" value={data?.stats.myBookingsToday ?? '—'} detail="scheduled for today" icon="clock" accent="accent-green" /></section>
      {error && <div className="page-error"><strong>Could not load workspace</strong><span>{error}</span><button onClick={load}>Try again</button></div>}
      {loading && !data ? <div className="loading-state"><div className="spinner" /> Loading your workspace…</div> : data && <>
        <section className="section-heading"><div><p className="eyebrow">FIND YOUR SPACE</p><h2>Rooms at a glance</h2></div><div className="filter-tabs">{['All rooms', 'Available', 'Maintenance'].map((item) => <button key={item} className={filter === item ? 'selected' : ''} onClick={() => setFilter(item)}>{item}<span>{item === 'All rooms' ? data.rooms.length : data.rooms.filter((room) => room.status === item).length}</span></button>)}</div></section>
        <section className="room-cards">{visibleRooms.map((room) => { const roomBookings = data.bookings.filter((booking) => booking.roomId === room.id && booking.status !== 'Cancelled'); return <div className="room-card" key={room.id}><div className={`room-card-top ${room.status === 'Maintenance' ? 'maintenance' : ''}`}><span className="floor-label">FLOOR {room.floor}</span><span className={`availability ${room.status.toLowerCase()}`}><i />{room.status}</span></div><div className="room-card-body"><h3>{room.name}</h3><p className="room-capacity"><Icon name="users" size={15} /> Up to {room.capacity} people</p><div className="amenities">{room.amenities.slice(0, 2).map((amenity) => <span key={amenity}>{amenity}</span>)}{room.amenities.length > 2 && <span>+{room.amenities.length - 2}</span>}</div></div><div className="room-card-foot"><span><span className="mini-avatar">{roomBookings.length ? roomBookings.length : '—'}</span> {roomBookings.length ? 'bookings today' : 'No bookings yet'}</span><button disabled={room.status !== 'Available'} onClick={() => setShowModal(true)}>{room.status === 'Available' ? 'Reserve' : 'Unavailable'} <Icon name="arrow" size={14} /></button></div></div>})}</section>
        <Schedule rooms={visibleRooms} bookings={data.bookings} />
        <section className="upcoming-card"><div className="section-heading compact"><div><p className="eyebrow">YOUR CALENDAR</p><h2>Upcoming bookings</h2></div><button className="text-button" onClick={() => setToast('You are viewing all bookings for this day.')}>View all <Icon name="arrow" size={14} /></button></div>{upcoming.length ? <div className="booking-list">{upcoming.slice(0, 4).map((booking) => <div className="booking-list-row" key={booking.id}><div className="booking-time"><strong>{formatTime(booking.startTime)}</strong><span>{formatTime(booking.endTime)}</span></div><div className="booking-color" /><div className="booking-info"><strong>{booking.title}</strong><span>{booking.roomName} · {booking.attendees} attendees</span></div><span className="booking-owner">{booking.userName === user.name ? 'You' : booking.userName}</span>{(booking.userId === user.id || user.role === 'Admin') && <button className="cancel-button" onClick={() => cancelBooking(booking.id)}>Cancel</button>}</div>)}</div> : <div className="empty-state"><Icon name="calendar" size={22} /> No confirmed bookings for this day.</div>}</section>
      </>}
    </main>
    {showModal && <BookingModal rooms={data?.rooms || []} date={date} onClose={() => setShowModal(false)} onCreated={(booking) => { setShowModal(false); setToast(`Booked ${booking.roomName}`); load() }} />}
    {toast && <div className="toast"><Icon name="check" size={17} /> {toast}</div>}
  </div>
}

function App() {
  const [user, setUser] = useState(null)
  const [checking, setChecking] = useState(Boolean(getToken()))
  useEffect(() => { if (!getToken()) return setChecking(false); api.me().then(setUser).catch(() => setToken(null)).finally(() => setChecking(false)) }, [])
  function logout() { setToken(null); setUser(null) }
  if (checking) return <div className="loading-state full-page"><div className="spinner" /> Checking session…</div>
  return user ? <Dashboard user={user} onLogout={logout} /> : <Login onLogin={setUser} />
}

createRoot(document.getElementById('root')).render(<React.StrictMode><App /></React.StrictMode>)
