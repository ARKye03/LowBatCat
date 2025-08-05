# LowBatCat 🔋😸

A lightweight C# battery notification service for Linux that purrs when your battery gets low.

## Features

- **Smart Battery Detection**: Automatically finds and monitors your laptop battery
- **Configurable Thresholds**: Set custom notification levels (default: 25%, 20%, 10%)
- **Native Linux Notifications**: Uses desktop notifications via `notify-send`
- **Intelligent Logic**: Only notifies when discharging, resets when charging starts
- **AOT Compiled**: Fast startup, low memory usage, single executable
- **Background Service**: Run as a daemon for continuous monitoring

## Installation

### Prerequisites

- .NET 9.0 SDK
- `notify-send` (usually included with most Linux desktop environments)

### Build from Source

```bash
git clone <your-repo>
cd LowBatCat
dotnet restore
dotnet build
```

### Create AOT Binary

```bash
dotnet publish -c Release
# Binary will be at: bin/Release/net9.0/linux-x64/publish/BatteryNotif
```

## Usage

### Basic Usage

```bash
# Run with default settings (25%, 20%, 10% thresholds, 30s interval)
./BatteryNotif

# Or with dotnet
dotnet run
```

### Custom Configuration

```bash
# Custom thresholds
./BatteryNotif --thresholds 50 25 15 5

# Custom check interval (every 60 seconds)
./BatteryNotif --interval 60

# Run as background daemon
./BatteryNotif --daemon --thresholds 30 20 10

# Combine options
./BatteryNotif --daemon --interval 45 --thresholds 40 25 15 5
```

### Command Line Options

- `--thresholds`: Battery percentage levels for notifications (space-separated)
- `--interval`: Check interval in seconds (default: 30)
- `--daemon`: Run as background service
- `--help`: Show help information

## How It Works

1. **Battery Detection**: Scans `/sys/class/power_supply/` for battery devices
2. **Monitoring**: Checks battery level at specified intervals
3. **Smart Notifications**:
   - Only sends notifications when battery is discharging
   - Prevents notification spam by tracking sent alerts
   - Resets notification flags when charging begins
4. **Desktop Integration**: Uses `notify-send` for native Linux notifications

## Example Output

```
Starting Battery Notification Service...
Monitoring battery: BAT0
Thresholds: 25, 20, 10%
Check interval: 30 seconds
Battery: 52% (Discharging)
Battery: 26% (Discharging)
Battery: 24% (Discharging)
Notification sent: Battery Low: 24%
```

## System Requirements

- Linux with `/sys/class/power_supply/` support
- Desktop environment with notification support
- .NET 9.0 runtime (or use AOT binary)

## License

MIT License - Feel free to use and modify as needed.

---

*Keep your battery happy and your cat purring! 🐱⚡*
