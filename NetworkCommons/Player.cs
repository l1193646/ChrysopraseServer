using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace NetworkCommons
{
    public delegate void ReadyToggledHandler(object sender, EventArgs e);
    public delegate void PlayerLostHandler(object sender, EventArgs e);

    /// <summary>
    /// Represents a player inside a Battlefield.
    /// </summary>
    public class Player
    {
        // Class settings
        public static string DefaultPlayerNamePrefix = "Player_";
        public static string DefaultPlayerNameSuffix = "";
        public static char DataSeparator = '|';

        // Class attributes
        /// <summary>Used to assign a unique numerical ID to each player</summary>
        private static long id_counter = 0;
        /// <summary>The ID of this player.</summary>
        private long id;
        /// <summary>The name or nickname used by this player.</summary>
        private string name;
        /// <summary>A flag indicating whether the player is still alive and playing.</summary>
        private bool alive;
        /// <summary>The score of this player.</summary>
        private long score;
        /// <summary>A socket providing the connection to the player's client.</summary>
        private Socket connection;
        /// <summary>A flag determining whether the player is ready to start playing or not.</summary>
        private bool ready;

        // Class properties

        /// <summary>Gets the ID of this player.</summary>
        public long ID { get { return id; } }
        
        /// <summary>Gets the name (or nickname) of this player.</summary>
        public string Name { get { return name; } }
        
        /// <summary>Gets the life status of this player, returns true if the player hasn't yet lost.</summary>
        public bool Alive { get { return alive; } }

        /// <summary>Gets or sets the score of this player.</summary>
        public long Score { get { return score; } set { score = value; } }

        /// <summary>Gets the Ready state of the player. Returns <c>true</c> if the played has notified the server he is ready to start playing.</summary>
        public bool Ready { get { return ready; } }

        /// <summary>Gets the socket bound to the player's game client.</summary>
        public Socket Connection { get { return connection; } set { connection = value; } }


        // Events
        public event ReadyToggledHandler ReadyToggled;
        public event PlayerLostHandler PlayerLost;

        /// <summary>
        /// Creates a new instance of the <see cref="ChrysopraseServer.Player"/> class with the specified connection provider (a Socket) and a default name.
        /// </summary>
        /// <param name="name">The name of the player.</param>
        /// <param name="connection">The socket used to connect to the player's client.</param>
        public Player(Socket connection)
        {
            Initialize(connection, DefaultPlayerNamePrefix + id + DefaultPlayerNameSuffix);
        }
        
        /// <summary>
        /// Creates a new instance of the <see cref="ChrysopraseServer.Player"/> class with the specified name and connection provider (a Socket).
        /// </summary>
        /// <param name="name">The name of the player.</param>
        /// <param name="connection">The socket used to connect to the player's client.</param>
        public Player(Socket connection, string name)
        {
            Initialize(connection, name);
        }

        /// <summary>
        /// Creates a skeleton instance of the <see cref="ChrysopraseServer.Player"/> class, used to re-create a Player object in a different host.
        /// </summary>
        public Player(int id, string name, bool alive, long score, bool ready)
        {
            this.id = id;
            this.name = name;
            this.alive = alive;
            this.score = score;
            this.ready = ready;
        }

        /// <summary>
        /// Initializes the values of this instance with the provided connection and name.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="name"></param>
        public void Initialize(Socket connection, string name)
        {
            this.id = id_counter;
            this.name = name;
            this.connection = connection;
            this.alive = true;
            this.score = 0;
            this.ready = false;

            id_counter++;

            ReadyToggled += new ReadyToggledHandler(Player_ReadyToggled);
            PlayerLost += new PlayerLostHandler(Player_PlayerLost);
        }

        void Player_PlayerLost(object sender, EventArgs e)
        {
            
        }

        void Player_ReadyToggled(object sender, EventArgs e)
        {
            
        }

        /// <summary>Changes the ready state of this player.</summary>
        public void ToggleReadyState()
        {
            ready = !ready;
            ReadyToggled(this, new EventArgs());
        }

        /// <summary>Turns off the "alive" flag (sets it to false).</summary>
        public void Kill()
        {
            alive = false;
            PlayerLost(this, new EventArgs());
        }

        /// <summary>
        /// Gets the data of this player's instance.
        /// </summary>
        /// <returns>A string with the values of this instance's attributes, separated by a pipe.</returns>
        public string GetPlayerData() { return id + DataSeparator + name + DataSeparator + ((alive) ? "1" : "0") + score + ((ready) ? "1" : "0"); }

        public void SendPlayerData(string data)
        {
            try
            {
                connection.Send(Encoding.ASCII.GetBytes(data));
            }
            catch (SocketException s_ex)
            {
                Console.WriteLine("Socket exception while sending player data (ID: {0} Name: {1}) Message: {2}", ID, Name, s_ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception while sending player data (ID: {0} Name: {1}) Message: {2}", ID, Name, ex.Message);
            }
        }

        public static Player FromPlayerData(string player_data)
        {
            string[] data_array = player_data.Split('|');
            return (data_array.Length == 4) ?
                new Player(int.Parse(data_array[0]), data_array[1], data_array[2] == "1", long.Parse(data_array[3]), data_array[4] == "1") : null;
        }
    }
}
