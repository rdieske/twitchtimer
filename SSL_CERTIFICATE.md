# SSL-Zertifikat einrichten (IONOS)

## Übersicht

Sie haben ein SSL-Zertifikat von IONOS als `.pfx` Datei im Windows Zertifikatsspeicher installiert.
Diese Anleitung zeigt, wie Sie es für Docker/Caddy exportieren und einrichten.

---

## Schritt 1: PFX aus Windows Zertifikatsspeicher exportieren

### 1.1 Zertifikatsverwaltung öffnen

```
Win + R → certlm.msc → Enter
```

**Oder für Benutzer-Zertifikate:**
```
Win + R → certmgr.msc → Enter
```

### 1.2 Zertifikat finden

- Navigieren Sie zu: `Persönlich` → `Zertifikate`
- Suchen Sie nach: `twitchtimer.enchanty.de` oder `*.enchanty.de`

### 1.3 Zertifikat exportieren

1. **Rechtsklick** auf das Zertifikat
2. Wählen Sie: `Alle Aufgaben` → `Exportieren...`
3. Klicken Sie `Weiter`
4. ✅ Wählen Sie: **"Ja, privaten Schlüssel exportieren"**
5. Klicken Sie `Weiter`
6. Format: **"Privater Informationsaustausch - PKCS #12 (.PFX)"**
7. ✅ Aktivieren Sie: **"Wenn möglich, alle Zertifikate im Zertifizierungspfad einbeziehen"**
8. Klicken Sie `Weiter`
9. **Passwort eingeben** (z.B. `temp123` - wird nur temporär benötigt)
10. Klicken Sie `Weiter`
11. Speichern unter: `C:\src\timer\ionos-cert.pfx`
12. Klicken Sie `Fertig stellen`

---

## Schritt 2: PFX in PEM-Format konvertieren

### Option A: Mit Git Bash (Empfohlen)

Öffnen Sie **Git Bash** in `C:\src\timer`:

```bash
cd /c/src/timer

# Verzeichnis erstellen
mkdir -p certs

# Zertifikat extrahieren (Sie werden nach dem PFX-Passwort gefragt)
openssl pkcs12 -in ionos-cert.pfx -clcerts -nokeys -out certs/cert.pem

# Privaten Schlüssel extrahieren (Sie werden nach dem PFX-Passwort gefragt)
openssl pkcs12 -in ionos-cert.pfx -nocerts -nodes -out certs/key.pem

# PFX-Datei löschen (aus Sicherheitsgründen)
rm ionos-cert.pfx
```

### Option B: Mit PowerShell (falls OpenSSL installiert)

```powershell
cd C:\src\timer

# Verzeichnis erstellen
mkdir certs

# Zertifikat extrahieren
openssl pkcs12 -in ionos-cert.pfx -clcerts -nokeys -out certs\cert.pem

# Privaten Schlüssel extrahieren
openssl pkcs12 -in ionos-cert.pfx -nocerts -nodes -out certs\key.pem

# PFX-Datei löschen
del ionos-cert.pfx
```

**Falls OpenSSL nicht gefunden wird:**
- Git Bash verwenden (kommt mit Git für Windows)
- Oder OpenSSL installieren: https://slproweb.com/products/Win32OpenSSL.html

---

## Schritt 3: Zertifikat-Dateien prüfen

```bash
# Dateien anzeigen
ls -la certs/

# Sollte zeigen:
# cert.pem  (Zertifikat + Intermediate Chain)
# key.pem   (Privater Schlüssel)
```

### Zertifikat-Details prüfen:

```bash
# Domain im Zertifikat prüfen
openssl x509 -in certs/cert.pem -text -noout | grep "Subject:"

# Sollte zeigen: CN = twitchtimer.enchanty.de oder CN = *.enchanty.de
```

### Gültigkeit prüfen:

```bash
# Ablaufdatum prüfen
openssl x509 -in certs/cert.pem -noout -dates

# Sollte zeigen:
# notBefore=...
# notAfter=... (Ablaufdatum, typisch 1 Jahr)
```

---

## Schritt 4: Docker-Konfiguration prüfen

Die Konfiguration ist bereits fertig:

### Caddyfile (bereits konfiguriert):
```
twitchtimer.enchanty.de {
    tls /etc/caddy/certs/cert.pem /etc/caddy/certs/key.pem
    reverse_proxy twitch-timer:8080
}
```

### docker-compose.yml (bereits konfiguriert):
```yaml
caddy:
  volumes:
    - ./certs:/etc/caddy/certs:ro  # Zertifikate read-only mounten
```

---

## Schritt 5: Docker starten

```bash
# Alte Container stoppen
docker compose down

# Neu starten mit SSL
docker compose up -d

# Logs ansehen (Ctrl+C zum Beenden)
docker logs timer-caddy-1 -f
```

**Erfolgreiche Logs sollten zeigen:**
```
Caddy serving HTTPS on port 443
```

**KEINE Fehler wie:**
```
certificate and private key do not match
unable to load certificate
```

---

## Schritt 6: HTTPS testen

### Test 1: Logs prüfen

```bash
docker logs timer-caddy-1 --tail 50
```

Suchen Sie nach Fehlern oder Warnungen.

### Test 2: HTTPS-Verbindung testen

```bash
curl -I https://twitchtimer.enchanty.de
```

**Erwartete Ausgabe:**
```
HTTP/2 200
server: Caddy
```

### Test 3: Im Browser testen

Öffnen Sie: https://twitchtimer.enchanty.de

- ✅ Grünes Schloss-Symbol sollte erscheinen
- ✅ Kein Zertifikatsfehler
- ✅ Login mit Twitch sollte funktionieren

---

## Schritt 7: Twitch App aktualisieren

**WICHTIG:** Twitch OAuth Redirect URI muss HTTPS verwenden!

1. Gehen Sie zu: https://dev.twitch.tv/console/apps
2. Klicken Sie auf Ihre App
3. Fügen Sie hinzu: `https://twitchtimer.enchanty.de/auth/callback`
4. ❌ Entfernen Sie: `http://localhost:7283/auth/callback` (falls vorhanden)
5. Speichern

### .env ist bereits aktualisiert:
```
TWITCH_REDIRECT_URI=https://twitchtimer.enchanty.de/auth/callback
```

---

## Troubleshooting

### "openssl: command not found"

**Lösung:**
- Verwenden Sie **Git Bash** statt PowerShell
- Oder installieren Sie OpenSSL: https://slproweb.com/products/Win32OpenSSL.html

### "unable to load certificate"

**Ursache:** Zertifikat-Datei ist beschädigt oder im falschen Format

**Lösung:**
```bash
# Zertifikat-Format prüfen
file certs/cert.pem

# Sollte zeigen: "PEM certificate"
```

Falls nicht PEM-Format:
```bash
# Neu exportieren aus PFX
openssl pkcs12 -in ionos-cert.pfx -clcerts -nokeys -out certs/cert.pem
```

### "certificate and private key do not match"

**Ursache:** Zertifikat und Schlüssel passen nicht zusammen

**Prüfen:**
```bash
# Zertifikat Modulus
openssl x509 -noout -modulus -in certs/cert.pem | openssl md5

# Key Modulus
openssl rsa -noout -modulus -in certs/key.pem | openssl md5
```

Die beiden MD5-Hashes **müssen identisch** sein!

**Lösung:** Beide Dateien neu aus derselben PFX-Datei exportieren

### "Caddy startet nicht"

**Logs prüfen:**
```bash
docker logs timer-caddy-1
```

**Häufige Fehler:**
- Port 443 bereits belegt → Anderen Dienst stoppen
- Zertifikat-Dateien nicht gefunden → Pfad in docker-compose.yml prüfen
- Berechtigungsfehler → `chmod 644 certs/cert.pem` und `chmod 600 certs/key.pem`

### "Browser zeigt Zertifikatsfehler"

**Mögliche Ursachen:**
1. **Falsches Zertifikat:** Domain im Zertifikat stimmt nicht überein
   ```bash
   openssl x509 -in certs/cert.pem -text -noout | grep DNS
   ```

2. **Intermediate fehlt:** Chain nicht vollständig
   - Prüfen Sie, ob `ca_bundle.crt` in `cert.pem` enthalten ist
   - Falls nicht: `cat certificate.cer ca_bundle.crt > certs/cert.pem`

3. **Zertifikat abgelaufen:**
   ```bash
   openssl x509 -in certs/cert.pem -noout -dates
   ```

---

## Zertifikat-Erneuerung

IONOS-Zertifikate laufen typischerweise nach **1 Jahr** ab.

### Ablaufdatum prüfen:

```bash
openssl x509 -in certs/cert.pem -noout -enddate
```

### Erneuerung (2 Wochen vor Ablauf):

1. Neues Zertifikat bei IONOS anfordern/herunterladen
2. Als PFX in Windows importieren
3. Schritte 1-5 wiederholen (PFX exportieren, konvertieren, Docker neu starten)

**Tipp:** Kalender-Erinnerung setzen!

---

## Zusammenfassung - Schnellreferenz

```bash
# 1. PFX aus Windows exportieren (certlm.msc)
#    → Speichern als: C:\src\timer\ionos-cert.pfx

# 2. In PEM konvertieren
cd /c/src/timer
mkdir -p certs
openssl pkcs12 -in ionos-cert.pfx -clcerts -nokeys -out certs/cert.pem
openssl pkcs12 -in ionos-cert.pfx -nocerts -nodes -out certs/key.pem
rm ionos-cert.pfx

# 3. Docker starten
docker compose down
docker compose up -d

# 4. Testen
docker logs timer-caddy-1 -f
curl -I https://twitchtimer.enchanty.de

# 5. Twitch App aktualisieren
#    → https://dev.twitch.tv/console/apps
#    → Redirect URI: https://twitchtimer.enchanty.de/auth/callback
```

---

## Sicherheitshinweise

- ✅ `.pfx` Datei nach Export löschen
- ✅ `certs/` Verzeichnis ist in `.gitignore` (wird nicht zu Git hochgeladen)
- ✅ Privater Schlüssel (`key.pem`) niemals teilen oder hochladen
- ✅ Zertifikat-Dateien nur auf dem Server speichern

