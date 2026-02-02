# CLAUDE.md — Project Context for AI Assistants

## Project Overview
USushi Loyalty Rewards App — a full-stack web application for a sushi restaurant loyalty program.
Customers earn a free meal for every 10 meals in a rolling 3-month period.
Built with .NET 8 Web API backend + React/Vite/TypeScript frontend, deployed to IIS.

## Architecture
- **Backend:** `backend/ProjectTemplate.Api/` — .NET 8 Web API with Dapper + SQL Server
- **Frontend:** `frontend/` — React 18 + Vite + TypeScript + Tailwind CSS
- **Deployment:** GitHub Actions → self-hosted runner → IIS at usushi.synthia.bot

## Key Features
- **Phone OTP Authentication** — users log in with phone number + SMS verification code
- **Receipt Scanning** — upload receipt photos, OCR via OpenAI Vision (GPT-4o-mini)
- **Loyalty Tracking** — 10 meals in 3 months = free meal reward
- **Admin SMS Broadcasts** — send promo messages to all users
- **Reward Management** — admin marks rewards as redeemed

## Key Patterns
- Backend uses Dapper (not EF Core) for SQL Server access
- JWT authentication (phone-based, no passwords)
- Auto-migration in Program.cs creates all tables on startup
- Controller routes use `[Route("[controller]")]` (IIS virtual app mounts at /api)
- Frontend uses typed fetch wrapper (`src/services/api.ts`) with file upload support
- AuthContext manages JWT tokens in localStorage, OTP flow
- SMS via Twilio REST API

## Deployment
- Physical paths: `F:\New_WWW\usushi\WWW` (frontend) + `F:\New_WWW\usushi\API` (backend)
- Deploy script: `Deployment/deploy-iis.ps1`
- Health endpoint: `GET /health` returns 200

## Configuration Placeholders (appsettings.json)
- `__CONNECTION_STRING__` — SQL Server connection string
- `__JWT_KEY__` — JWT signing key (min 32 characters)
- `__CORS_ORIGINS__` — Comma-separated allowed CORS origins
- `__OPENAI_API_KEY__` — OpenAI API key for receipt OCR
- `__TWILIO_ACCOUNT_SID__` — Twilio Account SID
- `__TWILIO_AUTH_TOKEN__` — Twilio Auth Token
- `__TWILIO_FROM_NUMBER__` — Twilio sender phone number

## Build Commands
```bash
# Backend
cd backend/ProjectTemplate.Api && dotnet build

# Frontend
cd frontend && npm install && npm run build
```

## API Endpoints
### Auth
- `POST /auth/send-otp` — Send OTP to phone number
- `POST /auth/verify-otp` — Verify OTP and get JWT
- `POST /auth/setup` — Bootstrap admin (only when 0 users)
- `GET /auth/me` — Current user profile
- `PUT /auth/profile` — Update display name

### Meals
- `POST /meals/upload` — Upload receipt image (multipart/form-data)
- `POST /meals/{id}/confirm` — Confirm pending meal with manual total
- `GET /meals` — List user's meals (paginated)
- `GET /meals/dashboard` — Dashboard progress data

### Rewards
- `GET /rewards` — User's rewards
- `GET /rewards/all` — All rewards (admin)
- `POST /rewards/{id}/redeem` — Mark reward redeemed (admin)

### Admin
- `GET /admin/dashboard` — Stats overview
- `GET /admin/users` — All users with meal stats
- `PUT /admin/users/{id}` — Update user
- `POST /admin/sms-broadcast` — Send SMS to users
- `GET /admin/sms-broadcasts` — Broadcast history

### Notifications
- `GET /notifications` — User's notifications
- `POST /notifications/{id}/read` — Mark as read
- `POST /notifications/read-all` — Mark all read

## Database Tables
- **Users** — Phone-based auth (no passwords), roles (User/Admin)
- **OtpCodes** — SMS verification codes with expiry and attempt tracking
- **Meals** — Receipt records with OCR data and manual fallback
- **Rewards** — Free meal rewards with earned/redeemed status
- **SmsBroadcasts** — Admin broadcast history
- **Notifications** — In-app notifications
