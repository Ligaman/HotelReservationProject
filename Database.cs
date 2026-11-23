using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace HotelReservations
{
    // Целият достъп до SQLite базата е събран тук.
    public static class Database
    {
        private const string DateFormat = "yyyy-MM-dd";

        // Файлът hotel.db се създава до .exe файла на приложението
        private static readonly string ConnectionString =
            $"Data Source={Path.Combine(AppContext.BaseDirectory, "hotel.db")};Foreign Keys=True";

        private static SqliteConnection Open()
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        // Създава таблиците, ако ги няма, и добавя няколко примерни стаи
        public static void Initialize()
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Rooms (
                    Id     INTEGER PRIMARY KEY AUTOINCREMENT,
                    Number TEXT    NOT NULL UNIQUE,
                    Type   TEXT    NOT NULL,
                    Price  REAL    NOT NULL CHECK (Price >= 0)
                );

                CREATE TABLE IF NOT EXISTS Reservations (
                    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    RoomId     INTEGER NOT NULL,
                    ClientName TEXT    NOT NULL CHECK (trim(ClientName) <> ''),
                    CheckIn    TEXT    NOT NULL,
                    CheckOut   TEXT    NOT NULL,
                    CHECK (CheckOut > CheckIn),
                    FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE RESTRICT
                );";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT COUNT(*) FROM Rooms";
            if ((long)cmd.ExecuteScalar()! == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO Rooms (Number, Type, Price) VALUES
                        ('101', 'Единична', 45),
                        ('102', 'Двойна', 65),
                        ('201', 'Апартамент', 110);";
                cmd.ExecuteNonQuery();
            }
        }

        // ---------- Стаи ----------

        public static List<Room> GetRooms()
        {
            var rooms = new List<Room>();
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Number, Type, Price FROM Rooms ORDER BY Number";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rooms.Add(new Room
                {
                    Id = reader.GetInt32(0),
                    Number = reader.GetString(1),
                    Type = reader.GetString(2),
                    Price = (decimal)reader.GetDouble(3)
                });
            }
            return rooms;
        }

        public static void AddRoom(Room room)
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Rooms (Number, Type, Price) VALUES ($number, $type, $price)";
            cmd.Parameters.AddWithValue("$number", room.Number);
            cmd.Parameters.AddWithValue("$type", room.Type);
            cmd.Parameters.AddWithValue("$price", (double)room.Price);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateRoom(Room room)
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Rooms SET Number = $number, Type = $type, Price = $price WHERE Id = $id";
            cmd.Parameters.AddWithValue("$number", room.Number);
            cmd.Parameters.AddWithValue("$type", room.Type);
            cmd.Parameters.AddWithValue("$price", (double)room.Price);
            cmd.Parameters.AddWithValue("$id", room.Id);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteRoom(int id)
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Rooms WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        // ---------- Резервации ----------

        public static List<Reservation> GetReservations()
        {
            var reservations = new List<Reservation>();
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT r.Id, r.RoomId, rm.Number, rm.Type, rm.Price, r.ClientName, r.CheckIn, r.CheckOut
                FROM Reservations r
                JOIN Rooms rm ON rm.Id = r.RoomId
                ORDER BY r.CheckIn, rm.Number";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                reservations.Add(new Reservation
                {
                    Id = reader.GetInt32(0),
                    RoomId = reader.GetInt32(1),
                    RoomNumber = reader.GetString(2),
                    RoomType = reader.GetString(3),
                    RoomPrice = (decimal)reader.GetDouble(4),
                    ClientName = reader.GetString(5),
                    CheckIn = ParseDate(reader.GetString(6)),
                    CheckOut = ParseDate(reader.GetString(7))
                });
            }
            return reservations;
        }

        public static void AddReservation(Reservation reservation)
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Reservations (RoomId, ClientName, CheckIn, CheckOut)
                VALUES ($roomId, $client, $checkIn, $checkOut)";
            AddReservationParameters(cmd, reservation);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateReservation(Reservation reservation)
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Reservations
                SET RoomId = $roomId, ClientName = $client, CheckIn = $checkIn, CheckOut = $checkOut
                WHERE Id = $id";
            AddReservationParameters(cmd, reservation);
            cmd.Parameters.AddWithValue("$id", reservation.Id);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteReservation(int id)
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Reservations WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        // Проверява дали стаята вече е заета в дадения период.
        // excludeId е резервацията, която се редактира в момента (за да не се засича сама със себе си).
        public static bool IsRoomTaken(int roomId, DateTime checkIn, DateTime checkOut, int? excludeId)
        {
            using var connection = Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Reservations
                WHERE RoomId = $roomId
                  AND Id <> $excludeId
                  AND CheckIn < $checkOut
                  AND CheckOut > $checkIn";
            cmd.Parameters.AddWithValue("$roomId", roomId);
            cmd.Parameters.AddWithValue("$excludeId", excludeId ?? -1);
            cmd.Parameters.AddWithValue("$checkIn", FormatDate(checkIn));
            cmd.Parameters.AddWithValue("$checkOut", FormatDate(checkOut));
            return (long)cmd.ExecuteScalar()! > 0;
        }

        private static void AddReservationParameters(SqliteCommand cmd, Reservation reservation)
        {
            cmd.Parameters.AddWithValue("$roomId", reservation.RoomId);
            cmd.Parameters.AddWithValue("$client", reservation.ClientName);
            cmd.Parameters.AddWithValue("$checkIn", FormatDate(reservation.CheckIn));
            cmd.Parameters.AddWithValue("$checkOut", FormatDate(reservation.CheckOut));
        }

        // Датите се пазят като текст във формат ГГГГ-ММ-ДД, за да се сравняват правилно в SQL
        private static string FormatDate(DateTime date) =>
            date.ToString(DateFormat, CultureInfo.InvariantCulture);

        private static DateTime ParseDate(string text) =>
            DateTime.ParseExact(text, DateFormat, CultureInfo.InvariantCulture);
    }
}
