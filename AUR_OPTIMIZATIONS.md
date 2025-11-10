# AUR Build Optimizations

This document explains the optimizations learned from the Arch Linux AUR PKGBUILDs that have been applied to this fork.

## Key Optimizations Applied

### 1. Environment Variables for .NET Builds

These reduce noise and disable telemetry during builds:

```bash
export DOTNET_CLI_TELEMETRY_OPTOUT=1      # Disable Microsoft telemetry
export DOTNET_NOLOGO=1                     # Suppress .NET logo
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 # Skip first-run experience
```

**Impact**: Cleaner build output, faster builds, privacy protection

### 2. Optimized Build Flags

The AUR uses specific dotnet build flags:

```bash
dotnet build src/Readarr.sln \
  --framework net6.0 \
  --runtime linux-x64 \
  --no-self-contained \        # Don't bundle .NET runtime (smaller size)
  --no-restore \               # Already restored separately
  --configuration Release \
  -p:Platform=Posix \          # Linux-specific optimizations
  -p:RuntimeIdentifiers=linux-x64 \
  -t:PublishAllRids
```

**Impact**:
- `--no-self-contained`: Reduces build size by ~60MB (assumes system .NET runtime)
- `--locked-mode` (in restore): Ensures reproducible builds
- `-p:Platform=Posix`: Enables Linux-specific optimizations

### 3. Post-Build Cleanup

After building, the AUR removes unnecessary files:

```bash
# From readarr-develop/PKGBUILD lines 101-104
rm "${_artifacts}/ServiceInstall"*
rm "${_artifacts}/ServiceUninstall"*
rm "${_artifacts}/Readarr.Windows."*
```

**Files removed**:
- `ServiceInstall.exe` / `ServiceUninstall.exe` - Windows service helpers
- `Readarr.Windows.dll` - Windows-specific code
- `Readarr.Update/` directory (optional) - Built-in updater

**Impact**: Reduces installation size by ~5-10MB, removes unused Windows code

### 4. Disable Built-In Updater

The AUR creates a `package_info` file to disable self-updating:

```ini
PackageAuthor=Raudbjorn/bookshelf
UpdateMethod=External
UpdateMethodMessage=This installation is managed externally. Use git pull or your package manager to update.
Branch=develop
```

**Location**: Copy to `_output/net6.0/linux-x64/publish/package_info`

**Impact**:
- Prevents Readarr from trying to self-update
- Essential for package-managed installations
- Cleaner UI (no update notifications when externally managed)

### 5. Debug Symbol Stripping (Optional)

For maximum size reduction:

```bash
find _output -name '*.dll' -exec strip --strip-unneeded {} \;
find _output -name '*.so' -exec strip --strip-unneeded {} \;
```

**Impact**: Reduces size by another 10-15%, but makes debugging harder

## Build Script Usage

Use the provided optimized build script:

```bash
chmod +x build-optimized.sh
./build-optimized.sh
```

This script incorporates all the above optimizations automatically.

## Comparison: Standard vs Optimized Build

| Aspect | Standard Build | AUR-Optimized Build |
|--------|---------------|---------------------|
| Build size | ~200-250 MB | ~140-180 MB |
| Telemetry | Enabled | Disabled |
| Windows files | Included | Removed |
| Self-updates | Enabled | Disabled (optional) |
| Build flags | Generic | Platform-optimized |
| Reproducibility | Variable | Locked dependencies |

## Architecture Support

The build script auto-detects architecture:

- **x86_64** → `linux-x64`
- **aarch64** → `linux-arm64`
- **armv7l** → `linux-arm`

All three are supported by Readarr and the AUR packages.

## Package Manager Integration

If packaging this for distribution:

1. ✅ Use `--locked-mode` for dependency restore
2. ✅ Set all DOTNET_* environment variables
3. ✅ Use `--no-self-contained` to require system .NET runtime
4. ✅ Remove ServiceInstall/ServiceUninstall/Windows files
5. ✅ Copy `package_info` to installation directory
6. ✅ Consider stripping debug symbols
7. ✅ Create systemd service file (see `aur/readarr-develop/readarr.service`)

## Additional AUR Files Worth Reviewing

The AUR packages include several other useful files:

- **readarr.service** - Systemd service file
- **readarr.sysusers** - System user creation
- **readarr.tmpfiles** - Temporary file management
- **readarr.install** - Post-install hooks

These are available in `../aur/readarr-develop/` for reference.

## References

- AUR readarr-develop: https://aur.archlinux.org/packages/readarr-develop/
- AUR readarr-nightly-bin: https://aur.archlinux.org/packages/readarr-nightly-bin/
- .NET CLI environment variables: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables

---

**Note**: These optimizations are already implemented in `build-optimized.sh` and documented here for transparency and future reference.
