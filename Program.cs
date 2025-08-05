using System.CommandLine;
using System.Diagnostics;
using Tmds.DBus.Protocol;

namespace BatteryNotifier;

internal class Program
{
    private static readonly Dictionary<int, bool> NotificationSent = new();
    private static string? BatteryPath;
    private static string? BatteryName;

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Battery notification service for Linux");

        var thresholdsOption = new Option<int[]>(
            name: "--thresholds",
            description: "Battery percentage thresholds for notifications (e.g., --thresholds 25 20 10)")
        {
            AllowMultipleArgumentsPerToken = true
        };
        thresholdsOption.SetDefaultValue(new int[] { 25, 20, 10 });

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

        rootCommand.SetHandler(async (thresholds, interval, daemon) =>
        {
            await RunBatteryMonitor(thresholds, interval, daemon);
        }, thresholdsOption, intervalOption, daemonOption);

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

            if (!batteries.Any())
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
            using var connection = new Connection(Address.Session!);
            await connection.ConnectAsync();

            var proxy = new MessageBuilder()
            {
                MessageType = MessageType.MethodCall,
                Destination = "org.freedesktop.Notifications",
                Path = "/org/freedesktop/Notifications",
                Interface = "org.freedesktop.Notifications",
                Member = "Notify"
            };

            proxy.WriteString("BatteryNotifier");  // app_name
            proxy.WriteUInt32(0);                  // replaces_id
            proxy.WriteString("battery-low");      // app_icon
            proxy.WriteString(title);              // summary
            proxy.WriteString(message);            // body
            proxy.WriteArray<string>(new string[0]);          // actions
            proxy.WriteDict<string, object>(new Dictionary<string, object>());   // hints
            proxy.WriteInt32(5000);                // expire_timeout (5 seconds)

            var request = proxy.BuildMessage();
            await connection.CallMethodAsync(request);

            Console.WriteLine($"Notification sent: {title}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send notification: {ex.Message}");
            // Fallback to console notification
            Console.WriteLine($"ALERT: {title} - {message}");
        }
    }
}