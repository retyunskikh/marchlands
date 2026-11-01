# Сервер комнат Marchlands на VPS (Ubuntu)

Сервер хранит комнаты и пересылает сообщения между двумя игроками комнаты. Бой он не считает:
lockstep остаётся на машинах игроков. Нагрузка — несколько килобайт в секунду на матч, хватит
самого дешёвого VPS (1 ядро, 512 МБ – 1 ГБ).

## 1. Сборка

В редакторе: **Marchlands → Сборка сервера (Linux)**. Получится папка
`Builds/Server/Marchlands-Server-N/` с файлом `MarchlandsServer.x86_64`.

Версия протокола (`LobbyProtocol.Version`) у сервера и игры должна совпадать. После изменений
в протоколе или симуляции обновляйте сервер вместе с игрой.

## 2. Первая установка

Подключитесь к серверу (`ssh root@137.22.145.254`) и выполните:

```bash
# Пользователь без прав входа, от которого работает сервер
adduser --system --group --no-create-home marchlands
mkdir -p /opt/marchlands/server
chown -R marchlands:marchlands /opt/marchlands

# Порт игры (UDP). Если ufw ещё не включён — сначала разрешите SSH, иначе потеряете доступ.
ufw allow OpenSSH
ufw allow 7777/udp
ufw enable
```

Если у провайдера VPS есть свой файрвол (панель управления, «группы безопасности»), откройте там
тоже **UDP 7777** на вход.

С вашего компьютера (PowerShell или Git Bash) скопируйте сборку и файл сервиса:

```bash
scp -r Builds/Server/Marchlands-Server-N/* root@137.22.145.254:/opt/marchlands/server/
scp deploy/marchlands-server.service root@137.22.145.254:/etc/systemd/system/
```

На сервере:

```bash
chown -R marchlands:marchlands /opt/marchlands
chmod +x /opt/marchlands/server/MarchlandsServer.x86_64
systemctl daemon-reload
systemctl enable --now marchlands-server
systemctl status marchlands-server
```

В статусе должно быть `active (running)`, а в логе — строка
`Сервер Marchlands запущен: порт 7777/UDP …`.

## 3. Лог и управление

```bash
journalctl -u marchlands-server -f          # лог в реальном времени (комнаты, подключения)
journalctl -u marchlands-server --since today
systemctl restart marchlands-server         # перезапуск
systemctl stop marchlands-server            # остановка
```

Сервер сам поднимается после перезагрузки VPS и перезапускается при падении.

## 4. Обновление

```bash
# на компьютере
scp -r Builds/Server/Marchlands-Server-N/* root@137.22.145.254:/tmp/marchlands-new/
# на сервере
systemctl stop marchlands-server
rm -rf /opt/marchlands/server/*
cp -r /tmp/marchlands-new/* /opt/marchlands/server/
chown -R marchlands:marchlands /opt/marchlands
chmod +x /opt/marchlands/server/MarchlandsServer.x86_64
systemctl start marchlands-server
```

## 5. Параметры

В строке `ExecStart` файла сервиса:

- `-port 7777` — порт UDP (сменили — откройте его в ufw и укажите в игре как `адрес:порт`);
- `-maxRooms 500` — сколько комнат может быть одновременно.

После правки файла сервиса: `systemctl daemon-reload && systemctl restart marchlands-server`.

## 6. Адрес в игре

«Сетевая игра» → вкладка «Интернет» → поле «Сервер»: IP или домен VPS (порт можно не писать, если
он 7777). Адрес запоминается. Когда адрес постоянный, его можно вписать в игру по умолчанию — поле
`defaultServerAddress` компонента `NetworkMenu` в сцене `MainMenu`.

## 7. Если не подключается

- `systemctl status marchlands-server` — сервер запущен?
- `ss -ulpn | grep 7777` — слушает ли порт;
- открыт ли UDP 7777 в ufw **и** в панели провайдера;
- совпадает ли версия игры с сервером (в игре будет «Другая версия игры»).
