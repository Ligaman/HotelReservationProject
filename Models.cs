namespace HotelReservations
{
    // Стая в хотела (ред от таблица Rooms)
    public class Room
    {
        public int Id { get; set; }
        public string Number { get; set; } = "";
        public string Type { get; set; } = "";
        public decimal Price { get; set; }

        // Текст за падащия списък при избор на стая
        public string Display => $"{Number} – {Type} ({Price:F2} €)";
    }

    // Резервация (ред от таблица Reservations + данните за стаята)
    public class Reservation
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = "";
        public string RoomType { get; set; } = "";
        public decimal RoomPrice { get; set; }
        public string ClientName { get; set; } = "";
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
    }
}
