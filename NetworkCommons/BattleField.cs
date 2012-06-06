using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetworkCommons
{
    public delegate void BattleFieldFilledHandler(object sender, EventArgs e);
    public delegate void StateChangedHandler(object sender, EventArgs e);

    /// <summary>
    /// 
    /// </summary>
    public class BattleFieldState
    {

        public enum RoomState { Waiting, Playing }


        // Class attributes

        /// <summary>The number of player allowed for this battlefield.</summary>
        protected int max_players;

        /// <summary>List of players.</summary>
        protected Player[] players;

        /// <summary>Gets the state of the BattleField.</summary>
        protected RoomState state;

        /// <summary>A string containing the message to be sent to the clients</summary>
        protected string message_buffer;

        protected byte[] received_data;

        // Class properties
        /// <summary>Gets the max number of players allowed for this BattleField.</summary>
        public int MaxPlayers { get { return max_players; } }

        /// <summary>Gets the list of players in the battlefield</summary>
        public Player[] Players { get { return players; } }

        /// <summary>Gets the number of players connected to this battlefield.</summary>
        public int PlayerCount
        {
            get
            {
                int count = 0;
                foreach (Player p in players) count += (p != null) ? 1 : 0;
                return count;
            }
        }

        public RoomState State { get { return state; } set { state = value; StateChanged(this, new EventArgs()); } }

        // Properties

        /// <summary>
        /// Determines if this instance of <see cref="ChrysopraseServer.BattleField"/> has no players connected to it.
        /// </summary>
        public bool Empty
        {
            get
            {
                bool empty = true;
                foreach (Player p in players)
                    empty &= (p == null);
                return empty;
            }
        }

        /// <summary>
        /// Determines if this instance of <see cref="ChrysopraseServer.BattleField"/> has an available slot.
        /// </summary>
        public bool HasAvailableSlot
        {
            get
            {
                bool available = false;
                foreach (Player p in players)
                    available |= (p == null);
                return available;
            }
        }

        /// <summary>Determines if the BattleField is full, meaning the players list has no available slots.</summary>
        public bool Full { get { return !HasAvailableSlot; } }

        /// <summary>Determines if all the players in the Battlefield have notified the server that they're ready.</summary>
        public bool EveryoneReady
        {
            get
            {
                bool answer = Full;
                if (answer)
                    foreach (Player p in players)
                        answer &= p.Ready;
                return answer;
            }
        }

        /// <summary>Determines if there's a winner decided, which means only one player is still playing.</summary>
        public bool WinnerDecided
        {
            get
            {
                bool correct_state = (state == RoomState.Playing);
                bool one_standing = false;
                foreach (Player p in players)
                    one_standing ^= (p != null && p.Alive);
                return one_standing && correct_state;
            }
        }

        // Events
        public event StateChangedHandler StateChanged;
    }


    /// <summary>
    /// This class represents a single battlefield with a set of custom rules, number of players, settings, etc.
    /// </summary>
    public class BattleField : BattleFieldState
    {
        // Class settings
        public static int DefaultBufferSize = 256;
        public static int DefaultMaxPlayers = 2;
        public static string WaitingKey = "waiting";
        public static string StartKey = "start";
        public static string PlayingKey = "playing";

        // Class attributes

        /// <summary>The thread that will attend the execution of the game being held in this battlefield.</summary>
        private Thread idler, game_attendant;

        // Events
        public event BattleFieldFilledHandler BattleFieldFilled;


        /// <summary>
        /// Creates a new instance of the <see cref="ChrysopraseServer.BattleField"/> class with default values.
        /// </summary>
        public BattleField()
        {
            Initialize(DefaultMaxPlayers);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ChrysopraseServer.BattleField"/> class with the specified max number of players.
        /// </summary>
        public BattleField(int max_players)
        {
            Initialize(max_players);
        }

        /// <summary>
        /// Initializes the values of this battle field.
        /// </summary>
        /// <param name="max_players">The maximum number of players allowed for this battle field.</param>
        public void Initialize(int max_players)
        {
            this.max_players = max_players;
            this.players = new Player[max_players];
            this.state = RoomState.Waiting;
            this.BattleFieldFilled += new BattleFieldFilledHandler(BattleField_BattleFieldFilled);
            this.StateChanged += new StateChangedHandler(BattleField_StateChanged);

            this.idler = new Thread(new ThreadStart(Wait));
            this.idler.Start();
        }

        void BattleField_StateChanged(object sender, EventArgs e)
        {

        }

        void BattleField_BattleFieldFilled(object sender, EventArgs e)
        {
            
        }

        /// <summary>
        /// Attempts to allocate a player in the players list.
        /// </summary>
        /// <param name="p">The player to welcome in the Battlefield.</param>
        /// <returns><c>True</c> if the player was successfully placed in a free slot.</returns>
        public bool AcceptPlayer(Player p)
        {
            bool found_free_slot = false;
            if (HasAvailableSlot)
                for (int i = 0; i < players.Length; i++)
                {
                    found_free_slot = (players[i] == null);
                    if (found_free_slot)
                    {
                        players[i] = p;
                        players[i].ReadyToggled += new ReadyToggledHandler(BattleField_ReadyToggled);
                        players[i].PlayerLost += new PlayerLostHandler(BattleField_PlayerLost);
                        break;
                    }
                }
            if (Full) BattleFieldFilled(this, new EventArgs());
            return found_free_slot;
        }

        void BattleField_PlayerLost(object sender, EventArgs e)
        {
            
        }

        /// <summary>
        /// Triggered when a player changes his ready state.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void BattleField_ReadyToggled(object sender, EventArgs e)
        {
            if (EveryoneReady)
            {
                game_attendant = new Thread(new ThreadStart(StartGame));
                game_attendant.Start();
            }
        }

        /// <summary>
        /// Subprocess to keep active the idle connections between connected clients and the server while waiting for the game to start
        /// </summary>
        private void Wait()
        {
            byte[] waiting = Encoding.ASCII.GetBytes(WaitingKey);
            byte[] start = Encoding.ASCII.GetBytes(StartKey);
            byte[] playing = Encoding.ASCII.GetBytes(PlayingKey);
            string data_to_send = "";

            while (State == RoomState.Waiting)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    data_to_send += (data_to_send.Length == 0) ? "" : "|";
                    players[i] = ReceivePlayerData(players[i]);
                    data_to_send += Encoding.ASCII.GetString(received_data);
                }

                for (int i = 0; i < players.Length; i++)
                {
                    players[i].Connection.Send(waiting);
                    players[i].SendPlayerData(data_to_send);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        private Player ReceivePlayerData(Player p)
        {
            Player temp_player;
            Socket temp_socket;
            string player_data;

            received_data = new byte[DefaultBufferSize];
            try
            {
                p.Connection.Receive(received_data);
                player_data = Encoding.ASCII.GetString(received_data);
                temp_socket = p.Connection;
                temp_player = Player.FromPlayerData(player_data);
                p = (temp_player != null) ? temp_player : p;
                p.Connection = temp_socket;
            }
            catch (SocketException s_ex)
            {
                Console.WriteLine("Socket exception while receiving player data (ID: {0} Name: {1}) Message: {2}", p.ID, p.Name, s_ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception while receiving player data (ID: {0} Name: {1}) Message: {2}", p.ID, p.Name, ex.Message);
            }
            return p;
        }

        /// <summary>
        /// Subprocess to handle the game clients' requests and update the status of the Battlefield according to the messages received from the players.
        /// </summary>
        private void StartGame()
        {
            State = RoomState.Playing;
            while (!Empty && State == RoomState.Playing && !WinnerDecided)
            {
                foreach (Player p in players)
                {

                }
            }
        }
    }
}
