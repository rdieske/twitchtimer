# HTTPS Setup Guide

## Voraussetzungen

1. **Domain**: Sie benötigen eine eigene Domain (z.B. `timer.example.com`)
2. **Server**: Ein Server mit öffentlicher IP-Adresse
3. **DNS**: DNS A-Record muss auf Ihre Server-IP zeigen

## Option 1: Caddy (Automatisches HTTPS) ⭐ EMPFOHLEN

Caddy holt automatisch kostenlose SSL-Zertifikate von Let's Encrypt.

### Setup:

1. **Caddyfile bearbeiten:**
   ```bash
   nano Caddyfile
   ```
   
   Ersetzen Sie `your-domain.com` mit Ihrer echten Domain:
   ```
   timer.example.com {
       reverse_proxy twitch-timer:8080
   }
   ```

2. **.env aktualisieren:**
   ```bash
   nano .env
   ```
   
   Ändern Sie die Redirect URI:
   ```
   TWITCH_REDIRECT_URI=https://timer.example.com/auth/callback
   ```

3. **Twitch App aktualisieren:**
   - Gehen Sie zu https://dev.twitch.tv/console/apps
   - Fügen Sie `https://timer.example.com/auth/callback` zu den OAuth Redirect URLs hinzu

4. **Docker neu starten:**
   ```bash
   docker compose down
   docker compose up -d
   ```

5. **Fertig!** 🎉
   - Ihre App ist jetzt unter `https://timer.example.com` erreichbar
   - Caddy erneuert die Zertifikate automatisch

---

## Option 2: Nginx mit Certbot

Falls Sie Nginx bevorzugen:

### docker-compose.yml anpassen:

```yaml
services:
  twitch-timer:
    # ... existing config ...
    expose:
      - "8080"
    networks:
      - web

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
      - ./certbot/conf:/etc/letsencrypt
      - ./certbot/www:/var/www/certbot
    networks:
      - web

  certbot:
    image: certbot/certbot
    volumes:
      - ./certbot/conf:/etc/letsencrypt
      - ./certbot/www:/var/www/certbot
    entrypoint: "/bin/sh -c 'trap exit TERM; while :; do certbot renew; sleep 12h & wait $${!}; done;'"

networks:
  web:
```

### nginx.conf erstellen:

```nginx
events {
    worker_connections 1024;
}

http {
    server {
        listen 80;
        server_name timer.example.com;
        
        location /.well-known/acme-challenge/ {
            root /var/www/certbot;
        }
        
        location / {
            return 301 https://$host$request_uri;
        }
    }
    
    server {
        listen 443 ssl;
        server_name timer.example.com;
        
        ssl_certificate /etc/letsencrypt/live/timer.example.com/fullchain.pem;
        ssl_certificate_key /etc/letsencrypt/live/timer.example.com/privkey.pem;
        
        location / {
            proxy_pass http://twitch-timer:8080;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
```

### Zertifikat holen:

```bash
docker compose run --rm certbot certonly --webroot --webroot-path /var/www/certbot -d timer.example.com
```

---

## Option 3: Cloudflare Tunnel (Kein Port-Forwarding nötig)

Falls Ihr Server hinter einem Router ist:

1. **Cloudflare Account erstellen** (kostenlos)
2. **Domain zu Cloudflare hinzufügen**
3. **Cloudflare Tunnel erstellen:**
   ```bash
   docker run cloudflare/cloudflared:latest tunnel login
   docker run cloudflare/cloudflared:latest tunnel create timer
   ```

4. **docker-compose.yml erweitern:**
   ```yaml
   cloudflared:
     image: cloudflare/cloudflared:latest
     command: tunnel --no-autoupdate run --token YOUR_TUNNEL_TOKEN
     restart: unless-stopped
     networks:
       - web
   ```

---

## Lokales HTTPS für Entwicklung

Für lokale Entwicklung mit selbst-signiertem Zertifikat:

```bash
# Zertifikat erstellen
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365 -nodes

# Caddyfile für lokales HTTPS
localhost {
    tls cert.pem key.pem
    reverse_proxy twitch-timer:8080
}
```

---

## Troubleshooting

**Ports nicht erreichbar?**
- Firewall-Regeln prüfen: `sudo ufw allow 80/tcp && sudo ufw allow 443/tcp`
- DNS-Propagation prüfen: `nslookup timer.example.com`

**Let's Encrypt Fehler?**
- Domain muss auf Server-IP zeigen
- Port 80 muss von außen erreichbar sein
- Logs prüfen: `docker logs timer-caddy-1`

**Twitch Redirect funktioniert nicht?**
- `.env` aktualisiert?
- Twitch App Console aktualisiert?
- Docker neu gestartet?
