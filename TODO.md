# 🍣 USushi (usushi.synthia.bot) — Analysis & TODO
**Last Updated:** 2026-02-03 | **Maintainer:** Synthia

## Current State
- **Repo:** 3E-Tech-Corp/usushi
- **Live:** usushi.synthia.bot
- **Stack:** .NET 8 + React/Vite/TS + Tailwind + Dapper + SQL Server
- **IIS:** Site `Demo_USushi`, DB `Demo_USushi`
- **Status:** Feature complete v1, deployed

## ✅ Completed
- [x] Phone OTP login via FXNotification SMS (JWT expiry: 30 days)
- [x] Receipt photo upload + AI scanning (OpenAI GPT-4o-mini Vision)
- [x] Meal tracking — 10 verified meals in rolling 3 months = free meal + SMS notification
- [x] Admin SMS blasts — compose and send to all users
- [x] Rewards management — earn/redeem free meals
- [x] Profile page — First + Last Name (auto-combined into DisplayName)
- [x] Admin Users page — list with stats + Export to Excel (CSV)
- [x] Mobile-friendly layout — collapsible sidebar, hamburger menu
- [x] Dashboard — progress bar (X/10 meals), profile completion, quick actions
- [x] DALL-E logo generated

## 🟡 Medium
- [ ] **Receipt approval workflow** — Admin reviews AI-scanned receipts before crediting meals (currently auto-approved)
- [ ] **Push notifications** — Remind users approaching reward threshold (e.g., "2 more meals to go!")
- [ ] **Menu integration** — Show restaurant menu in app (photos + prices)
- [ ] **Receipt history** — Users can view their past receipts and meal credits

## 🟢 Low
- [ ] **Referral program** — Invite friends, get bonus meal credits
- [ ] **Birthday rewards** — Auto free meal on user's birthday
- [ ] **Multi-location support** — If U Sushi opens more locations

## Technical Notes
- **SMS:** FXNotification gateway at pie.funtimepb.com/v2/sms/send. Fields: `From`, `To`, `Body`, `Media` (UPPERCASE)
- **Admin phone:** 9549078818 (User ID 1)
- **IIS paths:** Use `AppContext.BaseDirectory` (never `Directory.GetCurrentDirectory()`)
- **Deploy:** Overwrites API folder — run `fix-db.yml` AFTER each deploy to recreate uploads/ with permissions
- **OpenAI key:** In appsettings.Production.json (added via update-config workflow)
