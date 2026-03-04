using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI helper to control the BLE service from a Canvas.
/// Attach to a GameObject in the scene and wire the buttons/texts
/// in the Inspector (see README below).  The class is purposely
/// light-weight so you can adapt it for your layout.
/// </summary>
public class BleUIManager : MonoBehaviour
{
    [Tooltip("Reference to the BleService component (optional, will be auto-found).")]
    public BleService bleService;

    [Tooltip("Button used to connect/disconnect the home trainer.")]
    public Button connectButton;

    [Tooltip("UI Text component showing connection status or errors. Can be either a legacy Text or a TextMeshProUGUI object.")]
    public Text statusText;
    [Tooltip("Alternative TextMeshPro component for status (use this instead of statusText for TMP).")]
    public TMPro.TMP_Text statusTmp;

    [Tooltip("Text element displaying the last received power value. Optional: often handled by other UI (e.g. SimulationBootstrap).")]
    public Text powerText;
    [Tooltip("Alternative TextMeshPro component for power value.")]
    public TMPro.TMP_Text powerTmp;

    void Awake()
    {
        if (bleService == null)
            bleService = FindObjectOfType<BleService>();
    }

    void Start()
    {
        if (bleService == null)
        {
            Debug.LogError("BleUIManager: no BleService found in scene");
            return;
        }

        if (connectButton != null)
            connectButton.onClick.AddListener(ToggleConnection);

        UpdateConnectButton();

        bleService.OnConnected += () => { SetStatus("Connected"); UpdateConnectButton(); };
        bleService.OnDisconnected += (msg) => { SetStatus("Disconnected: " + msg); UpdateConnectButton(); };
        bleService.OnError += (msg) => { SetStatus("Error: " + msg); };
        // power display is optional; if both fields are null nothing happens
        bleService.OnPowerReceived += (p) => { SetPower(p); };
    }

    void ToggleConnection()
    {
        if (bleService.IsConnected)
            bleService.Disconnect();
        else
            bleService.Connect();
    }

    void SetStatus(string txt)
    {
        if (statusText != null)
            statusText.text = txt;
        if (statusTmp != null)
            statusTmp.text = txt;
    }

    void SetPower(double p)
    {
        string txt = $"{p:F0} W";
        if (powerText != null)
            powerText.text = txt;
        if (powerTmp != null)
            powerTmp.text = txt;
    }

    void UpdateConnectButton()
    {
        if (connectButton == null) return;
        // try both Text and TMP on the button
        var txt = connectButton.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.text = bleService.IsConnected ? "Disconnect" : "Connect";
            return;
        }
        var tmp = connectButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null)
        {
            tmp.text = bleService.IsConnected ? "Disconnect" : "Connect";
        }
    }
}
