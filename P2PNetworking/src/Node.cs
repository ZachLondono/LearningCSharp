using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace P2PNetworking {
 
	class Node {

		public static void WriteLine (string val) {
			Console.WriteLine($"[Thread: {Thread.CurrentThread.Name}] :: {val}");
		} 

		public static readonly byte Version = 1;
		public static readonly byte MinimumSupportedVersion = 1;
		
		public delegate void OnMessageTypeRecieved(ClientHandler source, MessageReceivedArgs args);
		
		private Dictionary<MessageType, OnMessageTypeRecieved> MessageMap;
		private DBInterface DBConnection { get; set; }
		private List<ClientHandler> Connections { get; set;}
		private readonly int Port;
		private readonly int Backlog;

		public int ActiveConnections {
			get => Connections.Count;
		}

		private short _refIdCounter = 0;
		private short ReferenceIdCounter  {

			get {
				// Lock the node when retreiving the next ReferenceId to avoid reusing the same refid during a session
				lock (this) {
					_refIdCounter++;
				}
				return _refIdCounter;
			}

		}

		public Node() : this(11000, 10, new SQLiteDBConnection(".")) {            
			// Start on default port
		}

		public Node(int port, int backlog, IDBInterface dbConnection) {

			if (Thread.CurrentThread.Name == null) {	
				Thread.CurrentThread.Name = "MainNodeThread";
				Node.WriteLine("Renamed Main Thread");
			} else Node.WriteLine("Main Node Thread already named " + Thread.CurrentThread.Name); 

			Port = port;
			Backlog = backlog;
			DBConnection = dbConnection;

			Node.WriteLine("Starting Node...");

			// List to store connections
			Connections = new List<ClientHandler>();

			Node.WriteLine("Opening Connection To Database...");

			// Map certain requests to specific function calls
			MessageMap = new Dictionary<MessageType, OnMessageTypeRecieved>();

			MessageMap.Add(MessageType.CONNECT, delegate(ClientHandler source, MessageReceivedArgs args) {				
				Node.WriteLine("Connection check message recieved");

				// This is the first message that a potential peer should send when attempting to connect.
				// The receiving node should check that this message has been received before accepting any further requests
				// When this message is received, the node should check that the connecting peer has not been blacklisted before allowing it to connect

				MessageHeader header = new MessageHeader();
				header.ProtocolVersion = Convert.ToByte(Node.Version);
				header.ContentType = MessageType.REQUEST_SUCCESSFUL;
				header.ContentLength = 0;
				header.Forward = false;
				header.ReferenceId = args.Header.ReferenceId;

				source.SendMessage(header, null);
				source.ConnectionAccepted = true;
			});
			
			MessageMap.Add(MessageType.REQUEST_PEERS, delegate(ClientHandler source, MessageReceivedArgs args) {
				Node.WriteLine("Peers requested");

				MessageHeader header = new MessageHeader();
				header.ProtocolVersion = Convert.ToByte(Node.Version);
				header.ContentType = MessageType.NOT_IMPLEMENTED;
				header.ContentLength = 0;
				header.Forward = false;
				header.ReferenceId = args.Header.ReferenceId;

				source.SendMessage(header, null);

			});

			MessageMap.Add(MessageType.REQUEST_RESOURCE, delegate(ClientHandler source, MessageReceivedArgs args) {
				Node.WriteLine("Resource requested");

				// Start initilizing the message response header
				MessageHeader header = new MessageHeader();
				header.ProtocolVersion = Convert.ToByte(Node.Version);
				header.Forward = false;
				header.ReferenceId = args.Header.ReferenceId;

				// Get the key from the request				
				var key = args.Content;

				if (key == null) {
					header.ContentType = MessageType.INVALID_REQUEST;
					header.ContentLength = 0;
					source.SendMessage(header, null);
					return;
				}

				// Query the database for the key	
				var command = DBConnection.CreateCommand();
				command.CommandText = $"SELECT value FROM data WHERE key = $key;";
				command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;

				byte[] buffer = null;
				using (var reader = command.ExecuteReader()) {
					if (reader.HasRows) {
						// If the record exists, add it to the response message
						buffer = GetBytes(reader);
						header.ContentType = MessageType.REQUEST_SUCCESSFUL;
						header.ContentLength = buffer.Length;
					} else {
						// If the record doesn't exist, repond with a RESOURCE_NOT_FOUND error
						header.ContentType = MessageType.RESOURCE_NOT_FOUND;
						header.ContentLength = 0;
					}
				}

				source.SendMessage(header, buffer);

			});

			MessageMap.Add(MessageType.CREATE_RESOURCE, delegate(ClientHandler source, MessageReceivedArgs args) {
				Node.WriteLine("Creation requested");

				MessageHeader header = new MessageHeader();
				header.ProtocolVersion = Convert.ToByte(Node.Version);
				header.Forward = false;
				header.ReferenceId = args.Header.ReferenceId;
				header.ContentLength = 0;

				var content = args.Content;
				if (content == null || content.Length <= 256) {
					header.ContentType = MessageType.INVALID_REQUEST;
					source.SendMessage(header, null);
					return;
				}

				byte[] key = new byte[256];
				byte[] value = new byte[content.Length - 256];

				System.Buffer.BlockCopy(content, 0, key, 0, key.Length);
				System.Buffer.BlockCopy(content, key.Length, value, 0, content.Length - key.Length);

				// Check if resource already exists
				var command = DBConnection.CreateCommand();
				command.CommandText = $"SELECT 1 FROM data WHERE key = $key LIMIT 1;";
				command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;
				using (var reader = command.ExecuteReader()) {
					if (reader.HasRows) {
						// Key already exists in db;
						header.ContentType = MessageType.RESOURCE_ALREADY_EXISTS;
						source.SendMessage(header, null);
						return;
					}
				}

				// Insert new resource into databse
				command = DBConnection.CreateCommand();
				command.CommandText = $"INSERT INTO data (key, value) VALUES ( $key ,  $value );";
				command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;
				command.Parameters.Add("$value", SqliteType.Blob, value.Length).Value = value;

				int rows = command.ExecuteNonQuery();

				header.ContentType = MessageType.RESOURCE_CREATED;
				source.SendMessage(header, null);

			});

			MessageMap.Add(MessageType.UPDATE_RESOURCE, delegate(ClientHandler source, MessageReceivedArgs args) {
				Node.WriteLine("Update requested");

				MessageHeader header = new MessageHeader();
				header.ProtocolVersion = Convert.ToByte(Node.Version);
				header.Forward = false;
				header.ReferenceId = args.Header.ReferenceId;
				header.ContentLength = 0;

				var content = args.Content;
				if (content == null || content.Length <= 256) {
					header.ContentType = MessageType.INVALID_REQUEST;
					source.SendMessage(header, null);
					return;
				}

				byte[] key = new byte[256];
				byte[] value = new byte[content.Length - 256];

				System.Buffer.BlockCopy(content, 0, key, 0, key.Length);
				System.Buffer.BlockCopy(content, key.Length, value, 0, content.Length - key.Length);

				// Insert new resource into databse
				command = DBConnection.CreateCommand();
				command.CommandText = $"INSERT INTO data (key, value) VALUES ( $key ,  $value );";
				command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;
				command.Parameters.Add("$value", SqliteType.Blob, value.Length).Value = value;

				int rows = command.ExecuteNonQuery();

				// If no rows where affected, the update failed
				if (rows == 0) header.ContentType = MessageType.RESOURCE_NOT_FOUND;
				else header.ContentType = MessageType.RESOURCE_UPDATED;
				source.SendMessage(header, null);

			});

			MessageMap.Add(MessageType.DELETE_RESOURCE, delegate(ClientHandler source, MessageReceivedArgs args) {
				Node.WriteLine("Deletion requested");

				MessageHeader header = new MessageHeader();
				header.ProtocolVersion = Convert.ToByte(Node.Version);
				header.Forward = false;
				header.ReferenceId = args.Header.ReferenceId;
				header.ContentLength = 0;

				var content = args.Content;
				if (content == null || content.Length <= 256) {
					header.ContentType = MessageType.INVALID_REQUEST;
					source.SendMessage(header, null);
					return;
				}

				byte[] key = new byte[256];
				System.Buffer.BlockCopy(content, 0, key, 0, key.Length);

				// Insert new resource into databse
				var command = DBConnection.CreateCommand();
				command.CommandText = $"DELETE FROM data WHERE key = $key";
				command.Parameters.Add("$key", SqliteType.Blob, key.Length).Value = key;

				int rows = command.ExecuteNonQuery();

				// If no rows where affected, the resource did not exist
				if (rows == 0)
					header.ContentType = MessageType.RESOURCE_NOT_FOUND;
				else header.ContentType = MessageType.RESOURCE_DELETED; 

				source.SendMessage(header, null);

			
			});


		}

		/// Begins listening for incoming connections
		public void ListenForConnections() {
			
			if (Thread.CurrentThread.Name == null) {	
				Thread.CurrentThread.Name = "ListeningThread";
				Node.WriteLine("Renamed Listening Thread");
			} else Node.WriteLine("Listening Node Thread already named " + Thread.CurrentThread.Name); 


			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
			IPAddress iPAddress= ipHostInfo.AddressList[0];
			IPEndPoint localEndPoint = new IPEndPoint(iPAddress, Port);

			Socket listener  = new Socket(iPAddress.AddressFamily, SocketType.Stream, 
			ProtocolType.Tcp);

			try {

	  			Node.WriteLine($"Listening for connections on: {Dns.GetHostName()}:{Port}");

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
            
			Node.WriteLine("Attempting to Connect to Known Peers");

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
					Node.WriteLine($"Attempting to Connect to Peer: {host}:{port}");

					try {

						// Connect to new peer
						ClientHandler handler = AddPeer(host, port);
						Node.WriteLine($"Successful Connection to Peer: {host}:{port}");

						MessageHeader header = new MessageHeader();
						header.ProtocolVersion = Convert.ToByte(Node.Version);
						header.ContentType = MessageType.CONNECT;
						header.ContentLength = 0;
						header.Forward = false;
						header.ReferenceId = ReferenceIdCounter;
	
						handler.SendRequest(header, null, delegate(MessageReceivedArgs args) {
							Node.WriteLine($"Response to connection request recieved: {args.Header.ContentType}");
							return true;
						});

 					} catch (Exception e) {
						
						Node.WriteLine(e.ToString());

						// Error connecting to new peer, remove it from peers list
						command = DBConnection.CreateCommand();
						command.CommandText = "DELETE FROM peers WHERE host = $host AND port = $port;";
						command.Parameters.AddWithValue("$host", host);
						command.Parameters.AddWithValue("$port", port);

						int row = command.ExecuteNonQuery();
						Node.WriteLine($"Removed peer {host}:{port} for failing to connect");
					}

				}

			}
		}

		public void OnMessageReceived(object source, MessageReceivedArgs message) {

			Node.WriteLine("Message Recieved");
			ClientHandler sourceHandler = (ClientHandler) source;

			if (message.Header.ContentType != MessageType.CONNECT && !sourceHandler.ConnectionAccepted) {
				// If the peer is sending a request, prior to requesting to connect, ignore the request
				return;
			}

			// call the correct function for the request
			OnMessageTypeRecieved value;
			if (MessageMap.TryGetValue(message.Header.ContentType, out value)) {
				// TODO: call this method in a new thread
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
				Node.WriteLine("Removing Stale Connection");
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

		public void SendRequest(MessageHeader message, byte[] content, ClientHandler.OnReceivedResponse responseDelegate) {

			foreach (ClientHandler peer in Connections) 
				peer.SendRequest(message, content, responseDelegate);

		}
 
		private byte[] GetBytes(SqliteDataReader reader) {

			// reads a blob of bytes from the reader in chunks of CHUNK_SIZE, then returns them in a single array
			const int CHUNK_SIZE = 2 * 1024;
			byte[] buffer = new byte[CHUNK_SIZE];
			long bytesRead;
			long fieldOffset = 0;

			using (MemoryStream stream = new MemoryStream()) {

				// Continue reading bytes, as long as there is more data to read
				while ((bytesRead = reader.GetBytes(0, fieldOffset, buffer, 1, buffer.Length)) > 0) {
					stream.Write(buffer, 0, (int)bytesRead);
					fieldOffset += bytesRead;					
				}

				// convert all the data to a single buffer
				return stream.ToArray();

			}

		}

	}

}
