using System.CommandLine;
using System.Diagnostics;

namespace LowBatCat;

internal class Program
{
    private static readonly Dictionary<int, bool> NotificationSent = [];
    private static string? BatteryPath;
    private static string? BatteryName;
    internal static readonly int[] default_threshold = [25, 20, 10];

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Battery notification service for Linux");

        var thresholdsOption = new Option<int[]>(
            name: "--thresholds",
            description: "Battery percentage thresholds for notifications (e.g., --thresholds 25 20 10)")
        {
            AllowMultipleArgumentsPerToken = true
        };
        thresholdsOption.SetDefaultValue(default_threshold);

        var intervalOption = new Option<int>(
            name: "--interval",
            description: "Check interval in seconds")
        {
            ArgumentHelpName = "seconds"
        };
        intervalOption.SetDefaultValue(30);

        var daemonOption = new Option<bool>(
            name: "--daemon",
            description: "Run as background service");

        rootCommand.AddOption(thresholdsOption);
        rootCommand.AddOption(intervalOption);
        rootCommand.AddOption(daemonOption);

        rootCommand.SetHandler(RunBatteryMonitor, thresholdsOption, intervalOption, daemonOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunBatteryMonitor(int[] thresholds, int interval, bool daemon)
    {
        Console.WriteLine("Starting Battery Notification Service...");

        if (!await InitializeBattery())
        {
            Console.WriteLine("No battery found or unable to access battery information.");
            return;
        }

        Console.WriteLine($"Monitoring battery: {BatteryName}");
        Console.WriteLine($"Thresholds: {string.Join(", ", thresholds.OrderByDescending(x => x))}%");
        Console.WriteLine($"Check interval: {interval} seconds");

        if (daemon)
        {
            Console.WriteLine("Running in daemon mode...");
        }

        // Initialize notification tracking
        foreach (var threshold in thresholds)
        {
            NotificationSent[threshold] = false;
        }

        var cancellationToken = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationToken.Cancel();
        };

        try
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                await CheckBatteryAndNotify(thresholds);
                await Task.Delay(interval * 1000, cancellationToken.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutting down battery monitor...");
        }
    }

    private static async Task<bool> InitializeBattery()
    {
        try
        {
            var powerSupplyPath = "/sys/class/power_supply";
            if (!Directory.Exists(powerSupplyPath))
                return false;

            var batteries = Directory.GetDirectories(powerSupplyPath)
                .Where(dir => File.Exists(Path.Combine(dir, "type")) &&
                             File.ReadAllText(Path.Combine(dir, "type")).Trim() == "Battery")
                .ToList();

            if (batteries.Count == 0)
                return false;

            BatteryPath = batteries.First();
            BatteryName = Path.GetFileName(BatteryPath);

            // Verify we can read capacity
            var capacityFile = Path.Combine(BatteryPath, "capacity");
            if (!File.Exists(capacityFile))
                return false;

            await File.ReadAllTextAsync(capacityFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task CheckBatteryAndNotify(int[] thresholds)
    {
        try
        {
            if (BatteryPath == null) return;

            var capacityFile = Path.Combine(BatteryPath, "capacity");
            var statusFile = Path.Combine(BatteryPath, "status");

            if (!File.Exists(capacityFile)) return;

            var capacityText = await File.ReadAllTextAsync(capacityFile);
            if (!int.TryParse(capacityText.Trim(), out var batteryLevel))
                return;

            var status = File.Exists(statusFile)
                ? (await File.ReadAllTextAsync(statusFile)).Trim()
                : "Unknown";

            var isCharging = status.Equals("Charging", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine($"Battery: {batteryLevel}% ({status})");

            // Reset notifications when charging
            if (isCharging)
            {
                foreach (var threshold in thresholds)
                {
                    NotificationSent[threshold] = false;
                }
                return;
            }

            // Check thresholds (only when not charging)
            foreach (var threshold in thresholds.OrderByDescending(x => x))
            {
                if (batteryLevel <= threshold && !NotificationSent[threshold])
                {
                    await SendNotification($"Battery Low: {batteryLevel}%",
                        $"Battery level has dropped to {batteryLevel}%. Consider charging soon.");
                    NotificationSent[threshold] = true;
                    break; // Only send one notification per check
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking battery: {ex.Message}");
        }
    }

    private static async Task SendNotification(string title, string message)
    {
        try
        {
            await SendDBusNotification(title, message);
            Console.WriteLine($"Notification sent: {title}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending notification via DBus: {ex.Message}");
            // Fallback to console notification
            Console.WriteLine($"ALERT: {title} - {message}");
        }
    }

    private static async Task SendDBusNotification(string title, string message)
    {
        try
        {
            // Try gdbus first (more modern and reliable)
            await TryGdbus(title, message);
        }
        catch
        {
            try
            {
                // Fallback to dbus-send
                await TryDbusSend(title, message);
            }
            catch
            {
                // Final fallback to busctl (systemd)
                await TryBusctl(title, message);
            }
        }
    }

    private static async Task TryGdbus(string title, string message)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gdbus",
                Arguments = $"call --session --dest org.freedesktop.Notifications --object-path /org/freedesktop/Notifications --method org.freedesktop.Notifications.Notify \"LowBatCat\" 0 \"battery-low\" \"{EscapeString(title)}\" \"{EscapeString(message)}\" [] {{}} 5000",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"gdbus failed: {error}");
        }
    }

    private static async Task TryDbusSend(string title, string message)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dbus-send",
                Arguments = $"--session --dest=org.freedesktop.Notifications --type=method_call /org/freedesktop/Notifications org.freedesktop.Notifications.Notify string:LowBatCat uint32:0 string:battery-low string:\"{EscapeString(title)}\" string:\"{EscapeString(message)}\" array:string: dict:string:variant: int32:5000",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"dbus-send failed: {error}");
        }
    }

    private static async Task TryBusctl(string title, string message)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "busctl",
                Arguments = $"--user call org.freedesktop.Notifications /org/freedesktop/Notifications org.freedesktop.Notifications.Notify susssasa{{sv}}i LowBatCat 0 battery-low \"{EscapeString(title)}\" \"{EscapeString(message)}\" 0 0 5000",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"busctl failed: {error}");
        }
    }

    private static string EscapeString(string input)
    {
        return input.Replace("\"", "\\\"").Replace("\\", "\\\\");
    }
}