#define TRACE
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text.RegularExpressions;
using System.IO;
using System.IO.Ports;


namespace HTTPServer
{
    class MyClass
    {
        private SerialPort _serialPort;
        private String portName;
        private String baudRate;
        private String parity;
        private String dataBits;
        private String stopBits;
        private String handshake;
        private static String status;
        private static String weight;
        private static int numberOfCycle;
        private static MyClass Port;

        public MyClass()
        {
            String line;
            // Read the file and display it line by line.
            System.IO.StreamReader file = new System.IO.StreamReader(@"ParamInitComPort.ini");
            string [] Param;
            bool st = false;
            //while ((line = file.ReadLine()) != null)
            //{
            //    Param = line.Split(new char[] { ' ', '=' });
            //    switch (Param[0])
            //    {
            //        case "portName": portName = Param[3];
            //            break;
            //        case "baudRate": baudRate = Param[3];
            //            break;
            //        case "parity": parity = Param[3];
            //            break;
            //        case "dataBits": dataBits = Param[3];
            //            break;
            //        case "stopBits": stopBits = Param[3];
            //            break;
            //        case "handshake": handshake = Param[3];
            //            st = true;
            //            break;
            //    }
            //}
            //if (st == true)
            //    OpenPort();
        }

        ~MyClass()
        {
            if (_serialPort.IsOpen == true)
                ClosePort();
        }

        public bool GetStatusPort()
        {
            //if (_serialPort.IsOpen == true)
                return true;
            //else
                //return false;            
        }

        private int OpenPort()
        {
            _serialPort = new SerialPort();
            _serialPort.PortName = portName;
            _serialPort.BaudRate = int.Parse(baudRate);
            _serialPort.Parity = (Parity)Enum.Parse(typeof(Parity), parity, true);
            _serialPort.DataBits = int.Parse(dataBits.ToUpperInvariant());
            _serialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), stopBits, true);
            _serialPort.Handshake = (Handshake)Enum.Parse(typeof(Handshake), handshake, true);
            _serialPort.ReadTimeout = 500;
            _serialPort.WriteTimeout = 500;
            try
            {
                _serialPort.Open();
                return 1; //порт відкрито без проблем
            }
            catch (IOException e)
            {                
                using (StreamWriter sw = new StreamWriter("Error.txt"))
                {     
                  sw.WriteLine(e);
                }
                return 0; //порт не відкрито
            }
        }

        private int ClosePort()
        {
            if (_serialPort.IsOpen == true)
            {
                _serialPort.Close();
                return 1; //закрито
            }
            else
                return 0; //не закрито
        }

        public String GetStateOfWeight(int st) // функція отримує стан ваги (вкл / викл)
        {
            if (_serialPort.IsOpen == false)
                return ">>Selected com port occupied by another process."; //вибраний com порт зайнятий іншим процессом 
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            _serialPort.WriteLine("\x53\x4A\x0D\x0A");
            numberOfCycle = 0;
            status = null;
            while (String.IsNullOrEmpty(status))
            {
                if (numberOfCycle > 3)
                    break;
                try
                {
                    status = _serialPort.ReadLine();
                }
                catch (TimeoutException)
                { }
                numberOfCycle++;
                Thread.Sleep(100);
            }
            //ClosePort();
            if (!String.IsNullOrEmpty(status))
                return ">>Weight included."; //вага включене
            else
                return ">>The weight is off or not connected."; //вага виключена
        }

        public String RemoveIncludeWeight() // функція включає / виключає вагу
        {
            int timePause = 0;
            if (GetStateOfWeight(0) == ">>Weight included.")
                timePause = 400;
            else
                timePause = 3000;
            if (_serialPort.IsOpen == false)
                return ">>Selected com port occupied by another process."; //вибраний com порт зайнятий іншим процессом 
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            _serialPort.WriteLine("\x53\x53\x0D\x0A");
            status = null;

            numberOfCycle = 0;
            while (String.IsNullOrEmpty(status))
            {
                if (numberOfCycle > 2)
                    break;
                try
                {
                    status = _serialPort.ReadLine();
                }
                catch (TimeoutException)
                { }
                numberOfCycle++;
                Thread.Sleep(100);
            }
            if (!String.IsNullOrEmpty(status))
            {
                Thread.Sleep(timePause);
                String st = GetStateOfWeight(1);
                return (st == ">>The weight is off or not connected.") ? ">>Weight excluded." : st; // стан ваги після каманди вкл/викл
            }
            else
            {
                return ">>Time waiting for a response from the weight out. Please try again. Maybe the problem is with z'yednani weight, to check the cable for integrity and try again povtorit weighing."; //якщо нема відповіді тоді відсутнє з'єднання з вагою
            }
        }

        public String GetWeight() // функція отримує вагу
        {
            if (GetStateOfWeight(0) == ">>The weight is off or not connected.")
                return ">>The weight is off or not connected. For weighing initially include weight / check the connection with i repeat again.";
            if (_serialPort.IsOpen == false)
                return ">>Selected com port occupied by another process."; //вибраний com порт зайнятий іншим процессом
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            numberOfCycle = 0;
            weight = null;
            _serialPort.WriteLine("\x53\x49\x0D\x0A");
            while (String.IsNullOrEmpty(weight))
            {
                if (numberOfCycle > 4)
                    break;               
                try
                {
                    weight = _serialPort.ReadLine();
                }
                catch (TimeoutException)
                { }
                numberOfCycle++;
                Thread.Sleep(100);
            }
            if (!String.IsNullOrEmpty(weight))
                return weight.Trim();
            else
                return ">>Time waiting for a response from the weight out. Please try again. Maybe the problem is with z'yednani weight, to check the cable for integrity and try again povtorit weighing.";
        }
    }

    class Client
    {
        // Конструктор класса. Ему нужно передавать принятого клиента от TcpListener
        public Client(TcpClient Client, MyClass ComPort)
        {
            // Объявим строку, в которой будет хранится запрос клиента
            string Request = "";
            // Буфер для хранения принятых от клиента данных
            byte[] Buffer = new byte[1024];
            // Переменная для хранения количества байт, принятых от клиента
            int Count;
            try
            {
                // Читаем из потока клиента до тех пор, пока от него поступают данные
                if ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
                {
                    // Преобразуем эти данные в строку и добавим ее к переменной Request

                    Request += Encoding.ASCII.GetString(Buffer, 0, 50);
                    // Запрос должен обрываться последовательностью \r\n\r\n
                    // Либо обрываем прием данных сами, если длина строки Request превышает 4 килобайта
                    // Нам не нужно получать данные из POST-запроса (и т. п.), а обычный запрос
                    // по идее не должен быть больше 4 килобайт
                    //if (Request.Contains("@") == true)
                    //{
                    //    Console.WriteLine("Index @: {0}", Request.IndexOf("@"));
                    //    break;
                    //}
                }
            }
            catch (IOException)
            { }

            int index = -1;
            int ID = 0;
            double pr;
            byte[] Buffer2 = new byte[50];
            String massage = "";
            Console.WriteLine(Request);
            if (Request.Contains("@id=") == true)
            {
                index = Request.IndexOf("@id=");
                pr = char.GetNumericValue(Request[index + 4]);
                ID = Convert.ToInt32(pr);

                switch (ID)
                {
                    //case 0:
                    //    massage = "Bad Request.";
                    //    break;
                    //case 1:
                    //    massage = ComPort.GetWeight();
                    //    break;
                    //case 2:
                    //    massage = ComPort.GetStateOfWeight(0);
                    //    break;
                    //case 3:
                    //    massage = ComPort.RemoveIncludeWeight();
                    //    break;
                    default:
                        massage = "ERROR!!!";
                        break;
                }               
            }
            else
                massage = "Bad Request.";

            Buffer2 = Encoding.ASCII.GetBytes(massage);
            Console.WriteLine(massage);
            // Отправим его клиенту
            try
            {
                Client.GetStream().Write(Buffer2, 0, Buffer2.Length);
                Client.Close();
            }
            catch (IOException)
            { }
        }
    }

    class Server
    {
        TcpListener Listener; // Объект, принимающий TCP-клиентов

        // Запуск сервера
        public Server(int Port, MyClass PortOpen)
        {
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            Console.WriteLine("Host name PC: {0}:{1}", ipHostInfo.AddressList[0], Port);
            Listener = new TcpListener(ipAddress, Port); // Создаем "слушателя" для указанного порта
            Listener.Start(); // Запускаем его
            int index = 0;
            // В бесконечном цикле
            while (true)
            {
                // Принимаем нового клиента
                try
                {
                    TcpClient Client = Listener.AcceptTcpClient();
                    if (Client != null)
                    {
                        Console.WriteLine("New Connect... {0}", index);
                        index++;
                        ClientThread(Client, PortOpen);                        
                    }
                }
                catch(InvalidOperationException)
                    {}
                catch(SocketException)
                    {}               
            }
        }

        static void ClientThread(Object StateInfo, MyClass PortOpen)
        {
            // Просто создаем новый экземпляр класса Client и передаем ему приведенный к классу TcpClient объект StateInfo
            new Client((TcpClient)StateInfo, PortOpen);
        }

        // Остановка сервера
        ~Server()
        {
            // Если "слушатель" был создан
            if (Listener != null)
            {
                // Остановим его
                Listener.Stop();
            }
        }

        static void Main(string[] args)
        {
            // Создадим новый сервер на порту 8080
            MyClass Port = new MyClass();
            if (Port.GetStatusPort() == true)
                new Server(8082, Port);                 
        }
    }
}
