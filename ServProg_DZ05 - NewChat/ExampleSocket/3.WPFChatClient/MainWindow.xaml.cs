using _3.WPFChatClient.dto;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace _3.WPFChatClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //фото користувача
        string image; // Змінна для збереження шляху до обраного фото
        TcpClient client = new TcpClient(); //клієнт, який іде при підключені до сервера
        NetworkStream ns; // Потік для обміну даними з сервером
        Thread thread; // Потік для асинхронного отримання даних з сервера
        //Повідомлення, яке відправляємо на сервер
        ChatMessage _message = new ChatMessage();

        public MainWindow()
        {
            InitializeComponent();
        }
        //відключення при закритті
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _message.Text = "Покинув чат"; //Повідомленяємо усім, що ми покинули чат
            var buffer = _message.Serialize(); // Серіалізуємо повідомлення
            ns.Write(buffer); // Відправляємо на сервер

            client.Client.Shutdown(SocketShutdown.Send); // Закриваємо з'єднання з сервером
            client.Close();
        }

        //вибір фото
        private void btnPhotoSelect_Click(object sender, RoutedEventArgs e)
        {
            // Отворюємо діалогове вікно для вибору файлу
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.ShowDialog();
            string filePath = dlg.FileName;

            // Зчитуємо файли байтового масиву та конвертуємо у base64
            var bytes = File.ReadAllBytes(filePath);
            var base64 = Convert.ToBase64String(bytes);

            // Створюємо об'єкт для відправки на сервер
            UploadDTO upload = new UploadDTO
            {
                Photo = base64
            };
            string json = JsonConvert.SerializeObject(upload);
            bytes = Encoding.UTF8.GetBytes(json);
            string serverUrl = "https://pv113.itstep.click";

            // Відправляємо дані на сервер через POST-запит
            WebRequest request = WebRequest.Create($"{serverUrl}/api/gallery/upload");
            request.Method = "POST";
            request.ContentType = "application/json";
            using (var stream = request.GetRequestStream())
            {
                stream.Write(bytes, 0, bytes.Length);
            }
            try
            {
                var response = request.GetResponse();
                using (var stream = new StreamReader(response.GetResponseStream()))
                {
                    string data = stream.ReadToEnd();
                    var resp = JsonConvert.DeserializeObject<UploadResponseDTO>(data);
                    image = resp.Image; // Зберігаємо ім'я файлу з сервера
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

        }
        //підлкючення до сервера
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IPAddress ip = IPAddress.Parse("91.238.103.135");
                int port = 1023;

                // Заповнюємо інформацію про користувача та відправляємо на сервер
                _message.UserId = Guid.NewGuid().ToString();
                _message.Name = txtUserName.Text;
                _message.Photo = image;
                client.Connect(ip, port); // Підключаємося до сервера
                ns = client.GetStream(); //получаю вказівник на потік
                thread = new Thread(o => ReceivedData((TcpClient)o)); // Створюємо потік для отримання даних
                thread.Start(client); // Запускаємо потік
                bntSend.IsEnabled = true;
                btnConnect.IsEnabled = false;
                txtUserName.IsEnabled = false;
                _message.Text = "Приєнався до чату";
                var buffer = _message.Serialize(); // Серіалізуємо повідомлення
                ns.Write(buffer); // Відправляємо на сервер
            }
            catch(Exception ex)
            {
                MessageBox.Show("Problem connect "+ex.Message);
            }
            
        }
        //Читання даних від сервера
        private void ReceivedData(TcpClient client) //отримує дані від сервера через даний метод
        {
            NetworkStream ns = client.GetStream();
            byte[] readBytes = new byte[16054400];
            int byte_count;
            while((byte_count = ns.Read(readBytes))>0) {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ChatMessage message = ChatMessage.Desserialize(readBytes); // Десеріалізація повідомлення
                    var grid = new Grid(); // Створюємо Grid для відображення повідомлення

                    // Додаємо стовпці до Grid для зображення та тексту
                    for (int i = 0; i < 2; i++) 
                    {
                        var colDef = new ColumnDefinition();
                        colDef.Width = GridLength.Auto;
                        grid.ColumnDefinitions.Add(colDef);
                    }

                    // Зображення за URL та BitmapImage
                    BitmapImage bmp = new BitmapImage(new Uri($"https://pv113.itstep.click{message.Photo}"));
                    var image = new Image(); // Створюємо об'єкт Image для відображення зображення
                    image.Source = bmp;
                    image.Width = 50;
                    image.Height = 50;

                    var textBlock = new TextBlock(); // відображення тексту
                    Grid.SetColumn(textBlock, 1);
                    textBlock.VerticalAlignment = VerticalAlignment.Center;
                    textBlock.Margin = new Thickness(5,0,0,0);
                    textBlock.Text = message.Name + " -> " + message.Text;
                    grid.Children.Add(image); // елементи до Grid
                    grid.Children.Add(textBlock);

                    lbInfo.Items.Add(grid); // Додаємо Grid
                    lbInfo.Items.MoveCurrentToLast(); // Переміщуємо на останнє місце
                    lbInfo.ScrollIntoView(lbInfo.Items.CurrentItem); // Прокручуємо до останнього елементу

                }));
            }
        }
        //надсилаємо повідомлення на сервер
        private void bntSend_Click(object sender, RoutedEventArgs e)
        {
            _message.Text = txtText.Text; // Встановлюємо текст повідомлення
            var buffer = _message.Serialize(); // Серіалізуємо повідомлення
            ns.Write(buffer); //відправляємо повідомлення на сервер
            txtText.Text = ""; // Очищаємо поле вводу повідомлення
        }
    }
}
