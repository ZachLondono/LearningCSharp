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

			Node nodeA = new Node(11000, (content) => { 
				Console.WriteLine("Recieved From NodeA");
				return true;
			});			
			
			Node nodeB = new Node(11100, (content) => {
				Console.WriteLine("Receveived From Node B");
				return false;
			});
			Node nodeC = new Node(11110, (content) => {
				Console.WriteLine("Receveived From Node C");
				return false;
			});
			Node nodeD = new Node(11111, (content) => {
				Console.WriteLine("Receveived From Node D");
				return false;
			});

			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());	
			IPAddress ipAddress = ipHostInfo.AddressList[0];
			IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);
			
			var listenTask = nodeA.ListenAsync();
			
			await nodeB.ConnectAsync(remoteEP);
			await nodeC.ConnectAsync(remoteEP);
			await nodeD.ConnectAsync(remoteEP);
			
			// Delay to make sure all nodes are connected
			await Task.Delay(1000);			

			var sendTaskA = nodeA.SendAsync(new byte[]{0,0,0,0,0});
			var sendTaskB = nodeB.SendAsync(new byte[]{0,0,0,0,0});
			var sendTaskC = nodeC.SendAsync(new byte[]{0,0,0,0,0});
			var sendTaskD = nodeD.SendAsync(new byte[]{0,0,0,0,0});

			await Task.Delay(2000);			
		}
	
	}

	class FileShareNode : Node {

		private static FileShareNode _Instance = null;

		public static FileShareNode GetInstance() {
			if (_Instance == null) 	_Instance = new FileShareNode(11100);
			return _Instance;
		}

		private FileShareNode(int port) : base(port, ProcessMessage) {
		}

		private static bool ProcessMessage(byte[] content) {
			return false;
		}

		//private static Message ParseMessage(byte[] content) {
		//
		//	Converts bytes into a Message Object type
		//
		//}

		private byte[] GetData(byte[] key) {

			// Looks for a local version of the data
			// if it is not found it will ask peers for data

			return new byte[0];	

		}
		

		private void StoreData(byte[] key, byte[] val) {

			// Stores data localy 
			// Asks peers to store data

		}

	}

	class Node {
	
		public int Port { get; }
		private bool _listen;
		private bool _receive;
		private Socket Listener;
		private List<Peer> _ConnectedPeers = new List<Peer>();
		private List<Task> _ReceivingTasks = new List<Task>();
		private Predicate<byte[]> _onReceive;

		public Node(int port, Predicate<byte[]> onReceive) {
			Port = port;
			_onReceive = onReceive;
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

					// TODO: Add pause token so that the user can pause this task
					// https://devblogs.microsoft.com/pfxteam/cooperatively-pausing-async-methods/					

					Socket handler = Listener.Accept();
					
					Peer peer = new Peer(handler);	
					_ConnectedPeers.Add(peer);
					_ReceivingTasks.Add(ReceiveFromPeer(peer));
				}
			});

		}

		public async Task<Peer> ConnectAsync(IPEndPoint remoteEP) {
			Peer peer = new Peer();
			await peer.ConnectAsync(remoteEP);

			if (peer.HasErrored) {
				Console.WriteLine($"Failed to connect to remote end-point {peer.LastException.ToString()}");
				return null;
	 		} 
			
			_ConnectedPeers.Add(peer);
			_ReceivingTasks.Add(ReceiveFromPeer(peer));
			return peer;
		}

		public async Task SendAsync(byte[] msg) {
			List<Task> sendingTasks = new List<Task>();


			Console.WriteLine($"Sending {msg.Length} bytes to {_ConnectedPeers.Count} peers"); 
			_ConnectedPeers.ForEach((Peer peer) => {
				sendingTasks.Add(peer.SendAsync(msg));
			});

			await Task.WhenAll(sendingTasks);
		}

		private async Task ReceiveFromPeer(Peer peer) {
			_receive = true;
			while (_receive) {
				// TODO: Add pause token so that the user can pause this task
				// https://devblogs.microsoft.com/pfxteam/cooperatively-pausing-async-methods/					
				byte[] content = await peer.ReceiveAsync();
			
				await Task.Run(async () => {
					if (_onReceive(content)) {
						await SendAsync(content);
					}
				});

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
