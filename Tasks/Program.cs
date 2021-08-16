using System;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace Tasks {
	
	class Program {
		
		static async Task Main(string[] args) {

			//Console.WriteLine("============================");
			//Console.WriteLine("Starting Peer Test");
			//await PeerTest();
	
			Console.WriteLine("============================");
			Console.WriteLine("Starting Node Test");
			await NodeTest();

			Console.WriteLine("============================");
		}

		static async Task NodeTest() {

			Node nodeA = new Node(11000);			
			var listenTask = nodeA.ListenAsync();
		

			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());	
			IPAddress ipAddress = ipHostInfo.AddressList[0];
			IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);
			
			Node nodeB = new Node(11100);
			await nodeB.ConnectAsync(remoteEP);
			Node nodeC = new Node(11110);
			await nodeC.ConnectAsync(remoteEP);
			Node nodeD = new Node(11111);
			await nodeD.ConnectAsync(remoteEP);

			var sendTask = nodeA.SendAsync(new byte[]{0,0,0,0,0});

			nodeA.StopReceiving();
			nodeB.StopReceiving();
			nodeC.StopReceiving();
			nodeD.StopReceiving();
			nodeA.StopListening();
			//await listenTask;

		}

		static async Task PeerTest() {
			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());	
			IPAddress ipAddress = ipHostInfo.AddressList[0];
			IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);
			IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);
		
			Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			// Start listening for connection
			Thread t = new Thread(new ThreadStart( () => {
					Console.WriteLine("Starting to Listen");
					
					listener.Bind(localEndPoint);		
					listener.Listen(100);					
					
					Console.WriteLine("Waiting for a connection...");
					
					Socket handler = listener.Accept();
					
					int recieveSize = 1024;
					byte[] buffer = new byte[recieveSize];
					try {
						int received = handler.Receive(buffer, 0, recieveSize, SocketFlags.None);
						Console.WriteLine($"Received {received} bytes");
					

						byte[] temp = new byte[received];
						Array.Copy(buffer, 0, temp, 0, received);
						handler.Send(temp, 0, temp.Length, SocketFlags.None);
						Console.WriteLine($"Sent {temp.Length} bytes");

					} catch (Exception e) {
						Console.WriteLine("Failed to recieve");
						Console.WriteLine(e.ToString());
					}

				}));
		
			t.Start();

			// Send data through peer
			Peer peer = new Peer();
			await peer.ConnectAsync(remoteEP);			

			if (peer.HasErrored) {
				Console.WriteLine($"Failed to connect {peer.LastException.ToString()}");
				return;
			}

			byte[] msg = Encoding.ASCII.GetBytes("Hello World");
			peer.BeginSend(msg);
			Console.WriteLine($"Total Bytes Sent: {peer.BytesSent}");
			
			/*peer.BeginReceive((content) => {
				Console.WriteLine($"Total Bytes Recieved: {content.Length}");
			});*/

			var receiveTask = peer.ReceiveAsync();
			var receive = await receiveTask;
			Console.WriteLine($"Total Bytes Recieved: {receiveTask.Result.Length}");
			
			// Join thread, and terminate
			t.Join();

		}

	}

	class Node {
	
		public int Port { get; }
		private bool _listen;
		private bool _receive;
		private Socket Listener;
		private List<Peer> _ConnectedPeers = new List<Peer>();


		public Node(int port) {
			
			// TODO: 
			// receive message from connected peers
			Port = port;
			_listen = false;
			_receive = false;

		}
		
		public void StopListening() {
			_listen = false;	
		}
		
		public void StopReceiving() {
			_receive = false;
		}
		
		public async Task ListenAsync() {
			
			_listen = true;
			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());	
			IPAddress ipAddress = ipHostInfo.AddressList[0];
			IPEndPoint localEndPoint = new IPEndPoint(ipAddress, Port);
			
			Listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			Listener.Bind(localEndPoint);		
			Listener.Listen(100);					
		
			await Task.Run(() => {
				while (_listen) {
					Socket handler = Listener.Accept();
					
					Peer peer = new Peer(handler);	
					_ConnectedPeers.Add(peer);
					var receivingTask = RecieveFromPeer(peer);
				}
			});

		}

		public async Task ConnectAsync(IPEndPoint remoteEP) {
			Peer peer = new Peer();
			await peer.ConnectAsync(remoteEP);

			if (peer.HasErrored) {
				Console.WriteLine($"Failed to connect to remote end-point {peer.LastException.ToString()}");
				return;
	 		} 
			
			_ConnectedPeers.Add(peer);
			var receivingTask = RecieveFromPeer(peer);
		}

		public async Task SendAsync(byte[] msg) {
			List<Task> sendingTasks = new List<Task>();


			Console.WriteLine($"Sending {msg.Length} bytes to {_ConnectedPeers.Count} peers"); 
			_ConnectedPeers.ForEach((Peer peer) => {
				sendingTasks.Add(peer.SendAsync(msg));
			});

			await Task.WhenAll(sendingTasks);
		}

		public async Task RecieveFromPeer(Peer peer) {
			_receive = true;
			while (_receive) {
				byte[] content = await peer.ReceiveAsync();
				Console.WriteLine($"Bytes recieved {content.Length}");
			}
		}

	}

	class Peer {
		
		private Socket Connection;
		private bool _isConnected;
		private int _received;
		private int _sent;
		private bool _hasErrored;
		private Exception _lastException;
		public bool Connected { get => _isConnected; }
		public int BytesSent { get => _sent; }
		public int BytesReceived { get => _received; }
		public bool HasErrored { get => _hasErrored; }
		public Exception LastException { get => _lastException; }
		
		private class ReceiveState {
			public const int BufferSize = 1024;
			public byte[] Buffer = new byte[BufferSize];		
			public Action<byte[]> OnReceive;
		}

		public Peer(Socket connection) {
			Connection = connection;
			var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
			var ipAddress = ipHostInfo.AddressList[0];
			_isConnected = false;
			_sent = 0;	
			_received = 0;
		}

		public Peer() {
			var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
			var ipAddress = ipHostInfo.AddressList[0];
			_isConnected = false;
			_sent = 0;	
			_received = 0;
			
			Connection = new Socket(ipAddress.AddressFamily,
						SocketType.Stream,
						ProtocolType.Tcp);
		}
		
		public Peer(IPEndPoint remoteEP) : this() {

			Connection.BeginConnect(remoteEP, 
					(obj) => { _isConnected = true; }, Connection);		

		}

		public async Task ConnectAsync(IPEndPoint remoteEP) {
			
			if (_isConnected) return;
	
			await Task.Run(() => {
				try {
					Connection.Connect(remoteEP);
					_isConnected = true;
				} catch (Exception e) {
					_hasErrored = true;
					_lastException = e;
				}
			});

		}

		public async Task SendAsync(byte[] msg) {
	
			await Task.Run(() => {
				try {
					_sent += Connection.Send(msg, 0, msg.Length, SocketFlags.None);
				} catch (Exception e) {
					_hasErrored = true;
					_lastException = e;
				}
			});

		}

		public async Task<byte[]> ReceiveAsync() {

			byte[] content = null;			
			await Task.Run(() => {

				int bufferSize = 1024;
				byte[] buffer = new byte[bufferSize];
				try {

					int received = Connection.Receive(buffer, 0, bufferSize, SocketFlags.None);
					byte[] temp = new byte[received]; 
					Array.Copy(buffer, 0, temp, 0, received);
					_received += received;
					content = temp;

				} catch (Exception e) {
					_hasErrored = true;
					_lastException = e;
				}


			});

			return content;

		}

		public void BeginSend(byte[] msg) {
			if (!_isConnected) return;
			Connection.BeginSend(msg, 0, msg.Length, SocketFlags.None,
				new AsyncCallback(SendCallback), Connection);
		}

		private void SendCallback(IAsyncResult ar) {
			try {
				_sent += Connection.EndSend(ar);
			} catch (Exception e) {
				_hasErrored = true;
				_lastException = e;
			}
		}

		public void BeginReceive(Action<byte[]> onReceive) {
			if (!_isConnected) return;

			var state = new ReceiveState();
			state.OnReceive = onReceive;

			Connection.BeginReceive(state.Buffer, 0, ReceiveState.BufferSize, 0, 
					new AsyncCallback(ReadCallback), state);

		}

		private void ReadCallback(IAsyncResult ar) {
			
			ReceiveState state = (ReceiveState) ar.AsyncState;
			
			int bytesRead = Connection.EndReceive(ar);
			
			_received += bytesRead;

			byte[] content = new byte[bytesRead];
			Array.Copy(state.Buffer, 0, content, 0, bytesRead);

			state.OnReceive(content);

		}

	}

}
