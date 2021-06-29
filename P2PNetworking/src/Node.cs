using Microsoft.Data.Sqlite;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

using System.Runtime.InteropServices;

namespace P2PNetworking {
 
    class Node {

        static void Main(string[] args) {               

            Node peer;
            if (args.Length == 1) {

                Console.WriteLine(args[0]);
                int port = Convert.ToInt32(args[0]);

                peer = new Node(port, 10);

            } else {
 
                peer = new Node();
 
            }

            peer.ConnectToPeers();

            Thread listenerThread = new Thread(new ThreadStart(peer.ListenForConnections));

            listenerThread.Start();

        }

        public static readonly int Version = 1;
        public static readonly int MinimumSupportedVersion = 1;

        public delegate void OnMessageTypeRecieved(ClientHandler source, MessageReceivedArgs args);

        private Dictionary<MessageType, OnMessageTypeRecieved> MessageMap;
        private SqliteConnection DBConnection { get; set; }
        private List<ClientHandler> Connections { get; set;}
        private readonly int Port;
        private readonly int Backlog;

	public Node() : this(11000, 10) {            
		// Start on default port
	}

        public Node(int port, int backlog) {
		
		Port = port;
		Backlog = backlog;
		
		Console.WriteLine("Starting Peer...");
		
		// List to store connections
		Connections = new List<ClientHandler>();
		
		Console.WriteLine("Opening Connection To Database...");
		// Open new connection to database
		DBConnection = new SqliteConnection("Data Source=Peer.db");
		DBConnection.Open();
		
		// If the peers table does not exist (first time stating this peer), create it
		var command = DBConnection.CreateCommand();
		command.CommandText = 
		@"
		    CREATE TABLE IF NOT EXISTS peers (
		        host VARCHAR(100),
		        port INTEGER
		    );
		";
		Console.WriteLine("Creating peers Table if it does not yet exist");
		command.ExecuteNonQuery();
		
		MessageMap = new Dictionary<MessageType, OnMessageTypeRecieved>();
		
		MessageMap.Add(MessageType.CONNECT, delegate(ClientHandler source, MessageReceivedArgs args) {
			Console.WriteLine("Connection check message recieved");
			source.SendMessage(MessageType.REQUEST_SUCCESSFUL, null, false);
		});
		
		MessageMap.Add(MessageType.REQUEST_SUCCESSFUL, delegate(ClientHandler source, MessageReceivedArgs args) {
			Console.WriteLine("Successful Connection");
		});

        }

        /// Begins listening for incoming connections
        public void ListenForConnections() {

		IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
		IPAddress iPAddress= ipHostInfo.AddressList[0];
		IPEndPoint localEndPoint = new IPEndPoint(iPAddress, Port);

		Socket listener  = new Socket(iPAddress.AddressFamily, SocketType.Stream, 
		ProtocolType.Tcp);

		try {

                	Console.WriteLine($"Listening for connections on: {Dns.GetHostName()}:{Port}");

                	listener.Bind(localEndPoint);
                	listener.Listen(Backlog);

                	while (true) {

                		Socket handler = listener.Accept();
				AddPeer(handler);

                	}

		} catch (Exception e) {
			Console.Write("Unable to listen for connections " + e.ToString());
		}

        }

        /// Connects to all known peers stored in local database
        public void ConnectToPeers() {
            
		Console.WriteLine("Attempting to Connect to Known Peers");
		
		// Read all peer entries from table
		var command = DBConnection.CreateCommand();
		command.CommandText = 
		@"
		    SELECT host, port FROM peers;
		";
		
		
		using (var reader = command.ExecuteReader()) {
		
			while (reader.Read()) {                        
				var host = reader.GetString(0);
				var port = reader.GetInt32(1);
				Console.WriteLine("Attempting to Connect to Peer: {0}:{1}", host, port);
				
				try {
					
					// Connect to new peer
					ClientHandler handler = AddPeer(host, port);
					Console.WriteLine("Successful Connection to Peer: {0}:{1}", host, port);
					
					handler.SendMessage(MessageType.CONNECT, null, false);
				
				} catch (Exception e) {
					// Error connecting to new peer, remove it from peers list
					command = DBConnection.CreateCommand();
					command.CommandText = "DELETE FROM peers WHERE host = $host AND port = $port;";
					command.Parameters.AddWithValue("$host", host);
					command.Parameters.AddWithValue("$port", port);
					
					int row = command.ExecuteNonQuery();
					Console.WriteLine($"Removed peer {host}:{port} for failing to connect");
				}

			}

		}
        }

        public void OnMessageReceived(object source, MessageReceivedArgs message) {

		Console.WriteLine("Message Recieved");
		ClientHandler sourceHandler = (ClientHandler) source;
		
		// call the correct function for the request
		OnMessageTypeRecieved value;
		if (MessageMap.TryGetValue(message.Header.ContentType, out value)) {
			value(sourceHandler, message);
		}
		
		if (message.Header.Forward) {
		
			// TODO: check if this node has already seen this message, if it has, do not forward it
			// A node knows that it has not seen a message already if it has
			// 1. Time To Live has not already passed
			// 2. Has not seen a message with the same reference number from the same source


			// If message is marked to be forwarded, forward it to all connected peers
			foreach (ClientHandler connection in Connections) {
				if(!sourceHandler.Equals(connection)) {
					sourceHandler.SendMessage(message.Header, message.Content);
				}
			}
		
		}

        }


        public ClientHandler AddPeer(Socket socket) {

		// Start thread to handle communications with this socket
		ClientHandler handler = new ClientHandler(socket);
		// Handler called on message receive
		handler.MessageReceived += OnMessageReceived;
		// Handler called on peer disconnect
		handler.PeerDisconected += delegate (object o, EventArgs e) {
		    Console.WriteLine("Removing Stale Connection");
		    Connections.Remove((ClientHandler) o);
		};
		
		Thread handlerThread = new Thread(new ThreadStart(handler.Run));
		handlerThread.Start();
		
		Connections.Add(handler);
		
		return handler;
	}

	public ClientHandler AddPeer(string host, int port) {
            
		// Get information about the host
		IPHostEntry ipHostInfo = Dns.GetHostEntry(host); 
		IPAddress ipAddress = ipHostInfo.AddressList[0];  
		IPEndPoint remoteEP = new IPEndPoint(ipAddress,port);  
		
		// Create a socket to connect to remote host
		Socket peerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		
		// Attempt to connect
		peerSocket.Connect(remoteEP);
		
		// If no exception is thrown, add connection to list of connections
		return AddPeer(peerSocket);

	}

	public void SavePeer(string host, int port) {

		// store host and port in database, if they do not already exist
		SqliteCommand check = DBConnection.CreateCommand();
		
		// Check if peer already exists in table
		check.CommandText = "SELECT 1 FROM peers WHERE host = $host AND port = $port;";
		    check.Parameters.AddWithValue("$host", host);
		check.Parameters.AddWithValue("$port", port);
		
		using (var reader = check.ExecuteReader()) {
		    // If any rows are returned, it exists
		    if (reader.HasRows) return;
		}
		
		// Insert new peer into table
		SqliteCommand insert = DBConnection.CreateCommand();
		insert.CommandText = "INSERT INTO peers (host, port) VALUES ($host, $port);";
		    insert.Parameters.AddWithValue("$host", host);
		insert.Parameters.AddWithValue("$port", port);
		
		int rows = insert.ExecuteNonQuery();
		if (rows < 1) Console.Write("Error saving new peer");

	}

    }

}
