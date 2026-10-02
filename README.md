# Roomly - Meeting Room Booking System

Full-stack implementation of the attached developer assignment. The repository contains an ASP.NET Core 8 API and a React/Vite web app that work together end to end.

## What is included

- JWT authentication with `AuthController` (`POST /api/auth/login`, `GET /api/auth/me`)
- CRUD controller for rooms and bookings
- Booking validation: room availability, overlapping times, capacity, ownership and soft cancellation
- JSON files as mock database tables: `users.json`, `rooms.json`, `bookings.json`
- Dapper query layer backed by an in-memory SQLite database hydrated from those JSON tables
- Every write is persisted back to the JSON files
- React dashboard with room cards, occupancy timeline, booking list and create-booking modal
- Separate frontend environment files for Development, UAT and Production
- Swagger/OpenAPI and `/health` endpoint

## Project structure

```text
backend/MeetingRoomBooking.Api/
  Controllers/      AuthController.cs, CrudController.cs
  Data/seed/        users.json, rooms.json, bookings.json
  Models/           entities and API contracts
  Services/         JSON-to-SQLite store and password hashing
frontend/
  src/              React application, API client and styles
  .env.development
  .env.uat
  .env.production
```

## Demo credentials

- Admin: `admin@meetingroom.local` / `Admin123!`
- Member: `user@meetingroom.local` / `Welcome123!`

## Run locally

### Backend

Requires .NET 8 SDK.

```bash
cd backend/MeetingRoomBooking.Api
dotnet restore
dotnet run
```

The API runs at `http://localhost:5050`. Swagger is at `http://localhost:5050/swagger`.

### Frontend

Requires Node.js 20+.

```bash
cd frontend
npm install
npm run dev
```

The web app runs at `http://localhost:5173` and uses `VITE_API_URL` from `.env.development`.

Build the other environments with:

```bash
npm run build:uat
npm run build
```

Update `.env.uat` and `.env.production` with the real deployed API URLs before deployment. The sample URLs are intentionally placeholders.

## API overview

All routes below except login and health require a Bearer token.

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/auth/login` | Authenticate a user |
| GET | `/api/auth/me` | Get the current user |
| GET/POST | `/api/rooms` | List rooms / create a room (Admin for create) |
| GET/PUT/DELETE | `/api/rooms/{id}` | Read, update or delete a room (Admin for mutations) |
| GET | `/api/dashboard?date=YYYY-MM-DD` | Dashboard data and daily stats |
| GET/POST | `/api/bookings` | List or create a booking |
| GET | `/api/bookings/{id}` | Read one booking |
| PUT/DELETE | `/api/bookings/{id}` | Update or cancel a booking |

## Notes

The JSON files are deliberately human-readable and are the persistence mechanism for this assignment. For production, replace `JsonDatabase` with a real database implementation and move the JWT secret to a secret manager/environment variable.
