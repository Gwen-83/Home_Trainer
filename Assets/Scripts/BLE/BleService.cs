using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;                  // needed for FirstOrDefault
using Debug = UnityEngine.Debug;   // ensure Unity's Debug is used everywhere

#if !UNITY_EDITOR && UNITY_WSA
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
// using System.Diagnostics;      // removed to avoid ambiguous Debug
using System.Threading.Tasks;
#endif

/// <summary>
/// BLE service wrapper for Unity.  
/// - In editor (or when simulateInEditor=true) it emits fake power data.
/// - On Windows platforms it can use the real FTMS protocol (Van Rysel HT) via
///   the Windows.Devices.Bluetooth APIs.  
///   The logic is essentially a port of the desktop <c>BleService</c> class.
/// </summary>
public class BleService : MonoBehaviour
{
    public bool simulateInEditor = true;      // force the fake flow
    public bool autoConnect = true;            // if true the service will start scanning immediately
    public string deviceNameFilter = "VANRYSEL"; // advertisement name substring to look for
    public double simulatedPower = 150.0;

    public event Action<double> OnPowerReceived;
    public event Action OnConnected;
    public event Action<string> OnDisconnected;
    public event Action<string> OnError;

    bool connected = false;
    float lastEmit = 0f;

    public bool IsConnected => connected;

#if !UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_WSA)
    private RealBleAdapter adapter;
#endif

    void Start()
    {
        Debug.Log($"BleService.Start simulateInEditor={simulateInEditor} autoConnect={autoConnect} platform={Application.platform}");
#if UNITY_EDITOR
        if (!simulateInEditor)
        {
            Debug.LogWarning("BleService: real BLE is disabled in Editor, build a standalone to connect the HT.");
        }
#endif
#if !UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_WSA)
        if (!simulateInEditor)
        {
            Debug.Log("BleService: using RealBleAdapter");
            adapter = new RealBleAdapter(deviceNameFilter);
            adapter.OnPowerReceived += (p) => { Debug.Log($"BleService: power received {p}"); OnPowerReceived?.Invoke(p); };
            adapter.OnConnected += () => { connected = true; Debug.Log("BleService: adapter connected"); OnConnected?.Invoke(); };
            adapter.OnDisconnected += (msg) => { connected = false; Debug.Log("BleService: adapter disconnected: " + msg); OnDisconnected?.Invoke(msg); };
            adapter.OnError += (msg) => { Debug.LogWarning("BleService error: " + msg); OnError?.Invoke(msg); };
            if (autoConnect)
                adapter.ConnectAsync();
            return;
        }
#endif
        if (simulateInEditor)
        {
            Debug.Log("BleService: using simulated connection");
            if (autoConnect)
                Connect();
        }
    }

    public void Connect()
    {
        if (connected)
        {
            Debug.Log("BleService: Connect() called but already connected");
            return;
        }

#if !UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_WSA)
        if (!simulateInEditor)
        {
            if (adapter == null)
            {
                adapter = new RealBleAdapter(deviceNameFilter);
                adapter.OnPowerReceived += (p) => { Debug.Log($"BleService: power received {p}"); OnPowerReceived?.Invoke(p); };
                adapter.OnConnected += () => { connected = true; Debug.Log("BleService: adapter connected"); OnConnected?.Invoke(); };
                adapter.OnDisconnected += (msg) => { connected = false; Debug.Log("BleService: adapter disconnected: " + msg); OnDisconnected?.Invoke(msg); };
                adapter.OnError += (msg) => { Debug.LogWarning("BleService error: " + msg); OnError?.Invoke(msg); };
            }
            Debug.Log("BleService: starting BLE connection attempt");
            adapter.ConnectAsync();
            return;
        }
#endif
        // fallback / simulation
        connected = true;
        Debug.Log("BleService: Connect called (mock)");
        OnConnected?.Invoke();
    }

    public void Disconnect()
    {
        if (!connected)
        {
            Debug.Log("BleService: Disconnect() called but not connected");
            return;
        }

        connected = false;
        Debug.Log("BleService: Disconnect called");
        OnDisconnected?.Invoke("Disconnected (Unity mock)");
#if !UNITY_EDITOR && UNITY_WSA
        adapter?.Disconnect();
#endif
    }

    void Update()
    {
#if !UNITY_EDITOR && UNITY_WSA
        if (!simulateInEditor && adapter != null)
        {
            // adapter handles its own callbacks
            return;
        }
#endif
        // emit a simulated power measurement at ~10Hz
        if (!simulateInEditor || !connected) return;
        lastEmit += Time.deltaTime;
        if (lastEmit >= 0.1f)
        {
            lastEmit = 0f;
            OnPowerReceived?.Invoke(simulatedPower);
        }
    }

    // Methods used by original code: send simulation set and send target power
    public void SendSimulationSet(double pente, double crr, double cda)
    {
#if !UNITY_EDITOR && UNITY_WSA
        if (!simulateInEditor)
        {
            adapter?.SendSimulationSet(pente, crr, cda);
            return;
        }
#endif
        Debug.Log($"BleService.SendSimulationSet mock: pente={pente} crr={crr} cda={cda}");
    }

    public void SendTargetPower(short watts)
    {
#if !UNITY_EDITOR && UNITY_WSA
        if (!simulateInEditor)
        {
            adapter?.SendTargetPower(watts);
            return;
        }
#endif
        Debug.Log($"BleService.SendTargetPower mock: {watts}W");
        simulatedPower = watts;
    }
}

#if !UNITY_EDITOR && UNITY_WSA
/// <summary>
/// Internal adapter implementing the original desktop BLE code.
/// </summary>
class RealBleAdapter
{
    private readonly string nameFilter;

    public RealBleAdapter(string filter)
    {
        nameFilter = filter ?? string.Empty;
        Debug.Log($"RealBleAdapter instantiated (filter=\"{nameFilter}\")");
    }

    // UUIDs FTMS (Fitness Machine Service)
    private static readonly Guid FTMS_SERVICE_UUID = Guid.Parse("00001826-0000-1000-8000-00805f9b34fb");
    private static readonly Guid POWER_MEASUREMENT_UUID = Guid.Parse("00002ad2-0000-1000-8000-00805f9b34fb");
    private static readonly Guid MACHINE_CONTROL_UUID = Guid.Parse("00002ad9-0000-1000-8000-00805f9b34fb");

    private BluetoothLEDevice device;
    private GattDeviceService service;
    private GattCharacteristic powerChar;
    private GattCharacteristic controlChar;

    private ulong targetAddress = 0;
    private BluetoothLEAdvertisementWatcher watcher = new();

    public event Action<double> OnPowerReceived;
    public event Action OnConnected;
    public event Action<string> OnDisconnected;
    public event Action<string> OnError;

    private System.Diagnostics.Stopwatch lastDataTime = new();
    private const int DATA_TIMEOUT_MS = 5000;
    private bool isRunning = false;
    private Task connectionMonitor;

    public bool IsConnected => device != null && device.ConnectionStatus == BluetoothConnectionStatus.Connected;
    public double LastPower { get; private set; } = 0;

    public async void ConnectAsync()
    {
        Debug.Log("RealBleAdapter.ConnectAsync starting scan");
        watcher.ScanningMode = BluetoothLEScanningMode.Active;
        watcher.Received += (sender, eventArgs) =>
        {
            string local = eventArgs.Advertisement.LocalName ?? "";
            Debug.Log($"Advertisement received: {local}");
            if (!string.IsNullOrEmpty(nameFilter) && local.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            {
                targetAddress = eventArgs.BluetoothAddress;
                Debug.Log($"Target address found: {targetAddress} (matched \"{nameFilter}\")");
                watcher.Stop();
            }
        };
        watcher.Start();

        int waitCount = 0;
        while (targetAddress == 0 && waitCount < 60)
        {
            await Task.Delay(500);
            waitCount++;
        }

        if (targetAddress == 0)
        {
            Debug.LogWarning("RealBleAdapter: device not found after timeout");
            OnError?.Invoke("Van Rysel HT non trouvé après 30s");
            return;
        }

        await ConnectToDeviceAsync(targetAddress);
    }

    private async Task ConnectToDeviceAsync(ulong address)
    {
        Debug.Log($"RealBleAdapter: connecting to address {address}");
        try
        {
            device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            var services = await device.GetGattServicesForUuidAsync(FTMS_SERVICE_UUID);
            service = services.Services.FirstOrDefault();

            if (service == null)
            {
                Debug.LogWarning("RealBleAdapter: FTMS service not found");
                OnError?.Invoke("Service FTMS non trouvé");
                return;
            }

            var pChars = await service.GetCharacteristicsForUuidAsync(POWER_MEASUREMENT_UUID);
            if (pChars.Status == GattCommunicationStatus.Success && pChars.Characteristics.Count > 0)
            {
                powerChar = pChars.Characteristics[0];
                try
                {
                    await powerChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    powerChar.ValueChanged += (s, a) =>
                    {
                        try
                        {
                            var data = a.CharacteristicValue.ToArray();
                            if (data.Length >= 6)
                            {
                                LastPower = BitConverter.ToInt16(data, 4);
                                OnPowerReceived?.Invoke(LastPower);
                                lastDataTime.Restart();
                            }
                        }
                        catch { }
                    };
                }
                catch { }
            }

            var cChars = await service.GetCharacteristicsForUuidAsync(MACHINE_CONTROL_UUID);
            if (cChars.Status == GattCommunicationStatus.Success && cChars.Characteristics.Count > 0)
            {
                controlChar = cChars.Characteristics[0];
                try
                {
                    await controlChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Indicate);
                }
                catch { }
            }

            isRunning = true;
            lastDataTime.Start();
            OnConnected?.Invoke();
            StartConnectionMonitor();
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Erreur connexion BLE: {ex.Message}");
        }
    }

    private void StartConnectionMonitor()
    {
        connectionMonitor = Task.Run(async () =>
        {
            while (isRunning && device != null)
            {
                if (lastDataTime.ElapsedMilliseconds > DATA_TIMEOUT_MS)
                {
                    OnError?.Invoke("Timeout puissance BLE (> 5s)");
                    LastPower = 0;
                }

                if (device.ConnectionStatus != BluetoothConnectionStatus.Connected)
                {
                    OnDisconnected?.Invoke("Déconnecté du HT");
                    isRunning = false;
                    break;
                }

                await Task.Delay(1000);
            }
        });
    }

    public async void SendSimulationSet(double pente, double crr, double cda)
    {
        if (controlChar == null) return;
        try
        {
            pente = Math.Clamp(pente, -0.06, 0.06);
            short grade = (short)(pente * 10000);
            short windSpeed = 0;
            byte crrByte = (byte)(crr * 10000);
            byte cdaByte = (byte)(cda * 10);

            byte[] cmd = new byte[]
            {
                0x11,
                (byte)(windSpeed & 0xFF),
                (byte)(windSpeed >> 8),
                (byte)(grade & 0xFF),
                (byte)(grade >> 8),
                crrByte,
                cdaByte
            };

            await controlChar.WriteValueAsync(cmd.AsBuffer());
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Erreur envoi simulation: {ex.Message}");
        }
    }

    public async void SendTargetPower(short watts)
    {
        if (controlChar == null) return;
        try
        {
            byte[] cmd = new byte[] { 0x05, (byte)(watts & 0xFF), (byte)(watts >> 8) };
            await controlChar.WriteValueAsync(cmd.AsBuffer());
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Erreur envoi puissance: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        isRunning = false;
        watcher.Stop();
        device?.Dispose();
        service?.Dispose();
        powerChar = null;
        controlChar = null;
    }
}
#endif