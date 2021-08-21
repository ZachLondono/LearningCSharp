using System;
using P2PNetworking;
using System.Threading.Tasks;
using System.Net;

namespace P2PTesting { 

	public class Testing {

		static async Task Main(string[] args) {

			Node nodeB = new Node(11000, 
					(state) => {
						Console.WriteLine("Request Received");
						state.Respond(new byte[]{123});
						return false;
					},(state) => {
						return false;
					});

			Node nodeA = new Node(10101, 
					(state) => {
						return false;
					},(state) => {
						return false;
					});


			var listenTask = nodeB.ListenAsync();

			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());	
			IPAddress ipAddress = ipHostInfo.AddressList[0];
			IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);

			await nodeA.ConnectAsync(remoteEP);

			byte[] response = await nodeA.RequestAsync(new byte[]{222});

			Console.WriteLine($"Response Received {response[0]}");

		}

	}
/*
		static void NodeTest1(string[] args) {
			if (args.Length > 0) {
				Node node = new Node(11111, 10, new SQLiteDBConnection("."), new ClientHandlerFactory());

				Thread t = new Thread(() =>{
					node.ListenForConnections();
				});

				t.Start();

				byte[] key = Encoding.ASCII.GetBytes("Hello");
				byte[] content = Encoding.ASCII.GetBytes("World");

				byte[] data = new DataPair(key, content).GetEncoded();

				Thread.Sleep(5000);

				MessageHeader header = new MessageHeader();
				header.ContentType = MessageType.CREATE_RESOURCE;
				header.Forward = false;
				header.ProtocolVersion = Node.Version;
				header.ReferenceId = 0;
				header.ContentLength = data.Length;

				node.SendRequest(header, data, (args) => {
					Console.WriteLine($"Recieved {args.Header.ContentType}");
					return false;
				});
		
				
			} else {	
				Node node = new Node(33333, 10, new SQLiteDBConnection("."), new ClientHandlerFactory());
				node.ConnectToPeers();
			}
		}

		static void TestSuite(string[] args) {               

			IDBInterface dbConnection = new SQLiteDBConnection(".");

			byte[] testKey = Encoding.ASCII.GetBytes("Hello");
			byte[] testValue = Encoding.ASCII.GetBytes("World");
			byte[] testValue2 = Encoding.ASCII.GetBytes("World!!");

			DataPair pair = new DataPair(testKey, testValue);
			DataPair pair2 = new DataPair(testKey, testValue2);


			System.Console.WriteLine("=================================");
			System.Console.WriteLine("Testing data table commands");
			var result = ' ';

			// Check if data contains key, before inserting it
			result = dbConnection.ContainsKey(pair.Key) == false ? '✓' : 'x';
			Console.WriteLine($"Check for pair before insert:\t{result}");

			// Check if pair is inserted into data
			result = dbConnection.InsertPair(pair) == true ? '✓' : 'x';
			Console.WriteLine($"Insert new pair:\t{result}");

			// Check if data contains key, after inserting it
			result = dbConnection.ContainsKey(pair.Key) == true ? '✓' : 'x';
			Console.WriteLine($"Check for pair after insert:\t{result}");

			// Try to insert duplicate key
			result = dbConnection.InsertPair(pair) == false ? '✓' : 'x';
			Console.WriteLine($"Try insert duplicate key:\t{result}");

			// Read value from known key
			var a = dbConnection.SelectData("value", "key", pair.Key);
			result = IsEqualArr(a, pair.Value) ? '✓' : 'x';
			Console.WriteLine($"Get value from key:\t{result}");

			// Update value
			result = dbConnection.UpdatePair(pair2) ? '✓' : 'x';
			Console.WriteLine($"Update value:\t{result}");
			
			// Read updated value
			var c = dbConnection.SelectData("value", "key", pair.Key);
			result = IsEqualArr(c, pair2.Value) ? '✓' : 'x';
			Console.WriteLine($"Get updated value:\t{result}");

			// Read key from known value
			var b = dbConnection.SelectData("key", "value", pair2.Value);
			if (b == null) result = 'x';
			else result = IsEqualArr(b, pair2.Key) ? '✓' : 'x';
			Console.WriteLine($"Get key from value:\t{result}");

			// Delete key
			result = dbConnection.RemoveKey(pair.Key) == true ? '✓' : 'x';
			Console.WriteLine($"Remove key:\t{result}");

			// Check if data contains key, after removing it
			result = dbConnection.ContainsKey(pair.Key) == false ? '✓' : 'x';
			Console.WriteLine($"Check for key after delete:\t{result}");
			System.Console.WriteLine("=================================");

			
			string host = "Hello";
			int port = 111111;			
			string host2 = "Hello2";
			int port2 = 111112;			
			
			Peer peer = new Peer(host, port);
			Peer peer2 = new Peer(host, port2);
			Peer peer3 = new Peer(host2, port);
			
			System.Console.WriteLine("=================================");
			System.Console.WriteLine("Testing peer table commands");
			// Check if data contains key, before inserting it
			result = dbConnection.ContainsPeer(peer) == false ? '✓' : 'x';
			Console.WriteLine($"Check for non-existing peer:\t{result}");

			// Check if pair is inserted into data
			result = dbConnection.InsertPeer(peer) == true ? '✓' : 'x';
			Console.WriteLine($"Inserting new peer:\t{result}");

			// Check if data contains key, after inserting it
			result = dbConnection.ContainsPeer(peer) == true ? '✓' : 'x';
			Console.WriteLine($"Check for new peer:\t{result}");

			// Try to insert duplicate key
			result = dbConnection.InsertPeer(peer) == false ? '✓' : 'x';
			Console.WriteLine($"Try to insert duplicate peer:\t{result}");

			// Insert peer at same host, different port
			result = dbConnection.InsertPeer(peer2) == true ? '✓' : 'x';
			Console.WriteLine($"Insert new peer w/ same host:\t{result}");

			// Insert peer at same host, different port
			result = dbConnection.InsertPeer(peer3) == true ? '✓' : 'x';
			Console.WriteLine($"Insert new peer w/ same port:\t{result}");

			// Delete peer
			result = dbConnection.RemovePeer(peer) == true ? '✓' : 'x';
			Console.WriteLine($"Remove peer1:\t{result}");

			result = dbConnection.RemovePeer(peer2) == true ? '✓' : 'x';
			Console.WriteLine($"Remove peer2:\t{result}");
			
			result = dbConnection.RemovePeer(peer3) == true ? '✓' : 'x';
			Console.WriteLine($"Remove peer3:\t{result}");

			// Check if data contains key, after removing it
			result = dbConnection.ContainsPeer(peer) == false ? '✓' : 'x';
			Console.WriteLine($"Check for peer1 after delete:\t{result}");
			System.Console.WriteLine("=================================");
		
			System.Console.WriteLine("=================================");
			System.Console.WriteLine("Testing Nodes");
			
			var handlerFactory = new ClientHandlerFactory();
			var node = new Node(11000, 4, dbConnection, handlerFactory);
			
			dbConnection.InsertPeer(new Peer("DESKTOP-QFS6RF2", 11111));
			dbConnection.InsertPeer(new Peer("DESKTOP-QFS6RF2", 22222));
			
			// Testing outgoing connections
			Thread outgoing = new Thread(new ThreadStart(Listen));
			outgoing.Start();

			Thread creation = new Thread(new ThreadStart(StoreData));
			creation.Start();
			
			// Need to add ability to connect to a specific peer
			node.ConnectToPeers();
			
			result = node.ActiveConnections == 2 ? '✓' : 'x';
			Console.WriteLine($"Outgoing Connections: {result}");
			outgoing.Join();
			creation.Join();

			Console.WriteLine("Hello?");

			// Check that node sends starting connection request

		}

		private static IClientHandler CreateHandler(int port) {
			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
			IPAddress addy = ipHostInfo.AddressList[0];
			IPEndPoint localEp = new IPEndPoint(addy, port);
			
			Socket socket = new Socket(addy.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.Bind(localEp);
			socket.Listen(1);
			
			Socket handlerS = socket.Accept();
			var handler = new ClientHandlerFactory().BuildClientHandler(handlerS);
			return handler;
		}

		private static void Listen() {
			
			IClientHandler handler = CreateHandler(11111);

			handler.SetOnMessageRecieve((source, args) => {
				var result = args.Header.ContentType == MessageType.CONNECT ? '✓' : 'x';
				Console.WriteLine($"Connection Request On Connect: {result}");
				((ClientHandler)source).Disconnect();
			});

			handler.Run();
		
		}

		private static void StoreData() {
			
			IClientHandler handler = CreateHandler(22222);

			byte[] key = Encoding.ASCII.GetBytes("Hello");
			byte[] content = Encoding.ASCII.GetBytes("World");

			byte[] data = new DataPair(key, content).GetEncoded();

			MessageHeader header = new MessageHeader();
			header.ContentType = MessageType.CREATE_RESOURCE;
			header.Forward = false;
			header.ProtocolVersion = Node.Version;
			header.ReferenceId = 0;
			header.ContentLength = data.Length;

			handler.SetOnMessageRecieve((source, args) => {						

				if (args.Header.ContentType == MessageType.CONNECT) {

					MessageHeader connHeader = new MessageHeader();
					connHeader.ProtocolVersion = Convert.ToByte(Node.Version);
					connHeader.ContentType = MessageType.REQUEST_SUCCESSFUL;
					connHeader.ContentLength = 0;
					connHeader.Forward = false;
					connHeader.ReferenceId = args.Header.ReferenceId;

					((ClientHandler)source).SendMessage(connHeader, null);
					((ClientHandler)source).ConnectionAccepted = true;

					Thread.Sleep(1000);
					((ClientHandler)source).SendMessage(header, data);
				} else {
					var result = args.Header.ContentType == MessageType.RESOURCE_CREATED ? '✓' : 'x';
					Console.WriteLine($"Resource Creation: {result}");
					((ClientHandler)source).Disconnect();
				}


			});

			handler.Run();

		}

		private static void ConnectToNode() {
			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
			IPAddress addy = ipHostInfo.AddressList[0];
			IPEndPoint remoteEp = new IPEndPoint(addy, 11000);
			Socket socket = new Socket(addy.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(remoteEp);
			// Give enought time for node thread to make connection
			Thread.Sleep(500);
		}

		private static void printByteArr(byte[] arr) {
			
			for (int i = 0; i < arr.Length; i++ ) {
				Console.Write(arr[i] + " ");				
			}
			Console.WriteLine();

		}

		private static bool IsEqualArr(byte[] a, byte[] b) {

			if (a.Length != b.Length) return false;

			for (int i = 0; i < a.Length; i++) {
				
				if (a[i] != b[i]) {
					return false;
				}

			}

			return true;

		}

		public class TestHandlerFactory : IClientHandlerFactory {
			public IClientHandler BuildClientHandler(Socket socket) {
				IClientHandler handler = new TestHandler();
				return handler;
			}
		}

		public class TestHandler : IClientHandler {
			public bool ConnectionAccepted { get; set; }
			public void Disconnect() {

			}

			public void SendMessage(MessageHeader header, byte[] content) {
				
				if (content == null) content = new byte[0];
				Console.WriteLine("---- Sending Message ----");
				Console.WriteLine($"type:{header.ContentType} fwd:{header.Forward} len:{content.Length} ver:{header.ProtocolVersion} ref:{header.ReferenceId}");
				Console.WriteLine("-------------------------");
			}

			public void SendRequest(MessageHeader header, byte[] content, OnReceivedResponse onResponse) {
				Console.WriteLine("---- Sending Request ----");
				Console.WriteLine($"type:{header.ContentType} fwd:{header.Forward} len:{content.Length} ver:{header.ProtocolVersion} ref:{header.ReferenceId}");
				Console.WriteLine("-------------------------");
			}

			public void SetOnMessageRecieve(MessageReceiveHandler handler) {
			}

			public void SetOnPeerDisconected(PeerDisconectHandler handler) {
			}

			public void Run() {
			}

		}
	}*/

}
