# Systemd Services Directory

Production-ready systemd service configurations for Bookshelf and rreading-glasses.

## Quick Start

```bash
# Install both services
sudo ./install-services.sh

# Setup PostgreSQL databases
sudo ./setup-postgresql.sh

# Start services
sudo systemctl enable --now bookshelf rreading-glasses
```

## Files

### Bookshelf (Readarr Fork)

- `bookshelf.service` - Systemd service definition
- `bookshelf.sysusers` - User/group creation config
- `bookshelf.tmpfiles` - Directory creation config

### rreading-glasses (Metadata API)

- `rreading-glasses.service` - Systemd service definition
- `rreading-glasses.sysusers` - User/group creation config
- `rreading-glasses.tmpfiles` - Directory creation config
- `rreading-glasses.env.example` - Environment configuration template

### Setup Scripts

- `install-services.sh` - Automated service installation
- `setup-postgresql.sh` - PostgreSQL database setup

## Documentation

See `SYSTEMD_SERVICES.md` in the project root for complete documentation including:
- Prerequisites
- Detailed setup instructions
- Configuration options
- Troubleshooting guide
- Security hardening details

## Service Details

### Bookshelf
- **Port**: 8787
- **User**: bookshelf
- **Group**: media
- **Binary**: /opt/bookshelf/Readarr
- **Data**: /var/lib/bookshelf
- **Database**: PostgreSQL (bookshelf)

### rreading-glasses
- **Port**: 8788
- **User**: rreading-glasses
- **Group**: rreading-glasses
- **Binary**: /opt/rreading-glasses/{rggr|rghc}
- **Data**: /var/lib/rreading-glasses
- **Database**: PostgreSQL (rreading_glasses)

## Security

Both services include comprehensive systemd hardening:
- Limited capabilities
- Private /tmp
- Protected system directories
- Restricted network access
- System call filtering

Run `systemd-analyze security <service>` to review.

## Support

For issues, see:
- Full documentation: `../SYSTEMD_SERVICES.md`
- Bookshelf: https://github.com/Raudbjorn/bookshelf
- rreading-glasses: https://github.com/mr55p-dev/rreading-glasses
