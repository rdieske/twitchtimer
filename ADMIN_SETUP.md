# Admin Dashboard Setup

## Getting Your Twitch User ID

To access the Admin Dashboard, you need to set your Twitch User ID in the `.env.local` file.

### Method 1: Using StreamWeasels Tool
1. Go to: https://www.streamweasels.com/tools/convert-twitch-username-to-user-id/
2. Enter your Twitch username
3. Copy the User ID (a number like `123456789`)

### Method 2: Using Browser Console
1. Log into the app at `http://localhost:8080`
2. Open Browser DevTools (F12)
3. Go to "Application" tab → "Cookies"
4. Look for the cookie value with your user ID

### Method 3: Using Twitch API
```bash
curl -X GET 'https://api.twitch.tv/helix/users?login=YOUR_USERNAME' \
  -H 'Authorization: Bearer YOUR_ACCESS_TOKEN' \
  -H 'Client-Id: YOUR_CLIENT_ID'
```

## Configuration

Edit `.env.local` and replace `YOUR_TWITCH_USER_ID_HERE` with your actual User ID:

```bash
AdminUserId=123456789
```

## Accessing the Admin Dashboard

1. Make sure you're logged in with your Twitch account
2. Click the "Admin Panel" icon (⚙️) in the top navigation bar
3. Or navigate directly to: `http://localhost:8080/admin`

## Features

The Admin Dashboard allows you to:
- View all active user sessions
- See connection status (Connected/Disconnected)
- Monitor which channels are being tracked
- Disconnect users if needed
- View connection timestamps and activity

## Security

- Only the user ID specified in `AdminUserId` can access the admin dashboard
- All other users will see "Access Denied"
- The admin icon only appears in the navigation for the admin user
