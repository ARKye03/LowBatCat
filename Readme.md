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

**For running the AOT binary (normal usage):**

- A `DBus Session`

**For building from source (development only):**

- .NET 9.0 SDK
- A `DBus Session`

### Build from Source

```bash
git clone https://github.com/ARKye03/LowBatCat
cd LowBatCat
dotnet restore
dotnet build
```

### Create AOT Binary

```bash
dotnet publish -c Release
# Binary will be at: bin/Release/net9.0/linux-x64/publish/LowBatCat
```

## Usage

### Basic Usage

```bash
lowbatcat --help
```

### Custom Configuration

```bash
# Custom thresholds
./lowbatcat --thresholds 50 25 15 5

# Custom check interval (every 60 seconds)
./lowbatcat --interval 60

# Run as background daemon
./lowbatcat --daemon --thresholds 30 20 10

# Combine options
./lowbatcat --daemon --interval 45 --thresholds 40 25 15 5
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
4. **Desktop Integration**: Uses `DBus` methods for native Linux notifications

## Example Output

```sh
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
- DBus session (for desktop notifications)

**Note**: No .NET runtime required when using the AOT compiled binary!

## License

MIT License - Feel free to use and modify as needed.

---

Keep your battery happy and your cat purring! 🐱⚡
