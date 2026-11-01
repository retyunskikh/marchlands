using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Marchlands.Network;
using Marchlands.Simulation.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Marchlands.UI
{
    /// <summary>
    /// «Сетевая игра» в главном меню: список серверов локальной сети, подключение по IP, создание сервера (имя, пароль
    /// по желанию), комната с игроками, картой, сторонами, туманом, готовностью и мини-чатом. Экран строится из кода
    /// в стиле главного меню (спрайт, шрифт и цвета берутся с образца — кнопки «Обучение»).
    /// </summary>
    public sealed class NetworkMenu : MonoBehaviour
    {
        private const string PlayerNameKey = "Marchlands.PlayerName";

        [SerializeField] private Button openButton;
        [Tooltip("Образец кнопки: её спрайт и шрифт.")]
        [SerializeField] private Button styleSample;
        [SerializeField] private MainMenu mainMenu;
        [SerializeField] private string battleSceneName = "Battle";

        [Header("Сеть")]
        [SerializeField] private int port = 7777;
        [SerializeField] private int discoveryPort = 47777;
        [Tooltip("Через сколько миллисекунд без ответа соединение считается оборванным.")]
        [SerializeField] private int disconnectTimeoutMs = 15000;

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
        private GameObject browser;
        private GameObject creator;
        private GameObject passwordPrompt;
        private GameObject room;
        private Text browserStatus;
        private Text createStatus;
        private Text passwordStatus;
        private Text roomStatus;
        private InputField playerNameField;
        private InputField addressField;
        private InputField serverNameField;
        private InputField serverPasswordField;
        private InputField joinPasswordField;
        private Text passwordServerLabel;
        private Text emptyListLabel;
        private readonly List<Button> serverRows = new List<Button>();
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
        private float nextListRefresh;
        private string pendingAddress;
        private int pendingPort;
        private string pendingServerName;
        private bool loading;

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
            ShowScreen(browser);
            SetStatus(browserStatus, string.Empty, false);
            discovery ??= new LanDiscovery(discoveryPort);
            if (!discovery.StartListening())
            {
                SetStatus(browserStatus, "Поиск серверов недоступен — подключайтесь по адресу.", true);
            }
        }

        private void Close()
        {
            NetworkGame.Shutdown();
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

            if (browser.activeSelf && Time.unscaledTime >= nextListRefresh)
            {
                nextListRefresh = Time.unscaledTime + 0.5f;
                RefreshServerList();
            }
        }

        // ---------- Список серверов и подключение ----------

        private void RefreshServerList()
        {
            shownServers.Clear();
            if (discovery != null)
            {
                shownServers.AddRange(discovery.Servers.OrderBy(server => server.Name));
            }

            for (int i = 0; i < serverRows.Count; i++)
            {
                bool visible = i < shownServers.Count;
                serverRows[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                LanServer server = shownServers[i];
                string text = server.Name + (server.HasPassword ? "   (пароль)" : string.Empty) + (server.IsFull ? "   — занят" : string.Empty);
                serverRows[i].GetComponentInChildren<Text>().text = text;
                serverRows[i].interactable = !server.IsFull;
            }

            emptyListLabel.gameObject.SetActive(shownServers.Count == 0);
        }

        private void OnServerRow(int index)
        {
            if (index >= shownServers.Count)
            {
                return;
            }

            LanServer server = shownServers[index];
            if (server.HasPassword)
            {
                AskPassword(server.Address, server.Port, server.Name, string.Empty);
            }
            else
            {
                Connect(server.Address, server.Port, string.Empty);
            }
        }

        private void OnConnectByAddress()
        {
            string text = addressField.text.Trim();
            string address = text;
            int targetPort = port;
            int colon = text.LastIndexOf(':');
            if (colon > 0 && int.TryParse(text.Substring(colon + 1), out int parsed))
            {
                address = text.Substring(0, colon);
                targetPort = parsed;
            }

            if (string.IsNullOrEmpty(address))
            {
                SetStatus(browserStatus, "Введите адрес сервера, например 192.168.0.10:" + port, true);
                return;
            }

            Connect(address, targetPort, string.Empty);
        }

        private void AskPassword(string address, int targetPort, string serverName, string error)
        {
            pendingAddress = address;
            pendingPort = targetPort;
            pendingServerName = serverName;
            passwordServerLabel.text = string.IsNullOrEmpty(serverName) ? $"Сервер {address}:{targetPort}" : $"Сервер «{serverName}»";
            joinPasswordField.text = string.Empty;
            SetStatus(passwordStatus, error, true);
            ShowScreen(passwordPrompt);
        }

        private void Connect(string address, int targetPort, string password)
        {
            SavePlayerName();
            NetworkSession session = NetworkSession.Join(address, targetPort, disconnectTimeoutMs);
            if (session == null)
            {
                SetStatus(browserStatus, "Не удалось разобрать адрес «" + address + "».", true);
                ShowScreen(browser);
                return;
            }

            NetworkGame.Begin(session);
            var guest = new LobbyGuest(PlayerName, bytes => session.Send(0, bytes));
            NetworkGame.Guest = guest;
            NetworkGame.Handler = (connection, message) => guest.Receive(message);
            NetworkGame.Connected += connection => guest.Join(password);
            NetworkGame.Disconnected += connection =>
            {
                bool joined = guest.IsJoined;
                NetworkGame.Shutdown();
                ShowScreen(browser);
                SetStatus(browserStatus, joined ? "Сервер закрыт или связь потеряна." : "Не удалось подключиться к " + address + ":" + targetPort + ".", true);
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
                    AskPassword(address, targetPort, pendingServerName, string.IsNullOrEmpty(password) ? string.Empty : "Неверный пароль.");
                    return;
                }

                ShowScreen(browser);
                SetStatus(browserStatus, reason == RejectReason.Full ? "На сервере уже два игрока."
                    : reason == RejectReason.InGame ? "На сервере уже идёт бой."
                    : "Другая версия игры — обновите обе копии.", true);
            };
            guest.Started += setup =>
            {
                NetworkGame.Setup = setup;
                LoadBattle();
            };

            ShowScreen(browser);
            SetStatus(browserStatus, "Подключение к " + address + ":" + targetPort + "…", false);
        }

        // ---------- Создание сервера ----------

        private void OpenCreator()
        {
            SavePlayerName();
            serverNameField.text = SystemInfo.deviceName + " " + DateTime.Now.ToString("mm:ss");
            serverPasswordField.text = string.Empty;
            SetStatus(createStatus, string.Empty, false);
            ShowScreen(creator);
        }

        private void CreateServer()
        {
            string serverName = serverNameField.text.Trim();
            if (string.IsNullOrEmpty(serverName))
            {
                serverName = SystemInfo.deviceName + " " + DateTime.Now.ToString("mm:ss");
            }

            NetworkSession session = NetworkSession.Host(port, disconnectTimeoutMs);
            if (session == null)
            {
                SetStatus(createStatus, $"Порт {port} занят: на этом компьютере уже запущен сервер?", true);
                return;
            }

            NetworkGame.Begin(session);
            var host = new LobbyHost(serverName, serverPasswordField.text, PlayerName, session);
            host.State.Map = Mathf.Clamp(MapChoice.Selected, 0, Mathf.Max(0, MapNames.Count - 1));
            host.State.FogOfWar = MapChoice.FogOfWar;
            NetworkGame.Host = host;
            NetworkGame.Handler = host.Receive;
            NetworkGame.Disconnected += host.OnDisconnected;
            host.Changed += RefreshRoom;
            ShowScreen(room);
            SetStatus(roomStatus, string.Empty, false);
            RefreshRoom();
        }

        // ---------- Комната ----------

        private void RefreshRoom()
        {
            LobbyHost host = NetworkGame.Host;
            LobbyGuest guest = NetworkGame.Guest;
            LobbyState state = host != null ? host.State : guest?.State;
            if (state == null || !room.activeSelf)
            {
                return;
            }

            bool isHost = host != null;
            roomTitle.text = state.ServerName + (isHost && host.HasPassword ? "  (с паролем)" : string.Empty);
            roomAddress.text = isHost ? "Адрес для подключения: " + LocalAddresses() + ", порт " + NetworkGame.Session.Port : string.Empty;

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
            startButton.interactable = isHost && host.CanStart;

            List<ChatLine> lines = (isHost ? host.Chat : guest.Chat).ToList();
            chatLog.text = string.Join("\n", lines.Skip(Math.Max(0, lines.Count - 9)).Select(line => line.Sender + ": " + line.Text));

            if (isHost && !state.HasGuest)
            {
                SetStatus(roomStatus, "Сервер виден в локальной сети. Ждём соперника.", false);
            }
            else if (isHost && !host.CanStart)
            {
                SetStatus(roomStatus, "Начать можно, когда оба готовы.", false);
            }
            else if (!isHost)
            {
                SetStatus(roomStatus, state.HostReady && state.GuestReady ? "Оба готовы — ждём, когда хост начнёт." : "Отметьте готовность.", false);
            }
            else
            {
                SetStatus(roomStatus, "Все готовы!", false);
            }
        }

        private void ChangeMap(int step)
        {
            LobbyHost host = NetworkGame.Host;
            if (host == null || MapNames.Count == 0)
            {
                return;
            }

            host.SetMap((host.State.Map + step + MapNames.Count) % MapNames.Count);
        }

        private void ToggleReady()
        {
            if (NetworkGame.Host != null)
            {
                NetworkGame.Host.SetReady(!NetworkGame.Host.State.HostReady);
            }
            else if (NetworkGame.Guest?.State != null)
            {
                NetworkGame.Guest.SetReady(!NetworkGame.Guest.State.GuestReady);
            }
        }

        private void SendChat()
        {
            string text = chatField.text;
            chatField.text = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (NetworkGame.Host != null)
            {
                NetworkGame.Host.SendChat(text);
            }
            else
            {
                NetworkGame.Guest?.SendChat(text);
            }

            chatField.ActivateInputField();
        }

        private void StartMatch()
        {
            LobbyHost host = NetworkGame.Host;
            MatchSetup setup = host?.Start(unchecked((ulong)DateTime.Now.Ticks));
            if (setup == null)
            {
                return;
            }

            NetworkGame.Setup = setup;
            LoadBattle();
        }

        private void LeaveRoom()
        {
            NetworkGame.Guest?.Leave();
            NetworkGame.Shutdown();
            ShowScreen(browser);
            SetStatus(browserStatus, string.Empty, false);
        }

        private void LoadBattle()
        {
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

            browser = MakeScreen("Список");
            Label(browser, "Сетевая игра", new Vector2(0f, 450f), new Vector2(1400f, 100f), 70, lightText, TextAnchor.MiddleCenter, true);
            Label(browser, "Ваше имя", new Vector2(-470f, 350f), new Vector2(300f, 80f), 38, dimText, TextAnchor.MiddleRight);
            playerNameField = Field(browser, new Vector2(30f, 350f), new Vector2(640f, 80f), "Имя игрока", false);
            playerNameField.text = PlayerPrefs.GetString(PlayerNameKey, SystemInfo.deviceName);
            Label(browser, "Серверы в локальной сети", new Vector2(0f, 265f), new Vector2(1200f, 60f), 40, lightText, TextAnchor.MiddleCenter);
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                serverRows.Add(MakeButton(browser, string.Empty, new Vector2(0f, 190f - i * 88f), new Vector2(1100f, 78f), lightButton, 36,
                    () => OnServerRow(index)));
            }

            emptyListLabel = Label(browser, "Серверов пока нет — создайте свой или подключитесь по адресу.", new Vector2(0f, 100f),
                new Vector2(1200f, 60f), 34, dimText, TextAnchor.MiddleCenter);
            Label(browser, "Адрес", new Vector2(-560f, -270f), new Vector2(200f, 80f), 38, dimText, TextAnchor.MiddleRight);
            addressField = Field(browser, new Vector2(-130f, -270f), new Vector2(620f, 80f), "192.168.0.10:" + port, false);
            MakeButton(browser, "Подключиться", new Vector2(390f, -270f), new Vector2(380f, 80f), lightButton, 38, OnConnectByAddress);
            browserStatus = Label(browser, string.Empty, new Vector2(0f, -355f), new Vector2(1400f, 60f), 34, dimText, TextAnchor.MiddleCenter);
            MakeButton(browser, "Создать сервер", new Vector2(-250f, -450f), new Vector2(440f, 100f), accentButton, 44, OpenCreator, true);
            MakeButton(browser, "Назад", new Vector2(250f, -450f), new Vector2(440f, 100f), grayButton, 44, Close);

            creator = MakeScreen("Создание");
            Label(creator, "Новый сервер", new Vector2(0f, 400f), new Vector2(1400f, 100f), 70, lightText, TextAnchor.MiddleCenter, true);
            Label(creator, "Имя сервера", new Vector2(-470f, 200f), new Vector2(400f, 80f), 38, dimText, TextAnchor.MiddleRight);
            serverNameField = Field(creator, new Vector2(150f, 200f), new Vector2(800f, 80f), "Имя сервера", false);
            Label(creator, "Пароль", new Vector2(-470f, 80f), new Vector2(400f, 80f), 38, dimText, TextAnchor.MiddleRight);
            serverPasswordField = Field(creator, new Vector2(150f, 80f), new Vector2(800f, 80f), "необязательно", true);
            Label(creator, "Без пароля подключиться может любой в вашей сети.", new Vector2(150f, 0f), new Vector2(800f, 60f), 30, dimText,
                TextAnchor.MiddleLeft);
            createStatus = Label(creator, string.Empty, new Vector2(0f, -150f), new Vector2(1400f, 60f), 34, dimText, TextAnchor.MiddleCenter);
            MakeButton(creator, "Создать", new Vector2(-250f, -450f), new Vector2(440f, 100f), accentButton, 44, CreateServer, true);
            MakeButton(creator, "Отмена", new Vector2(250f, -450f), new Vector2(440f, 100f), grayButton, 44, () => ShowScreen(browser));

            passwordPrompt = MakeScreen("Пароль");
            Label(passwordPrompt, "Нужен пароль", new Vector2(0f, 300f), new Vector2(1400f, 100f), 70, lightText, TextAnchor.MiddleCenter, true);
            passwordServerLabel = Label(passwordPrompt, string.Empty, new Vector2(0f, 170f), new Vector2(1400f, 60f), 40, dimText, TextAnchor.MiddleCenter);
            joinPasswordField = Field(passwordPrompt, new Vector2(0f, 60f), new Vector2(700f, 80f), "Пароль", true);
            joinPasswordField.onSubmit.AddListener(_ => Connect(pendingAddress, pendingPort, joinPasswordField.text));
            passwordStatus = Label(passwordPrompt, string.Empty, new Vector2(0f, -40f), new Vector2(1400f, 60f), 34, errorText, TextAnchor.MiddleCenter);
            MakeButton(passwordPrompt, "Войти", new Vector2(-250f, -250f), new Vector2(440f, 100f), accentButton, 44,
                () => Connect(pendingAddress, pendingPort, joinPasswordField.text), true);
            MakeButton(passwordPrompt, "Отмена", new Vector2(250f, -250f), new Vector2(440f, 100f), grayButton, 44, () => ShowScreen(browser));

            room = MakeScreen("Комната");
            roomTitle = Label(room, string.Empty, new Vector2(0f, 455f), new Vector2(1700f, 90f), 60, lightText, TextAnchor.MiddleCenter, true);
            roomAddress = Label(room, string.Empty, new Vector2(0f, 390f), new Vector2(1700f, 50f), 30, dimText, TextAnchor.MiddleCenter);
            Label(room, "Игроки", new Vector2(-430f, 310f), new Vector2(760f, 60f), 40, lightText, TextAnchor.MiddleLeft);
            hostRowBox = Box(room.transform, "Хост", new Vector2(-430f, 230f), new Vector2(760f, 80f), grayButton);
            hostRow = Label(hostRowBox.gameObject, string.Empty, Vector2.zero, new Vector2(720f, 80f), 34, darkText, TextAnchor.MiddleLeft);
            guestRowBox = Box(room.transform, "Гость", new Vector2(-430f, 140f), new Vector2(760f, 80f), grayButton);
            guestRow = Label(guestRowBox.gameObject, string.Empty, Vector2.zero, new Vector2(720f, 80f), 34, darkText, TextAnchor.MiddleLeft);
            Label(room, "Карта", new Vector2(-430f, 50f), new Vector2(760f, 50f), 34, dimText, TextAnchor.MiddleLeft);
            mapPrevious = MakeButton(room, "‹", new Vector2(-760f, -20f), new Vector2(90f, 80f), lightButton, 50, () => ChangeMap(-1));
            mapLabel = Label(room, string.Empty, new Vector2(-430f, -20f), new Vector2(560f, 80f), 40, lightText, TextAnchor.MiddleCenter, true);
            mapNext = MakeButton(room, "›", new Vector2(-100f, -20f), new Vector2(90f, 80f), lightButton, 50, () => ChangeMap(1));
            swapButton = MakeButton(room, "Поменять стороны", new Vector2(-430f, -120f), new Vector2(760f, 80f), lightButton, 36,
                () => NetworkGame.Host?.SwapSides());
            fogButton = MakeButton(room, "Туман войны: вкл", new Vector2(-430f, -215f), new Vector2(760f, 80f), lightButton, 36,
                () => NetworkGame.Host?.SetFogOfWar(!NetworkGame.Host.State.FogOfWar));
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
            startButton = MakeButton(room, "Начать", new Vector2(0f, -440f), new Vector2(420f, 100f), accentButton, 46, StartMatch, true);
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
            browser.SetActive(screen == browser);
            creator.SetActive(screen == creator);
            passwordPrompt.SetActive(screen == passwordPrompt);
            room.SetActive(screen == room);
            if (screen == browser)
            {
                RefreshServerList();
            }
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
            field.characterLimit = 48;
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
