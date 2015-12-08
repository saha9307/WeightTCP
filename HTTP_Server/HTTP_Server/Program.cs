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
using System.Windows.Forms;
using System.Drawing;


namespace HTTPServer
{
    class MyClass
    {
        private String weightName;
        private SerialPort _serialPort;
        private String portName;
        private String baudRate;
        private String parity;
        private String dataBits;
        private String stopBits;
        private String handshake;
        private int portTCP;
        private static String status;
        private static String weight;
        private static int numberOfCycle;
        bool _continue;

        public MyClass()
        {
            String line;
            // Read the file and display it line by line.
            System.IO.StreamReader file = new System.IO.StreamReader(@"ParamInitComPort.ini");
            string [] Param;
            bool st = false;
            while ((line = file.ReadLine()) != null)
            {
                Param = line.Split(new char[] { ' ', '=' });
                switch (Param[0])
                {
                    case "portName": portName = Param[3];
                        break;
                    case "baudRate": baudRate = Param[3];
                        break;
                    case "parity": parity = Param[3];
                        break;
                    case "dataBits": dataBits = Param[3];
                        break;
                    case "stopBits": stopBits = Param[3];
                        break;
                    case "handshake": handshake = Param[3];
                        break;
                    case "weightName": weightName = Param[3];
                        break;
                    case "portTCP": portTCP = Convert.ToInt32(Param[3], 10);
                        st = true;
                        break;
                }
            }
            if (st == true)
                if (OpenPort() == 0) 
                    Environment.Exit(0);
        }

        ~MyClass()
        {
            try
            {
                if (_serialPort.IsOpen == true)
                    ClosePort();
            }
            catch (Exception e)
            { return; }
           
        }

        public bool GetStatusPort()
        {
            if (_serialPort.IsOpen == true)
                return true;
            else
                return false;            
        }

        public int getPortTCP()
        { return portTCP; }

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

        public int ClosePort()
        {
            if (_serialPort.IsOpen == true)
            {
                _serialPort.Close();
                return 1; //закрито
            }
            else
                return 0; //не закрито
        }

        public String GetStateOfWeight() // функція отримує стан ваги (вкл / викл)
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

            if (status != null)
                return ">>Weight included."; //вага включене
            else
                return ">>The weight is off or isn't connected."; //вага виключена
        }

        public String RemoveIncludeWeight() // функція включає / виключає вагу
        {
            int timePause = 5000;
            if (_serialPort.IsOpen == false)
                return ">>Selected com port occupied by another process."; //вибраний com порт зайнятий іншим процессом 
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            _serialPort.WriteLine("\x53\x53\x0D\x0A");
            Thread.Sleep(timePause);
          
             return GetStateOfWeight(); // стан ваги після каманди вкл/викл        
        }

        public String GetWeight() // функція отримує вагу
        {
            if (weightName == "BDL" && GetStateOfWeight() == ">>The weight is off or not connected.")
                return ">>The weight is off or not connected. For weighing initially include weight / check the connection with i repeat again.";
            if (_serialPort.IsOpen == false)
                return ">>Selected com port occupied by another process."; //вибраний com порт зайнятий іншим процессом
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            numberOfCycle = 0;
            weight = null;

            String command = "";
            
            switch (weightName)
            {
                case "BDU": command = "\x02\x41\x03\x0D\x0A";
                    break;
                case "BDL": command = "\x53\x49\x0D\x0A";
                    break;
                case "KELI": command = "\x02\x41\x03";
                    break;
            }

            _serialPort.WriteLine(command);

            _continue = true;
            do
            {
                try
                {
                    Thread.Sleep(1000);
                    if (_serialPort.BytesToRead > 0)
                    {
                        char[] str = new char[_serialPort.BytesToRead];
                        int numSize = _serialPort.Read(str, 0, _serialPort.BytesToRead);                        
                        weight = new string(str);
                        _continue = false;
                    }
                }
                catch (TimeoutException) {}

                if (numberOfCycle > 4)
                    break;              
               numberOfCycle++;
            }
            while (_continue);
            

            if (!String.IsNullOrEmpty(weight))
                return weight.Trim();
            else
                return ">>Time waiting for a response from the weight out. Please try again. Maybe the problem is with connecting weight, to check the cable for integrity and try again repeat weighing.";
        }
    }     

    class Client
    {
        // Конструктор класса. Ему нужно передавать принятого клиента от TcpListener
        public Client(TcpClient Client, MyClass ComPort)
        {

            string Request = ""; // string for questions from client
            byte[] Buffer = new byte[1024]; // buff for questions from client 
            int Count; // number send bytes from client
            try
            {
                // read data from stream
                if ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
                {
                    // convert data into string
                    Request += Encoding.ASCII.GetString(Buffer, 0, 50);
                }
            }
            catch (IOException)
            { }
            
            int index = -1;
            int ID = 0;
            double pr;
            byte[] Buffer2 = new byte[50];
            String massage = "";            
            if (Request.Contains("?id=") == true)
            {
                Console.WriteLine("" + Client.Client.RemoteEndPoint.ToString() + "\n" + Request);
                index = Request.IndexOf("?id=");
                pr = char.GetNumericValue(Request[index + 4]);
                ID = Convert.ToInt32(pr);

                switch (ID)
                {
                    case 0:
                        massage = "Bad Request.";
                        break;
                    case 1:
                        massage = ComPort.GetWeight();
                        break;
                    case 2:
                        massage = ComPort.GetStateOfWeight();
                        break;
                    case 3:
                        massage = ComPort.RemoveIncludeWeight();
                        break;
                    default:
                        massage = "ERROR!!!";
                        break;
                }
                Buffer2 = Encoding.ASCII.GetBytes(massage);
                Console.WriteLine(massage + "\n<<----------------------------------------->>\n");
            }
            //else
            //    massage = "Bad Request.";

           
            try
            {
                //sending answer to client
                Client.GetStream().Write(Buffer2, 0, Buffer2.Length);
                Client.Close();
            }
            catch (IOException)
            { }
        }
    }

    class Server
    {
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")] 
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        private static TcpListener Listener; // Объект, принимающий TCP-клиентов
        private static bool ConsoleVisible = false;
        private static NotifyIcon TrayIcon;
        private static System.Timers.Timer TrayTimer;
        private static MyClass PortOpen;

        public Server(int portTCP, MyClass PortOpenNew)
        {
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            Console.WriteLine("Host name my PC: {0}:{1} \n<<---------------------------------------->>", ipHostInfo.AddressList[0], portTCP);
            Listener = new TcpListener(ipAddress, portTCP); // Создаем "слушателя" для указанного порта

            try
            {
                Listener.Start(); // start listener

                // hide console window
                ToggleWindow(false);
                
                // создаем контекстное меню
				ContextMenu TrayMenu = new ContextMenu();
				TrayMenu.MenuItems.Add("Показать/спрятать консоль", TrayToggle);
                TrayMenu.MenuItems.Add("Перезапустить сервер ваг", RestartServer);
				TrayMenu.MenuItems.Add("Выход", OnExit);

                TrayIcon = new NotifyIcon();
                TrayIcon.ContextMenu = TrayMenu; // registering context menu 
                TrayIcon.Icon = new System.Drawing.Icon("server.ico");
                TrayIcon.Visible = true;
                TrayIcon.Text = "Сервер ваг работает. Порт: " + portTCP;
                TrayIcon.MouseDoubleClick += new MouseEventHandler(TrayClick); // show/hide console window double click
                TrayIcon.ShowBalloonTip(2000, "Сервер ваг", "Сервер ваг успешно запущен и работает!", ToolTipIcon.Info);
                System.Threading.Thread.Sleep(2000);

                //createing timer for start listener
                TrayTimer = new System.Timers.Timer();
                TrayTimer.Interval = 100;
                TrayTimer.Enabled = true;
                TrayTimer.Elapsed += new System.Timers.ElapsedEventHandler(StartListener); // основная функция консоли
                PortOpen = PortOpenNew;

                Application.Run();
            }
            catch (Exception e)
            { return; }
           
        }

        static void StartListener(object StateInfo, EventArgs e)
        {
            while (true)
            {
                // Принимаем нового клиента
                try
                {
                    TcpClient Client = Listener.AcceptTcpClient();
                    if (Client != null)
                    {                       
                        ClientThread(Client, PortOpen);
                    }
                }
                catch (InvalidOperationException)
                { }
                catch (SocketException)
                { }
            }
        }

        static void RestartServer(object Sender, EventArgs e)
        {
            Listener.Stop();
            PortOpen.ClosePort();

            //restart app
            TrayIcon.Icon = null;
            TrayIcon.Dispose();
            Application.DoEvents();
            Application.Restart();
        }

        static void ToggleWindow(bool visible)
        {
            ConsoleVisible = visible;
            ShowWindow(GetConsoleWindow(), Convert.ToInt32(visible));
        }

        static void TrayToggle(object Sender, EventArgs e)
        {
            // по выбору из меню
            ToggleWindow(!ConsoleVisible);
        }

        static void OnExit(object Sender, EventArgs e)
        {            
            Listener.Stop();
            PortOpen.ClosePort();
            TrayIcon.Icon = null;
            TrayIcon.Dispose();
            Application.DoEvents(); 
            Environment.Exit(0);            
        }

        static void TrayClick(object Sender, EventArgs e)
        {
            // по двойному щелчку
            ToggleWindow(!ConsoleVisible);
        }

        static void ClientThread(Object StateInfo, MyClass PortOpen)
        {
            // Просто создаем новый экземпляр класса Client и передаем ему приведенный к классу TcpClient объект StateInfo
            new Client((TcpClient)StateInfo, PortOpen);
        }

        ~Server()
        {
            if (Listener != null)
            {
                // stopped listener
                Listener.Stop();             
            }
        }

        static void Main(string[] args)
        {
            // Создадим новый сервер на порту 8080
            MyClass Port = new MyClass();
            if (Port.GetStatusPort() == true)
            {
                new Server(Port.getPortTCP(), Port);
            }  
        }

    }
}
