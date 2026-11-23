using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;

namespace HotelReservations
{
    public partial class MainWindow : Window
    {
        private const int SqliteConstraintError = 19;

        // Всички записи от базата; в таблиците се показват само тези, които минават филтъра
        private List<Room> _allRooms = new();
        private List<Reservation> _allReservations = new();

        public MainWindow()
        {
            InitializeComponent();
            Database.Initialize();
            LoadRooms();
            LoadReservations();
        }

        // ---------- Зареждане на данните ----------

        private void LoadRooms()
        {
            _allRooms = Database.GetRooms();
            ReservationRoomCombo.ItemsSource = _allRooms;
            ApplyRoomFilter();
        }

        private void LoadReservations()
        {
            _allReservations = Database.GetReservations();
            ApplyReservationFilter();
        }

        // ---------- Филтриране ----------

        // Показва само стаите, чийто номер или тип съдържа въведения текст
        private void ApplyRoomFilter()
        {
            string text = RoomFilter.Text.Trim();
            RoomsGrid.ItemsSource = _allRooms
                .Where(r => Matches(r.Number, text) || Matches(r.Type, text))
                .ToList();
        }

        // Показва само резервациите, при които клиентът, номерът или типът на стаята съдържа текста
        // и чийто престой попада (поне частично) в избрания период. Празна дата означава "без ограничение".
        private void ApplyReservationFilter()
        {
            string text = ReservationFilter.Text.Trim();
            DateTime? from = FilterFromPicker.SelectedDate?.Date;
            DateTime? to = FilterToPicker.SelectedDate?.Date;

            ReservationsGrid.ItemsSource = _allReservations
                .Where(r => Matches(r.ClientName, text) || Matches(r.RoomNumber, text) || Matches(r.RoomType, text))
                .Where(r => (from == null || r.CheckOut >= from) && (to == null || r.CheckIn <= to))
                .ToList();
        }

        // Търсене без значение от главни/малки букви; празен филтър показва всичко
        private static bool Matches(string value, string text) =>
            text == "" || value.Contains(text, StringComparison.CurrentCultureIgnoreCase);

        private void RoomFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyRoomFilter();

        private void ReservationFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyReservationFilter();

        private void RoomFilterClear_Click(object sender, RoutedEventArgs e) => RoomFilter.Clear();

        private void ReservationDateFilter_Changed(object? sender, SelectionChangedEventArgs e) => ApplyReservationFilter();

        private void ReservationFilterClear_Click(object sender, RoutedEventArgs e)
        {
            ReservationFilter.Clear();
            FilterFromPicker.SelectedDate = null;
            FilterToPicker.SelectedDate = null;
        }

        // ================= Стаи =================

        private void RoomsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoomsGrid.SelectedItem is Room room)
            {
                RoomNumberTextBox.Text = room.Number;
                RoomTypeCombo.Text = room.Type;
                RoomPriceTextBox.Text = room.Price.ToString("F2");
            }
        }

        private void AddRoom_Click(object sender, RoutedEventArgs e)
        {
            var room = ReadRoomForm();
            if (room == null) return;

            if (!TrySave(() => Database.AddRoom(room), "Вече има стая с този номер.")) return;

            LoadRooms();
            ClearRoomForm();
        }

        private void UpdateRoom_Click(object sender, RoutedEventArgs e)
        {
            if (RoomsGrid.SelectedItem is not Room selected)
            {
                ShowWarning("Изберете стая от списъка.");
                return;
            }

            var room = ReadRoomForm();
            if (room == null) return;
            room.Id = selected.Id;

            if (!TrySave(() => Database.UpdateRoom(room), "Вече има стая с този номер.")) return;

            LoadRooms();
            LoadReservations(); // резервациите показват номера, типа и цената на стаята
            ClearRoomForm();
        }

        private void DeleteRoom_Click(object sender, RoutedEventArgs e)
        {
            if (RoomsGrid.SelectedItem is not Room selected)
            {
                ShowWarning("Изберете стая от списъка.");
                return;
            }

            if (!Confirm($"Да се изтрие ли стая {selected.Number}?")) return;

            if (!TrySave(() => Database.DeleteRoom(selected.Id),
                    "Стаята има резервации и не може да бъде изтрита. Изтрийте първо резервациите ѝ.")) return;

            LoadRooms();
            ClearRoomForm();
        }

        private void ClearRoom_Click(object sender, RoutedEventArgs e) => ClearRoomForm();

        // Чете и проверява полетата за стая; връща null, ако има грешка
        private Room? ReadRoomForm()
        {
            string number = RoomNumberTextBox.Text.Trim();
            string type = RoomTypeCombo.Text.Trim();

            if (number == "")
            {
                ShowWarning("Въведете номер на стаята.");
                return null;
            }
            if (type == "")
            {
                ShowWarning("Въведете тип на стаята.");
                return null;
            }
            if (!TryParsePrice(RoomPriceTextBox.Text, out decimal price) || price < 0)
            {
                ShowWarning("Въведете валидна цена (неотрицателно число).");
                return null;
            }

            return new Room { Number = number, Type = type, Price = price };
        }

        // Приема и запетая, и точка като десетичен разделител
        private static bool TryParsePrice(string text, out decimal price) =>
            decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Number,
                CultureInfo.InvariantCulture, out price);

        private void ClearRoomForm()
        {
            RoomsGrid.SelectedItem = null;
            RoomNumberTextBox.Clear();
            RoomTypeCombo.Text = "";
            RoomPriceTextBox.Clear();
        }

        // ================= Резервации =================

        private void ReservationsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReservationsGrid.SelectedItem is Reservation reservation)
            {
                ReservationRoomCombo.SelectedValue = reservation.RoomId;
                ClientTextBox.Text = reservation.ClientName;
                CheckInPicker.SelectedDate = reservation.CheckIn;
                CheckOutPicker.SelectedDate = reservation.CheckOut;
            }
        }

        private void AddReservation_Click(object sender, RoutedEventArgs e)
        {
            var reservation = ReadReservationForm(null);
            if (reservation == null) return;

            if (!TrySave(() => Database.AddReservation(reservation), "Данните за резервацията са невалидни.")) return;

            LoadReservations();
            ClearReservationForm();
        }

        private void UpdateReservation_Click(object sender, RoutedEventArgs e)
        {
            if (ReservationsGrid.SelectedItem is not Reservation selected)
            {
                ShowWarning("Изберете резервация от списъка.");
                return;
            }

            var reservation = ReadReservationForm(selected.Id);
            if (reservation == null) return;
            reservation.Id = selected.Id;

            if (!TrySave(() => Database.UpdateReservation(reservation), "Данните за резервацията са невалидни.")) return;

            LoadReservations();
            ClearReservationForm();
        }

        private void DeleteReservation_Click(object sender, RoutedEventArgs e)
        {
            if (ReservationsGrid.SelectedItem is not Reservation selected)
            {
                ShowWarning("Изберете резервация от списъка.");
                return;
            }

            if (!Confirm($"Да се изтрие ли резервацията на {selected.ClientName}?")) return;

            Database.DeleteReservation(selected.Id);
            LoadReservations();
            ClearReservationForm();
        }

        private void ClearReservation_Click(object sender, RoutedEventArgs e) => ClearReservationForm();

        // Чете и проверява полетата за резервация; връща null, ако има грешка.
        // editedId е Id на редактираната резервация (null при добавяне на нова).
        private Reservation? ReadReservationForm(int? editedId)
        {
            if (ReservationRoomCombo.SelectedValue is not int roomId)
            {
                ShowWarning("Изберете стая.");
                return null;
            }

            string client = ClientTextBox.Text.Trim();
            if (client == "")
            {
                ShowWarning("Името на клиента не може да бъде празно.");
                return null;
            }

            if (CheckInPicker.SelectedDate == null || CheckOutPicker.SelectedDate == null)
            {
                ShowWarning("Изберете дата на настаняване и дата на освобождаване.");
                return null;
            }

            DateTime checkIn = CheckInPicker.SelectedDate.Value.Date;
            DateTime checkOut = CheckOutPicker.SelectedDate.Value.Date;

            if (checkOut <= checkIn)
            {
                ShowWarning("Датата на освобождаване трябва да е след датата на настаняване.");
                return null;
            }

            if (Database.IsRoomTaken(roomId, checkIn, checkOut, editedId))
            {
                ShowWarning("Стаята вече е резервирана за този период.");
                return null;
            }

            return new Reservation
            {
                RoomId = roomId,
                ClientName = client,
                CheckIn = checkIn,
                CheckOut = checkOut
            };
        }

        private void ClearReservationForm()
        {
            ReservationsGrid.SelectedItem = null;
            ReservationRoomCombo.SelectedItem = null;
            ClientTextBox.Clear();
            CheckInPicker.SelectedDate = null;
            CheckOutPicker.SelectedDate = null;
        }

        // ================= Помощни методи =================

        // Изпълнява операция в базата; при нарушено ограничение (UNIQUE, CHECK, FOREIGN KEY)
        // показва подаденото съобщение и връща false.
        private bool TrySave(Action action, string constraintMessage)
        {
            try
            {
                action();
                return true;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintError)
            {
                ShowWarning(constraintMessage);
                return false;
            }
        }

        private void ShowWarning(string message) =>
            MessageBox.Show(this, message, "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);

        private bool Confirm(string message) =>
            MessageBox.Show(this, message, "Потвърждение", MessageBoxButton.YesNo, MessageBoxImage.Question)
                == MessageBoxResult.Yes;
    }
}
