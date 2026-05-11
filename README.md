# Dotnet Authentication Template

An ASP.NET Core 8 MVC authentication and user-management template with a dark trading-dashboard theme.

## Features

- **Role-based access control** - Admin, Trader, Monitor roles
- **Registration flow** - new users are held pending until an Admin approves them; first registered user becomes Admin automatically
- **Email verification** - account activation link sent on registration
- **Forgotten username** - sends username reminder email
- **Forgotten password** - suspends the account and sends a time-limited reset link
- **Admin user management** - approve/suspend/delete users, change roles, trigger password resets
- **Profile page** - change email address or password
- **Password visibility toggle** - eye icon on every password field
- **Profile dropdown** - hover the avatar to access User Management (Admin only) or Logout
- **Dark theme** - CSS custom properties for easy rebranding

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 8 MVC |
| Database | EF Core 8 + SQLite |
| Auth | Cookie authentication (claims-based) |
| Password hashing | BCrypt.Net-Next |
| Email | MailKit / MimeKit |
| Tokens | HMAC-SHA256 |

## Getting Started

1. Clone the repo and open in VS Code.
2. Copy appsettings.json to appsettings.Development.json and fill in real SMTP and secret key values.
3. Run: dotnet run
4. The first user to register is automatically promoted to Admin.

## Deployment (Linux / systemd)

dotnet publish -c Release -o ./publish then point a systemd unit at the DLL.

## Licence

MIT - use freely as a starting point for your own projects.
