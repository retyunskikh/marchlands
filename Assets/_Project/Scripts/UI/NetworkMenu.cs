using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Marchlands.Network;
using Marchlands.Network.Relay;
using Marchlands.Simulation.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// «Сетевая игра» в главном меню. Вкладка «Интернет» — комнаты на сервере Marchlands (VPS): список, создание
    /// с именем и паролем по желанию, вход. Вкладка «Локальная сеть» — серверы, найденные в сети, подключение по IP,
    /// свой сервер. Комната одинакова для обоих случаев (<see cref="ILobby"/>): игроки, карта, стороны, туман,
    /// готовность и мини-чат. Экран строится из кода в стиле главного меню (спрайт и шрифт — с образца).
    /// </summary>
    public sealed class NetworkMenu : MonoBehaviour
    {
        private const string PlayerNameKey = "Marchlands.PlayerName";
        private const string ServerAddressKey = "Marchlands.ServerAddress";
        private const int RowCount = 5;

        [SerializeField] private Button openButton;
        [Tooltip("Образец кнопки: её спрайт и шрифт.")]
        [SerializeField] private Button styleSample;
        [SerializeField] private MainMenu mainMenu;
        [SerializeField] private string battleSceneName = "Battle";

        [Header("Сеть")]
        [Tooltip("Адрес сервера комнат в интернете (IP или домен, можно с «:порт»), пока игрок не ввёл свой.")]
        [SerializeField] private string defaultServerAddress = "";
        [SerializeField] private int port = 7777;
        [SerializeField] private int discoveryPort = 47777;
        [Tooltip("Через сколько миллисекунд без ответа соединение считается оборванным.")]
        [SerializeField] private int disconnectTimeoutMs = 15000;
        [Tooltip("Как часто обновлять список комнат сервера, с.")]
        [SerializeField] private float roomListInterval = 3f;

        [Header("Цвета")]
        [SerializeField] private Color backdropColor = new Color(0.12f, 0.14f, 0.1f, 1f);
        [SerializeField] private Color lightButton = new Color(0.95f, 0.92f, 0.82f);
        [SerializeField] private Color accentButton = new Color(0.95f, 0.78f, 0.55f);
        [SerializeField] private Color grayButton = new Color(0.8f, 0.8f, 0.78f);
        [SerializeField] private Color readyColor = new Color(0.62f, 0.85f, 0.5f);
        [SerializeField] private Color fieldColor = new Color(0.98f, 0.97f, 0.93f);
        [SerializeField] private Color boxColor = new Color(0.18f, 0.21f, 0.16f);
        [SerializeField] private Color darkText = new Color(0.12f, 0.12f, 0.12f);
        [SerializeField] private Color lightText = new Color(0.96f, 0.9f, 0.75f);
        [SerializeField] private Color dimText = new Color(0.8f, 0.8f, 0.72f);
        [SerializeField] private Color errorText = new Color(1f, 0.6f, 0.5f);

        [Header("Тексты")]
        [SerializeField] private string franceName = "Франция";
        [SerializeField] private string austriaName = "Австрия";

        private Sprite sprite;
        private Font font;
        private GameObject overlay;
        private GameObject internet;
        private GameObject lan;
        private GameObject creator;
        private GameObject passwordPrompt;
        private GameObject room;
        private GameObject header;
        private Button internetTab;
        private Button lanTab;
        private Text internetStatus;
        private Text lanStatus;
        private Text createStatus;
        private Text createTitle;
        private Text passwordStatus;
        private Text roomStatus;
        private InputField playerNameField;
        private InputField serverAddressField;
        private InputField lanAddressField;
        private InputField roomNameField;
        private InputField roomPasswordField;
        private InputField joinPasswordField;
        private Text passwordTargetLabel;
        private Text internetEmpty;
        private Text lanEmpty;
        private readonly List<Button> internetRows = new List<Button>();
        private readonly List<Button> lanRows = new List<Button>();
        private readonly List<RelayRoomInfo> shownRooms = new List<RelayRoomInfo>();
        private readonly List<LanServer> shownServers = new List<LanServer>();
        private Text roomTitle;
        private Text roomAddress;
        private Text hostRow;
        private Text guestRow;
        private Image hostRowBox;
        private Image guestRowBox;
        private Text mapLabel;
        private Button mapPrevious;
        private Button mapNext;
        private Button swapButton;
        private Button fogButton;
        private Text fogLabel;
        private Button readyButton;
        private Text readyLabel;
        private Button startButton;
        private Text chatLog;
        private InputField chatField;

        private LanDiscovery discovery;
        private RelayLobby relay;
        private bool relayConnected;
        private float nextRefresh;
        private bool loading;

        // Куда входить после ввода пароля: в комнату сервера или на сервер локальной сети.
        private bool pendingRelay;
        private int pendingRoomId;
        private string pendingAddress;
        private int pendingPort;
        private string pendingName;

        private string PlayerName
        {
            get
            {
                string name = playerNameField != null ? playerNameField.text.Trim() : string.Empty;
                return string.IsNullOrEmpty(name) ? SystemInfo.deviceName : name;
            }
        }

        private void Awake()
        {
            sprite = styleSample.image.sprite;
            font = styleSample.GetComponentInChildren<Text>(true).font;
            openButton.onClick.AddListener(Open);
            // Пока сетевая игра — только между ПК (игра с телефоном не проверена на расхождение расчётов).
            openButton.gameObject.SetActive(Marchlands.View.Platform.IsDesktop);
            Build();
            overlay.SetActive(false);
        }

        private void OnDestroy()
        {
            discovery?.Dispose();
            discovery = null;
            // Ушли в бой — соединение живёт дальше; закрыли меню без матча — закрываем.
            if (!loading && NetworkGame.Setup == null)
            {
                NetworkGame.Shutdown();
            }
        }

        private void Open()
        {
            overlay.SetActive(true);
            OpenInternet();
        }

        private void Close()
        {
            NetworkGame.Shutdown();
            relay = null;
            relayConnected = false;
            discovery?.Dispose();
            discovery = null;
            overlay.SetActive(false);
        }

        private void Update()
        {
            if (!overlay.activeSelf)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            discovery?.Poll(now);
            LobbyHost host = NetworkGame.Host;
            if (host != null && !host.IsStarted && NetworkGame.Session != null)
            {
                discovery?.Announce(now, host.State.ServerName, NetworkGame.Session.Port, host.HasPassword, host.State.HasGuest);
            }

            if (Time.unscaledTime < nextRefresh)
            {
                return;
            }

            if (lan.activeSelf)
            {
                nextRefresh = Time.unscaledTime + 0.5f;
                RefreshLanList();
            }
            else if (internet.activeSelf && relayConnected)
            {
                nextRefresh = Time.unscaledTime + roomListInterval;
                relay.RequestRooms();
            }
        }

        // ---------- Интернет: сервер комнат ----------

        private void OpenInternet()
        {
            StopLan();
            ShowScreen(internet);
            ShowRooms(Array.Empty<RelayRoomInfo>());
            if (relayConnected)
            {
                relay.RequestRooms();
                return;
            }

            ConnectToServer();
        }

        private void ConnectToServer()
        {
            NetworkGame.Shutdown();
            relay = null;
            relayConnected = false;
            string text = serverAddressField.text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                SetStatus(internetStatus, "Введите адрес сервера Marchlands.", true);
                return;
            }

            PlayerPrefs.SetString(ServerAddressKey, text);
            PlayerPrefs.Save();
            SplitAddress(text, out string host, out int targetPort);
            string address = Resolve(host);
            NetworkSession session = address != null ? NetworkSession.Join(address, targetPort, disconnectTimeoutMs) : null;
            if (session == null)
            {
                SetStatus(internetStatus, "Не удалось найти сервер «" + host + "».", true);
                return;
            }

            NetworkGame.Begin(session);
            NetworkGame.IsRelay = true;
            var lobby = new RelayLobby(PlayerName, bytes => session.Send(0, bytes));
            relay = lobby;
            NetworkGame.Lobby = lobby;
            NetworkGame.Handler = (connection, message) => lobby.Receive(message);
            NetworkGame.Connected += connection =>
            {
                relayConnected = true;
                SetStatus(internetStatus, "Подключено к серверу.", false);
                lobby.RequestRooms();
            };
            NetworkGame.Disconnected += connection =>
            {
                bool wasConnected = relayConnected;
                NetworkGame.Shutdown();
                relay = null;
                relayConnected = false;
                if (overlay.activeSelf && !lan.activeSelf)
                {
                    ShowScreen(internet);
                    ShowRooms(Array.Empty<RelayRoomInfo>());
                    SetStatus(internetStatus, wasConnected ? "Связь с сервером потеряна." : "Сервер недоступен.", true);
                }
            };
            lobby.RoomsReceived += ShowRooms;
            lobby.Changed += () =>
            {
                if (!lobby.IsJoined || lobby.State == null)
                {
                    return;
                }

                if (!room.activeSelf)
                {
                    ShowScreen(room);
                    SetStatus(roomStatus, string.Empty, false);
                }

                RefreshRoom();
            };
            lobby.Rejected += reason =>
            {
                if (reason == RejectReason.WrongPassword && pendingRelay)
                {
                    AskPassword("Неверный пароль.");
                    return;
                }

                ShowScreen(internet);
                lobby.RequestRooms();
                SetStatus(internetStatus, RejectText(reason), true);
            };
            lobby.Started += BeginMatch;
            SetStatus(internetStatus, "Подключение к " + host + "…", false);
        }

        private void ShowRooms(IReadOnlyList<RelayRoomInfo> rooms)
        {
            shownRooms.Clear();
            shownRooms.AddRange(rooms);
            for (int i = 0; i < internetRows.Count; i++)
            {
                bool visible = i < shownRooms.Count;
                internetRows[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                RelayRoomInfo info = shownRooms[i];
                internetRows[i].GetComponentInChildren<Text>().text = info.Name + "  —  " + info.HostName
                    + (info.HasPassword ? "   (пароль)" : string.Empty) + (info.IsFull ? "   — занята" : string.Empty);
                internetRows[i].interactable = !info.IsFull;
            }

            internetEmpty.gameObject.SetActive(relayConnected && shownRooms.Count == 0);
        }

        private void OnRoomRow(int index)
        {
            if (relay == null || index >= shownRooms.Count)
            {
                return;
            }

            RelayRoomInfo info = shownRooms[index];
            SavePlayerName();
            relay.PlayerName = PlayerName;
            pendingRelay = true;
            pendingRoomId = info.Id;
            pendingName = info.Name;
            if (info.HasPassword)
            {
                AskPassword(string.Empty);
            }
            else
            {
                relay.Join(info.Id, string.Empty);
            }
        }

        // ---------- Локальная сеть ----------

        private void OpenLan()
        {
            // Соединение с сервером комнат закрываем: в локальной сети — своё.
            NetworkGame.Shutdown();
            relay = null;
            relayConnected = false;
            ShowScreen(lan);
            SetStatus(lanStatus, string.Empty, false);
            discovery ??= new LanDiscovery(discoveryPort);
            if (!discovery.StartListening())
            {
                SetStatus(lanStatus, "Поиск серверов недоступен — подключайтесь по адресу.", true);
            }

            RefreshLanList();
        }

        private void StopLan()
        {
            if (NetworkGame.Host != null || NetworkGame.Guest != null)
            {
                NetworkGame.Shutdown();
            }

            discovery?.Dispose();
            discovery = null;
        }

        private void RefreshLanList()
        {
            shownServers.Clear();
            if (discovery != null)
            {
                shownServers.AddRange(discovery.Servers.OrderBy(server => server.Name));
            }

            for (int i = 0; i < lanRows.Count; i++)
            {
                bool visible = i < shownServers.Count;
                lanRows[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                LanServer server = shownServers[i];
                lanRows[i].GetComponentInChildren<Text>().text = server.Name + (server.HasPassword ? "   (пароль)" : string.Empty)
                    + (server.IsFull ? "   — занят" : string.Empty);
                lanRows[i].interactable = !server.IsFull;
            }

            lanEmpty.gameObject.SetActive(shownServers.Count == 0);
        }

        private void OnLanRow(int index)
        {
            if (index >= shownServers.Count)
            {
                return;
            }

            LanServer server = shownServers[index];
            pendingRelay = false;
            pendingAddress = server.Address;
            pendingPort = server.Port;
            pendingName = server.Name;
            if (server.HasPassword)
            {
                AskPassword(string.Empty);
            }
            else
            {
                ConnectLan(server.Address, server.Port, string.Empty);
            }
        }

        private void OnConnectByAddress()
        {
            SplitAddress(lanAddressField.text.Trim(), out string address, out int targetPort);
            if (string.IsNullOrEmpty(address))
            {
                SetStatus(lanStatus, "Введите адрес сервера, например 192.168.0.10:" + port, true);
                return;
            }

            pendingRelay = false;
            pendingAddress = address;
            pendingPort = targetPort;
            pendingName = string.Empty;
            ConnectLan(address, targetPort, string.Empty);
        }

        private void ConnectLan(string address, int targetPort, string password)
        {
            SavePlayerName();
            NetworkSession session = NetworkSession.Join(Resolve(address) ?? address, targetPort, disconnectTimeoutMs);
            if (session == null)
            {
                SetStatus(lanStatus, "Не удалось разобрать адрес «" + address + "».", true);
                ShowScreen(lan);
                return;
            }

            NetworkGame.Begin(session);
            var guest = new LobbyGuest(PlayerName, bytes => session.Send(0, bytes));
            NetworkGame.Guest = guest;
            NetworkGame.Lobby = guest;
            NetworkGame.Handler = (connection, message) => guest.Receive(message);
            NetworkGame.Connected += connection => guest.Join(password);
            NetworkGame.Disconnected += connection =>
            {
                bool joined = guest.IsJoined;
                NetworkGame.Shutdown();
                ShowScreen(lan);
                SetStatus(lanStatus, joined ? "Сервер закрыт или связь потеряна." : "Не удалось подключиться к " + address + ":" + targetPort + ".", true);
            };
            guest.Changed += () =>
            {
                if (guest.IsJoined && guest.State != null)
                {
                    if (!room.activeSelf)
                    {
                        ShowScreen(room);
                        SetStatus(roomStatus, string.Empty, false);
                    }

                    RefreshRoom();
                }
            };
            guest.Rejected += reason =>
            {
                NetworkGame.Shutdown();
                if (reason == RejectReason.WrongPassword)
                {
                    AskPassword(string.IsNullOrEmpty(password) ? string.Empty : "Неверный пароль.");
                    return;
                }

                ShowScreen(lan);
                SetStatus(lanStatus, RejectText(reason), true);
            };
            guest.Started += BeginMatch;

            ShowScreen(lan);
            SetStatus(lanStatus, "Подключение к " + address + ":" + targetPort + "…", false);
        }

        private void CreateLanServer(string serverName, string password)
        {
            NetworkSession session = NetworkSession.Host(port, disconnectTimeoutMs);
            if (session == null)
            {
                SetStatus(createStatus, $"Порт {port} занят: на этом компьютере уже запущен сервер?", true);
                return;
            }

            NetworkGame.Begin(session);
            var host = new LobbyHost(serverName, password, PlayerName, session);
            host.State.Map = Mathf.Clamp(MapChoice.Selected, 0, Mathf.Max(0, MapNames.Count - 1));
            host.State.FogOfWar = MapChoice.FogOfWar;
            NetworkGame.Host = host;
            NetworkGame.Lobby = host;
            NetworkGame.Handler = host.Receive;
            NetworkGame.Disconnected += host.OnDisconnected;
            host.Changed += RefreshRoom;
            host.Started += BeginMatch;
            ShowScreen(room);
            SetStatus(roomStatus, string.Empty, false);
            RefreshRoom();
        }

        // ---------- Создание комнаты и пароль ----------

        private void OpenCreator()
        {
            SavePlayerName();
            bool internetMode = internet.activeSelf;
            if (internetMode && !relayConnected)
            {
                SetStatus(internetStatus, "Сначала подключитесь к серверу.", true);
                return;
            }

            createTitle.text = internetMode ? "Новая комната" : "Новый сервер в локальной сети";
            roomNameField.text = DefaultRoomName();
            roomPasswordField.text = string.Empty;
            SetStatus(createStatus, string.Empty, false);
            pendingRelay = internetMode;
            ShowScreen(creator);
        }

        private void CreateRoom()
        {
            string roomName = roomNameField.text.Trim();
            if (string.IsNullOrEmpty(roomName))
            {
                roomName = DefaultRoomName();
            }

            if (pendingRelay)
            {
                if (relay == null || !relayConnected)
                {
                    SetStatus(createStatus, "Нет связи с сервером.", true);
                    return;
                }

                relay.PlayerName = PlayerName;
                relay.Create(roomName, roomPasswordField.text);
                SetStatus(createStatus, "Создаём комнату…", false);
                return;
            }

            CreateLanServer(roomName, roomPasswordField.text);
        }

        private void CancelCreator()
        {
            ShowScreen(pendingRelay ? internet : lan);
        }

        private void AskPassword(string error)
        {
            passwordTargetLabel.text = !string.IsNullOrEmpty(pendingName) ? $"«{pendingName}»" : $"{pendingAddress}:{pendingPort}";
            joinPasswordField.text = string.Empty;
            SetStatus(passwordStatus, error, true);
            ShowScreen(passwordPrompt);
        }

        private void SubmitPassword()
        {
            if (pendingRelay)
            {
                relay?.Join(pendingRoomId, joinPasswordField.text);
            }
            else
            {
                ConnectLan(pendingAddress, pendingPort, joinPasswordField.text);
            }
        }

        private void CancelPassword()
        {
            ShowScreen(pendingRelay ? internet : lan);
        }

        // ---------- Комната ----------

        private void RefreshRoom()
        {
            ILobby lobby = NetworkGame.Lobby;
            LobbyState state = lobby?.State;
            if (state == null || !room.activeSelf)
            {
                return;
            }

            bool isHost = lobby.IsHost;
            roomTitle.text = state.ServerName + (lobby.HasPassword ? "  (с паролем)" : string.Empty);
            roomAddress.text = lobby is LobbyHost && NetworkGame.Session != null
                ? "Адрес для подключения: " + LocalAddresses() + ", порт " + NetworkGame.Session.Port
                : NetworkGame.IsRelay ? "Комната на сервере Marchlands" : string.Empty;

            hostRow.text = $"{SideName(state.HostSide)}:  {state.HostName}{(isHost ? " (вы)" : string.Empty)}   {(state.HostReady ? "— готов" : "— не готов")}";
            hostRowBox.color = state.HostReady ? readyColor : grayButton;
            guestRow.text = state.HasGuest
                ? $"{SideName(state.GuestSide)}:  {state.GuestName}{(isHost ? string.Empty : " (вы)")}   {(state.GuestReady ? "— готов" : "— не готов")}"
                : $"{SideName(state.GuestSide)}:  ожидание соперника…";
            guestRowBox.color = state.HasGuest && state.GuestReady ? readyColor : grayButton;

            mapLabel.text = state.Map >= 0 && state.Map < MapNames.Count ? MapNames[state.Map] : "Карта " + (state.Map + 1);
            mapPrevious.gameObject.SetActive(isHost);
            mapNext.gameObject.SetActive(isHost);
            swapButton.interactable = isHost;
            fogButton.interactable = isHost;
            fogLabel.text = state.FogOfWar ? "Туман войны: вкл" : "Туман войны: выкл";

            bool myReady = isHost ? state.HostReady : state.GuestReady;
            readyLabel.text = myReady ? "Не готов" : "Готов";
            readyButton.image.color = myReady ? readyColor : lightButton;
            startButton.gameObject.SetActive(isHost);
            startButton.interactable = isHost && lobby.CanStart;

            List<ChatLine> lines = lobby.Chat.ToList();
            chatLog.text = string.Join("\n", lines.Skip(Math.Max(0, lines.Count - 9)).Select(line => line.Sender + ": " + line.Text));

            if (isHost && !state.HasGuest)
            {
                SetStatus(roomStatus, NetworkGame.IsRelay ? "Комната видна в списке на сервере. Ждём соперника."
                    : "Сервер виден в локальной сети. Ждём соперника.", false);
            }
            else if (isHost && !lobby.CanStart)
            {
                SetStatus(roomStatus, "Начать можно, когда оба готовы.", false);
            }
            else if (!isHost)
            {
                SetStatus(roomStatus, state.HostReady && state.GuestReady ? "Оба готовы — ждём, когда создатель начнёт." : "Отметьте готовность.", false);
            }
            else
            {
                SetStatus(roomStatus, "Все готовы!", false);
            }
        }

        private void ChangeMap(int step)
        {
            ILobby lobby = NetworkGame.Lobby;
            if (lobby?.State == null || !lobby.IsHost || MapNames.Count == 0)
            {
                return;
            }

            lobby.SetMap((lobby.State.Map + step + MapNames.Count) % MapNames.Count);
        }

        private void ToggleReady()
        {
            ILobby lobby = NetworkGame.Lobby;
            if (lobby?.State == null)
            {
                return;
            }

            lobby.SetReady(!(lobby.IsHost ? lobby.State.HostReady : lobby.State.GuestReady));
        }

        private void SendChat()
        {
            string text = chatField.text;
            chatField.text = string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                NetworkGame.Lobby?.SendChat(text);
            }

            chatField.ActivateInputField();
        }

        private void LeaveRoom()
        {
            if (NetworkGame.IsRelay && relay != null)
            {
                // На сервере комнат соединение остаётся — возвращаемся к списку.
                relay.Leave();
                ShowScreen(internet);
                relay.RequestRooms();
                SetStatus(internetStatus, string.Empty, false);
                return;
            }

            NetworkGame.Lobby?.Leave();
            NetworkGame.Shutdown();
            OpenLan();
        }

        private void BeginMatch(MatchSetup setup)
        {
            NetworkGame.Setup = setup;
            loading = true;
            MapChoice.Tutorial = false;
            NetworkGame.Handler = null;
            SceneManager.LoadScene(battleSceneName);
        }

        // ---------- Построение экрана ----------

        private void Build()
        {
            var canvas = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            overlay = Box(canvas, "Сетевая игра", Vector2.zero, Vector2.zero, backdropColor).gameObject;
            var overlayRect = (RectTransform)overlay.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;
            overlay.transform.SetAsLastSibling();
            // Свой слой поверх всего меню (и значков музыки и выхода), фон — сплошной.
            overlay.GetComponent<Image>().sprite = null;
            var overlayCanvas = overlay.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 100;
            overlay.AddComponent<GraphicRaycaster>();

            // Общая шапка списков: заголовок, вкладки, имя игрока.
            header = MakeScreen("Шапка");
            Label(header, "Сетевая игра", new Vector2(0f, 465f), new Vector2(1400f, 90f), 66, lightText, TextAnchor.MiddleCenter, true);
            internetTab = MakeButton(header, "Интернет", new Vector2(-230f, 375f), new Vector2(440f, 80f), lightButton, 40, OpenInternet, true);
            lanTab = MakeButton(header, "Локальная сеть", new Vector2(230f, 375f), new Vector2(440f, 80f), grayButton, 40, OpenLan, true);
            Label(header, "Ваше имя", new Vector2(-470f, 285f), new Vector2(300f, 76f), 36, dimText, TextAnchor.MiddleRight);
            playerNameField = Field(header, new Vector2(30f, 285f), new Vector2(640f, 76f), "Имя игрока", false);
            playerNameField.text = PlayerPrefs.GetString(PlayerNameKey, SystemInfo.deviceName);

            internet = MakeScreen("Интернет");
            Label(internet, "Сервер", new Vector2(-470f, 195f), new Vector2(300f, 76f), 36, dimText, TextAnchor.MiddleRight);
            serverAddressField = Field(internet, new Vector2(-40f, 195f), new Vector2(500f, 76f), "адрес сервера", false);
            serverAddressField.text = PlayerPrefs.GetString(ServerAddressKey, defaultServerAddress);
            MakeButton(internet, "Подключиться", new Vector2(420f, 195f), new Vector2(360f, 76f), lightButton, 34, ConnectToServer);
            for (int i = 0; i < RowCount; i++)
            {
                int index = i;
                internetRows.Add(MakeButton(internet, string.Empty, new Vector2(0f, 105f - i * 82f), new Vector2(1100f, 72f), lightButton, 34,
                    () => OnRoomRow(index)));
            }

            internetEmpty = Label(internet, "Комнат пока нет — создайте свою.", new Vector2(0f, 30f), new Vector2(1200f, 60f), 34, dimText,
                TextAnchor.MiddleCenter);
            internetStatus = Label(internet, string.Empty, new Vector2(0f, -340f), new Vector2(1500f, 60f), 34, dimText, TextAnchor.MiddleCenter);
            MakeButton(internet, "Создать комнату", new Vector2(-250f, -450f), new Vector2(440f, 100f), accentButton, 44, OpenCreator, true);
            MakeButton(internet, "Назад", new Vector2(250f, -450f), new Vector2(440f, 100f), grayButton, 44, Close);

            lan = MakeScreen("Локальная сеть");
            for (int i = 0; i < RowCount; i++)
            {
                int index = i;
                lanRows.Add(MakeButton(lan, string.Empty, new Vector2(0f, 195f - i * 82f), new Vector2(1100f, 72f), lightButton, 34,
                    () => OnLanRow(index)));
            }

            lanEmpty = Label(lan, "Серверов в сети пока нет — создайте свой или подключитесь по адресу.", new Vector2(0f, 120f),
                new Vector2(1300f, 60f), 34, dimText, TextAnchor.MiddleCenter);
            Label(lan, "Адрес", new Vector2(-560f, -250f), new Vector2(200f, 76f), 36, dimText, TextAnchor.MiddleRight);
            lanAddressField = Field(lan, new Vector2(-130f, -250f), new Vector2(620f, 76f), "192.168.0.10:" + port, false);
            MakeButton(lan, "Подключиться", new Vector2(390f, -250f), new Vector2(380f, 76f), lightButton, 36, OnConnectByAddress);
            lanStatus = Label(lan, string.Empty, new Vector2(0f, -340f), new Vector2(1500f, 60f), 34, dimText, TextAnchor.MiddleCenter);
            MakeButton(lan, "Создать сервер", new Vector2(-250f, -450f), new Vector2(440f, 100f), accentButton, 44, OpenCreator, true);
            MakeButton(lan, "Назад", new Vector2(250f, -450f), new Vector2(440f, 100f), grayButton, 44, Close);

            creator = MakeScreen("Создание");
            createTitle = Label(creator, "Новая комната", new Vector2(0f, 400f), new Vector2(1600f, 100f), 66, lightText, TextAnchor.MiddleCenter, true);
            Label(creator, "Название", new Vector2(-470f, 200f), new Vector2(400f, 80f), 38, dimText, TextAnchor.MiddleRight);
            roomNameField = Field(creator, new Vector2(150f, 200f), new Vector2(800f, 80f), "Название", false);
            Label(creator, "Пароль", new Vector2(-470f, 80f), new Vector2(400f, 80f), 38, dimText, TextAnchor.MiddleRight);
            roomPasswordField = Field(creator, new Vector2(150f, 80f), new Vector2(800f, 80f), "необязательно", true);
            Label(creator, "Без пароля войти может любой.", new Vector2(150f, 0f), new Vector2(800f, 60f), 30, dimText, TextAnchor.MiddleLeft);
            createStatus = Label(creator, string.Empty, new Vector2(0f, -150f), new Vector2(1400f, 60f), 34, dimText, TextAnchor.MiddleCenter);
            MakeButton(creator, "Создать", new Vector2(-250f, -450f), new Vector2(440f, 100f), accentButton, 44, CreateRoom, true);
            MakeButton(creator, "Отмена", new Vector2(250f, -450f), new Vector2(440f, 100f), grayButton, 44, CancelCreator);

            passwordPrompt = MakeScreen("Пароль");
            Label(passwordPrompt, "Нужен пароль", new Vector2(0f, 300f), new Vector2(1400f, 100f), 66, lightText, TextAnchor.MiddleCenter, true);
            passwordTargetLabel = Label(passwordPrompt, string.Empty, new Vector2(0f, 170f), new Vector2(1400f, 60f), 40, dimText, TextAnchor.MiddleCenter);
            joinPasswordField = Field(passwordPrompt, new Vector2(0f, 60f), new Vector2(700f, 80f), "Пароль", true);
            joinPasswordField.onSubmit.AddListener(_ => SubmitPassword());
            passwordStatus = Label(passwordPrompt, string.Empty, new Vector2(0f, -40f), new Vector2(1400f, 60f), 34, errorText, TextAnchor.MiddleCenter);
            MakeButton(passwordPrompt, "Войти", new Vector2(-250f, -250f), new Vector2(440f, 100f), accentButton, 44, SubmitPassword, true);
            MakeButton(passwordPrompt, "Отмена", new Vector2(250f, -250f), new Vector2(440f, 100f), grayButton, 44, CancelPassword);

            room = MakeScreen("Комната");
            roomTitle = Label(room, string.Empty, new Vector2(0f, 455f), new Vector2(1700f, 90f), 60, lightText, TextAnchor.MiddleCenter, true);
            roomAddress = Label(room, string.Empty, new Vector2(0f, 390f), new Vector2(1700f, 50f), 30, dimText, TextAnchor.MiddleCenter);
            Label(room, "Игроки", new Vector2(-430f, 310f), new Vector2(760f, 60f), 40, lightText, TextAnchor.MiddleLeft);
            hostRowBox = Box(room.transform, "Создатель", new Vector2(-430f, 230f), new Vector2(760f, 80f), grayButton);
            hostRow = Label(hostRowBox.gameObject, string.Empty, Vector2.zero, new Vector2(720f, 80f), 34, darkText, TextAnchor.MiddleLeft);
            guestRowBox = Box(room.transform, "Соперник", new Vector2(-430f, 140f), new Vector2(760f, 80f), grayButton);
            guestRow = Label(guestRowBox.gameObject, string.Empty, Vector2.zero, new Vector2(720f, 80f), 34, darkText, TextAnchor.MiddleLeft);
            Label(room, "Карта", new Vector2(-430f, 50f), new Vector2(760f, 50f), 34, dimText, TextAnchor.MiddleLeft);
            mapPrevious = MakeButton(room, "‹", new Vector2(-760f, -20f), new Vector2(90f, 80f), lightButton, 50, () => ChangeMap(-1));
            mapLabel = Label(room, string.Empty, new Vector2(-430f, -20f), new Vector2(560f, 80f), 40, lightText, TextAnchor.MiddleCenter, true);
            mapNext = MakeButton(room, "›", new Vector2(-100f, -20f), new Vector2(90f, 80f), lightButton, 50, () => ChangeMap(1));
            swapButton = MakeButton(room, "Поменять стороны", new Vector2(-430f, -120f), new Vector2(760f, 80f), lightButton, 36,
                () => NetworkGame.Lobby?.SwapSides());
            fogButton = MakeButton(room, "Туман войны: вкл", new Vector2(-430f, -215f), new Vector2(760f, 80f), lightButton, 36, () =>
            {
                ILobby lobby = NetworkGame.Lobby;
                if (lobby?.State != null)
                {
                    lobby.SetFogOfWar(!lobby.State.FogOfWar);
                }
            });
            fogLabel = fogButton.GetComponentInChildren<Text>();

            Label(room, "Чат", new Vector2(430f, 310f), new Vector2(760f, 60f), 40, lightText, TextAnchor.MiddleLeft);
            Image logBox = Box(room.transform, "Лента чата", new Vector2(430f, 65f), new Vector2(760f, 400f), boxColor);
            chatLog = Label(logBox.gameObject, string.Empty, Vector2.zero, new Vector2(720f, 380f), 30, lightText, TextAnchor.LowerLeft);
            chatLog.horizontalOverflow = HorizontalWrapMode.Wrap;
            chatLog.verticalOverflow = VerticalWrapMode.Truncate;
            chatField = Field(room, new Vector2(330f, -190f), new Vector2(560f, 80f), "Сообщение…", false);
            chatField.characterLimit = 200;
            chatField.onSubmit.AddListener(_ => SendChat());
            MakeButton(room, "Отправить", new Vector2(715f, -190f), new Vector2(190f, 80f), lightButton, 30, SendChat);

            roomStatus = Label(room, string.Empty, new Vector2(0f, -320f), new Vector2(1600f, 60f), 34, dimText, TextAnchor.MiddleCenter);
            readyButton = MakeButton(room, "Готов", new Vector2(-500f, -440f), new Vector2(420f, 100f), lightButton, 46, ToggleReady, true);
            readyLabel = readyButton.GetComponentInChildren<Text>();
            startButton = MakeButton(room, "Начать", new Vector2(0f, -440f), new Vector2(420f, 100f), accentButton, 46,
                () => NetworkGame.Lobby?.StartMatch(), true);
            MakeButton(room, "Выйти", new Vector2(500f, -440f), new Vector2(420f, 100f), grayButton, 46, LeaveRoom);
        }

        private GameObject MakeScreen(string screenName)
        {
            var screen = new GameObject(screenName, typeof(RectTransform));
            var rect = (RectTransform)screen.transform;
            rect.SetParent(overlay.transform, false);
            rect.sizeDelta = new Vector2(1920f, 1080f);
            return screen;
        }

        private void ShowScreen(GameObject screen)
        {
            internet.SetActive(screen == internet);
            lan.SetActive(screen == lan);
            creator.SetActive(screen == creator);
            passwordPrompt.SetActive(screen == passwordPrompt);
            room.SetActive(screen == room);
            bool list = screen == internet || screen == lan;
            header.SetActive(list);
            internetTab.image.color = screen == internet ? accentButton : grayButton;
            lanTab.image.color = screen == lan ? accentButton : grayButton;
        }

        private Image Box(Transform parent, string boxName, Vector2 position, Vector2 size, Color color)
        {
            var box = new GameObject(boxName, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)box.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = box.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private Text Label(GameObject parent, string text, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment,
            bool bold = false)
        {
            var label = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)label.transform;
            rect.SetParent(parent.transform, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var component = label.GetComponent<Text>();
            component.font = font;
            component.fontSize = fontSize;
            component.color = color;
            component.alignment = alignment;
            component.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            component.horizontalOverflow = HorizontalWrapMode.Overflow;
            component.text = text;
            component.raycastTarget = false;
            return component;
        }

        private Button MakeButton(GameObject parent, string text, Vector2 position, Vector2 size, Color color, int fontSize, Action onClick,
            bool bold = false)
        {
            Image image = Box(parent.transform, text, position, size, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            Label(image.gameObject, text, Vector2.zero, size, fontSize, darkText, TextAnchor.MiddleCenter, bold);
            return button;
        }

        private InputField Field(GameObject parent, Vector2 position, Vector2 size, string placeholder, bool password)
        {
            Image image = Box(parent.transform, "Поле", position, size, fieldColor);
            var field = image.gameObject.AddComponent<InputField>();
            Text placeholderText = Label(image.gameObject, placeholder, Vector2.zero, size - new Vector2(40f, 0f), 34,
                new Color(darkText.r, darkText.g, darkText.b, 0.4f), TextAnchor.MiddleLeft);
            placeholderText.fontStyle = FontStyle.Italic;
            Text text = Label(image.gameObject, string.Empty, Vector2.zero, size - new Vector2(40f, 0f), 34, darkText, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            field.textComponent = text;
            field.placeholder = placeholderText;
            field.targetGraphic = image;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 64;
            if (password)
            {
                field.contentType = InputField.ContentType.Password;
            }

            return field;
        }

        private void SetStatus(Text label, string text, bool error)
        {
            label.text = text;
            label.color = error ? errorText : dimText;
        }

        private void SavePlayerName()
        {
            PlayerPrefs.SetString(PlayerNameKey, PlayerName);
            PlayerPrefs.Save();
        }

        private IReadOnlyList<string> MapNames => mainMenu != null ? mainMenu.MapNames : Array.Empty<string>();

        private string SideName(Side side)
        {
            return side == Side.France ? franceName : austriaName;
        }

        private static string DefaultRoomName()
        {
            return SystemInfo.deviceName + " " + DateTime.Now.ToString("mm:ss");
        }

        private static string RejectText(RejectReason reason)
        {
            switch (reason)
            {
                case RejectReason.WrongPassword:
                    return "Неверный пароль.";
                case RejectReason.Full:
                    return "В комнате уже два игрока.";
                case RejectReason.InGame:
                    return "Там уже идёт бой.";
                case RejectReason.RoomNotFound:
                    return "Комнаты уже нет.";
                case RejectReason.RoomClosed:
                    return "Создатель закрыл комнату.";
                case RejectReason.TooManyRooms:
                    return "На сервере слишком много комнат — попробуйте позже.";
                default:
                    return "Другая версия игры — обновите игру.";
            }
        }

        /// <summary>«хост:порт» или «хост» (порт по умолчанию).</summary>
        private void SplitAddress(string text, out string host, out int targetPort)
        {
            host = text;
            targetPort = port;
            int colon = text.LastIndexOf(':');
            if (colon > 0 && int.TryParse(text.Substring(colon + 1), out int parsed))
            {
                host = text.Substring(0, colon);
                targetPort = parsed;
            }
        }

        /// <summary>IPv4-адрес по IP или имени (домену); null — не найден.</summary>
        private static string Resolve(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return null;
            }

            if (IPAddress.TryParse(host, out IPAddress parsed))
            {
                return parsed.ToString();
            }

            try
            {
                IPAddress address = Dns.GetHostAddresses(host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                return address?.ToString();
            }
            catch (SocketException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string LocalAddresses()
        {
            try
            {
                IEnumerable<string> addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    .Select(address => address.ToString());
                string joined = string.Join(", ", addresses);
                return string.IsNullOrEmpty(joined) ? "127.0.0.1" : joined;
            }
            catch (SocketException)
            {
                return "127.0.0.1";
            }
        }
    }
}
