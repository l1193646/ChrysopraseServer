using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using NetworkCommons;

namespace ChrysopraseServer
{
    // Event handlers
    public delegate void ClientConnectedHandler(object sender, EventArgs e);

    /// <summary>
    /// Daemon program that runs on console, acts as the server for the game.
    /// </summary>
    public class ChrysopraseDaemon
    {
        // Class settings
        public static int DefaultPort = 57575;
        public static int DefaultMaxUsers = 10;
        public static string HostName = "localhost";

        // Class attributes

        /// <summary>The socket used to accept connections, it binds to local host on the port specified by the port attribute.</summary>
        private Socket server;
        /// <summary>The port this server is bound to.</summary>
        private int port;
        /// <summary>Represents the local network end point</summary>
        private IPEndPoint local_end_point;
        /// <summary>The max allowed number of users connected at the same time.</summary>
        private int max_users;
        /// <summary>The battlefields or play rooms on the server.</summary>
        private ArrayList battlefields;
        /// <summary>This thread is in charge of listening for incoming connections.</summary>
        private Thread listener;

        // Class properties

        /// <summary>Gets the number of users connected to the server.</summary>
        public int ConnectedUsers
        {
            get
            {
                int count = 0;
                foreach (BattleField b in battlefields)
                {
                    count += b.PlayerCount;
                }
                return count;
            }
        }

        /// <summary>Determines if the server is full, and thus, it can't accept any more connections.</summary>
        public bool IsFull { get { return (ConnectedUsers >= max_users); } }

        // Events
        public event ClientConnectedHandler ClientConnected;

        /// <summary>
        /// Creates a new instance of the <see cref="ChrysopraseServer.ChrysopraseDaemon"/> class, and tries to bind it to the default port.
        /// </summary>
        public ChrysopraseDaemon()
        {
            Initialize(DefaultPort, DefaultMaxUsers);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ChrysopraseServer.ChrysopraseDaemon"/> class, and tries to bind it to the specified port.
        /// </summary>
        /// <param name="port">The port this daemon should be bound to.</param>
        /// <param name="max_users">The maximum number of users this server will allow to connect to it.</param>
        public ChrysopraseDaemon(int port, int max_users)
        {
            Initialize(port, max_users);
        }

        /// <summary>
        /// Initializes this instance with the provided values.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="max_users"></param>
        private void Initialize(int port, int max_users)
        {
            server = null;
            this.port = port;
            this.max_users = max_users;
            this.battlefields = new ArrayList();
            try
            {
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                local_end_point = new IPEndPoint(IPAddress.Any, port);
                server.Bind(local_end_point);
                server.Listen(1);
                ClientConnected += new ClientConnectedHandler(ChrysopraseDaemon_ClientConnected);
            }
            catch (SocketException s_ex)
            {
                Console.WriteLine(s_ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        void ChrysopraseDaemon_ClientConnected(object sender, EventArgs e)
        {
            Console.WriteLine("Client connected! Connected clients: {0}", ConnectedUsers);
            if (IsFull) server.Blocking = true;
        }

        /// <summary>
        /// Begins a subprocess that will listen to the server's port and assign connection sockets to battlefields.
        /// </summary>
        public void BeginListening()
        {
            listener = new Thread(new ThreadStart(ListenNetworkForIncomingClients));
            listener.Start();
        }

        private void ListenNetworkForIncomingClients()
        {
            Socket new_client = null;
            while (new_client == null)
            {
                while (IsFull) { Thread.Sleep(200); }
                new_client = AcceptClient();
                Player new_player = new Player(new_client);
                PlaceOnBattleField(new_player);
            }
        }

        private Socket AcceptClient()
        {
            Socket new_client = null;
            try
            {
                new_client = server.Accept();
                ClientConnected(this, new EventArgs());
            }
            catch (SocketException s_ex)
            {
                Console.WriteLine(s_ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return new_client;
        }

        private void PlaceOnBattleField(Player p)
        {
            bool placed = false;
            foreach (BattleField b in battlefields)
            {
                placed = b.AcceptPlayer(p);
                if (placed) break;
            }
            if (!placed)
            {
                BattleField new_battlefield = new BattleField();
                new_battlefield.AcceptPlayer(p);
                battlefields.Add(new_battlefield);
            }
        }

        /// <summary>
        /// Parses a command to execute some operation on the daemon.
        /// </summary>
        /// <param name="commands">A string array containing a command and optional parameters</param>
        public void ParseCommand(string[] commands)
        {
            if (commands.Length == 0) return;
            switch (commands[0])
            {
                case "start":
                    BeginListening();
                    Console.WriteLine("Started listening for incoming clients on port {0}", port);
                    break;
                case "stop":

                    break;
                case "userinfo":
                    Console.WriteLine("Users connected: {0}", ConnectedUsers);
                    break;
                case "rooms":
                    foreach(BattleField b in battlefields)
                    {
                        Console.WriteLine("BattleField:");
                        foreach (Player p in b.Players)
                        {
                            Console.WriteLine("Player: {0}", p.Name);
                        }
                    }
                    break;
                case "":

                    break;
                default:
                    Console.WriteLine("Invalid command");
                    break;
            }
        }

        /// <summary>
        /// Entry point of the server
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            ChrysopraseDaemon daemon = new ChrysopraseDaemon();
            string command = "";
            do
            {
                command = Console.ReadLine();
                daemon.ParseCommand(command.Split(' '));
            } while (command != "exit");
        }
    }
}
