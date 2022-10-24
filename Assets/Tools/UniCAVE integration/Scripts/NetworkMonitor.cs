
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using EdyCommonTools;
using Mirror;
using Mirror.Discovery;


namespace Perrinn424
{

[RequireComponent(typeof(NetworkManager))]
public class NetworkMonitor : MonoBehaviour
	{
	[Header("GameObjects")]
	[Tooltip("These GameObjects will be enabled on servers and hosts. Always disabled on clients.")]
	public GameObject[] serverOnly = new GameObject[0];
	[Tooltip("These GameObjects will be enabled on clients. Always disabled on server and host.")]
	public GameObject[] clientOnly = new GameObject[0];
	[Tooltip("These GameObjects will be enabled on clients when they're connecting to a server. They will be disabled when the client is connected. Always disabled on server and host.")]
	public GameObject[] clientConnecting = new GameObject[0];
	[Tooltip("These GameObjects will be enabled on clients once they're connected to a server. They will be disabled while the client is connecting. Always disabled on server and host.")]
	public GameObject[] clientConnected = new GameObject[0];
	[Header("On-screen widget")]
	public bool debugInfo = false;
	public GUITextBox.Settings debugWidget = new GUITextBox.Settings();
	[Tooltip("Shows debug messages in the console whenever roles or states change")]
	public bool debugConsole = false;

	public enum Role { Undefined, Server, Client, Host }
	public enum State { Undefined, Connecting, Connected }

	// Private fields

	NetworkManager m_manager;
	NetworkDiscovery m_discovery;
	Role m_currentRole = Role.Undefined;
	State m_clientState = State.Undefined;

	GUITextBox m_textBox = new GUITextBox();
	StringBuilder m_text = new StringBuilder(1024);

	// Trick to assign a default font to the GUI box. Configure the default font in the script settings in the Editor.
	[HideInInspector] public Font defaultFont;


	void OnValidate ()
		{
		// Initialize default widget font
		if (debugWidget.font == null)
			debugWidget.font = defaultFont;
		}


	void OnEnable ()
		{
		m_manager = GetComponent<NetworkManager>();
		m_discovery = GetComponent<NetworkDiscovery>();
		m_textBox.settings = debugWidget;
		m_textBox.header = "Connection status              \n";

		m_currentRole = Role.Undefined;
		m_clientState = State.Undefined;

		// Disable all gameobjects

		SetListActive(serverOnly, false);
		SetListActive(clientOnly, false);
		SetListActive(clientConnecting, false);
		SetListActive(clientConnected, false);
		}


	void OnDisable ()
		{
		m_currentRole = Role.Undefined;
		m_clientState = State.Undefined;
		}


	void Update ()
		{
		// Current network states

		bool isServerActive = NetworkServer.active;
		bool isServerAdvertising = m_discovery != null? m_discovery.serverAdvertising : false;

		bool isClientActive = NetworkClient.active;
		bool isClientSearching = m_discovery != null? m_discovery.clientSearching : false;
		bool isClientConnected = NetworkClient.isConnected;
		bool isClientReady = NetworkClient.ready;

		// TODO: Client connecting (NetworkClient.isConnecting)

		// Define new role and state

		Role newRole = Role.Undefined;
		State newState = State.Undefined;

		if (isServerActive && isClientActive)
			newRole = Role.Host;
		else
		if (isServerActive)
			newRole = Role.Server;
		else
		if (isClientActive)
			newRole = Role.Client;

		if (isClientActive)
			{
			if (isClientReady)
				newState = State.Connected;
			else
			if (isClientSearching)
				newState = State.Connecting;
			}

		// Enable / disable the corresponding GameObjects
		//
		// GameObjects are enabled or disabled only once each time the state changes.
		// This allows the GameObjects to take further actions. For example, a isClientReady GameObject may
		// show the connection information (client id, transport, networkAddress, etc.) for some seconds
		// and then disable itself to hide the information.

		if (newRole != m_currentRole)
			{
			SetListActive(serverOnly, newRole == Role.Server || newRole == Role.Host);
			SetListActive(clientOnly, newRole == Role.Client);

			if (debugConsole)
				Debug.Log($"NetworkMonitor: Role changed: {m_currentRole} -> {newRole}");

			m_currentRole = newRole;
			}

		if (newState != m_clientState)
			{
			SetListActive(clientConnecting, newState == State.Connecting);
			SetListActive(clientConnected, newState == State.Connected);

			if (debugConsole)
				Debug.Log($"NetworkMonitor: State changed: {m_clientState} -> {newState}");

			m_clientState = newState;
			}

		// On-screen widget

		if (debugInfo)
			{
			m_text.Clear();
			m_text.Append($"ROLE:               {m_currentRole}\n");
			m_text.Append($"CLIENT:             {m_clientState}\n\n");
			m_text.Append($"Server active:      {isServerActive}\n");
			m_text.Append($"Server advertising: {isServerAdvertising}\n\n");
			m_text.Append($"Client active:      {isClientActive}\n");
			m_text.Append($"Client searching:   {isClientSearching}\n");
			m_text.Append($"Client connected:   {isClientConnected}\n");
			m_text.Append($"Client ready:       {isClientReady}\n\n");

			string strLocalPlayer = NetworkClient.localPlayer != null? "yes" : "no";
			m_text.Append($"Local player:       {strLocalPlayer}\n");
			m_text.Append($"Network address:    {m_manager.networkAddress}\n");
			m_text.Append($"Active transport:   {Transport.activeTransport}");

			m_textBox.text = m_text.ToString();
			}
		}


	void OnGUI ()
		{
		if (debugInfo)
			m_textBox.OnGUI();
		}


	void SetListActive (GameObject[] list, bool active)
		{
		foreach (GameObject go in list)
			go.SetActive(active);
		}
	}

}