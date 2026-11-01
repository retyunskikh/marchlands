namespace Marchlands.Network
{
    /// <summary>Первый байт каждого сетевого сообщения: что в нём. Лобби — до матча, остальное — во время матча.</summary>
    public enum NetMessageType : byte
    {
        // Лобби.
        Hello = 1,
        Welcome = 2,
        Reject = 3,
        LobbyState = 4,
        Ready = 5,
        Chat = 6,
        StartMatch = 7,
        Leave = 8,

        // Матч.
        TickInput = 20,
        StateHash = 21,
        Quit = 22,

        // Сервер комнат в интернете (VPS).
        ListRooms = 30,
        RoomList = 31,
        CreateRoom = 32,
        JoinRoom = 33,
        RoomSettings = 34,
        StartRequest = 35,
        RoomJoined = 36
    }
}
