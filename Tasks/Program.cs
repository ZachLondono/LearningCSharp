using System;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Tasks {
	
	class Program {
		
		static async Task Main(string[] args) {
			Console.WriteLine("============================");
			Console.WriteLine("Starting Node Test");
			await NodeTest(args.Length > 0);
			Console.WriteLine("============================");
		}

		static async Task NodeTest(bool A) {
			
			// TODO : do not give direct access to peer, instead give a mechanism to respond
			Node nodeA = new Node(11000, (State) => { 
				Console.WriteLine($"A: Request Received: {State.Content}");
				State.Respond(new byte[]{123});
				return false;
			}, (State) => {
				Console.WriteLine($"A: Broadcast Received: {State.Content}");
				return false;
			});			
			
			/*Node nodeB = new Node(11100, (State) => {
				Console.WriteLine($"B: Request Received: {State.Content}");
				return false;
			}, (State) => {	
				Console.WriteLine($"B: Broadcast Received: {State.Content}");
				return false;
			});*/
			
			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());	
			IPAddress ipAddress = ipHostInfo.AddressList[0];
			IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);

			if (A) {		
				var listenTask = nodeA.ListenAsync();
				while (true) {}
			} else {
				await nodeA.ConnectAsync(remoteEP);
				byte[] response = await nodeA.RequestAsync(new byte[]{1});
				if (response != null)
					Console.WriteLine($"Response: {response[0]}");
				else Console.WriteLine("null");
			}

		}
	
	}

	class FileShareNode : Node {

		private static FileShareNode _Instance = null;
	
		// A list of file keys which this node is waiting to receive
		private List<byte[]> PendingFiles = new List<byte[]>();

		public static FileShareNode GetInstance() {
			if (_Instance == null) 	_Instance = new FileShareNode(11100);
			return _Instance;
		}

		private FileShareNode(int port) : base(port, ProcessMessage, ProcessMessage) {
		}

		private static bool ProcessMessage(ReceiveState content) {
			Console.WriteLine("Message Received");
			return false;
		}
	
		private byte[] GetData(byte[] key) {
			return new byte[] {};
		}

		private void StoreData(byte[] key, byte[] val) {
		}

	}

	

	class Node {
	
		public struct ReceiveState {
			public byte[] Content { get; } 
			private Peer peer;
			private int requestId;
			public ReceiveState(byte[] received, Peer receivedFrom, int requestId) {
				Content = received;
				peer = receivedFrom;
				this.requestId = requestId;
			}
			
			public async void Respond (byte[] response) {

				ResponseHeader responseHeader = new ResponseHeader(requestId, response.Length);

				MessageHeader messageHeader = new MessageHeader(new Random().Next(), MessageType.Response, Marshal.SizeOf(responseHeader) + response.Length);
				
				await peer.SendAsync(MessageHeader.GetBytes(messageHeader));
				await peer.SendAsync(ResponseHeader.GetBytes(responseHeader));
				await peer.SendAsync(response);
			}

		}
		
		public enum MessageType : byte{
			Broadcast = 2,
			Request = 1,
			Response = 0
		}
		
		public struct MessageHeader {
			public int Id { get; }
			public MessageType Type { get; }
			public int MsgLen { get; }
			public MessageHeader(int id, MessageType type, int len) {
				Id = id;
				Type = type;
				MsgLen = len;
			}

			public static byte[] GetBytes(MessageHeader header) {
				int size = Marshal.SizeOf(header);
				byte[] arr = new byte[size];

				IntPtr ptr = Marshal.AllocHGlobal(size);
				Marshal.StructureToPtr(header, ptr, true);
				Marshal.Copy(ptr, arr, 0, size);
				Marshal.FreeHGlobal(ptr);
				return arr;
			}

			public static MessageHeader FromBytes(byte[] bytes) {
				MessageHeader header = new MessageHeader();
				int size = Marshal.SizeOf(header);
				IntPtr ptr = Marshal.AllocHGlobal(size);
				Marshal.Copy(bytes, 0, ptr, size);
				header = (MessageHeader) Marshal.PtrToStructure(ptr, header.GetType());
				Marshal.FreeHGlobal(ptr);

				return header;
			}

		}

		public struct ResponseHeader {
			public int RefId { get; }
			public int Length { get; }

			public ResponseHeader(int refId, int length) {
				RefId = refId;
				Length = length;
			}

			public static byte[] GetBytes(ResponseHeader header) {
				int size = Marshal.SizeOf(header);
				byte[] arr = new byte[size];

				IntPtr ptr = Marshal.AllocHGlobal(size);
				Marshal.StructureToPtr(header, ptr, true);
				Marshal.Copy(ptr, arr, 0, size);
				Marshal.FreeHGlobal(ptr);
				return arr;
			}

			public static ResponseHeader FromBytes(byte[] bytes) {
				ResponseHeader header = new ResponseHeader();
				int size = Marshal.SizeOf(header);
				IntPtr ptr = Marshal.AllocHGlobal(size);
				Marshal.Copy(bytes, 0, ptr, size);
				header = (ResponseHeader) Marshal.PtrToStructure(ptr, header.GetType());
				Marshal.FreeHGlobal(ptr);

				return header;
			}
		}
	
		public int Port { get; }
		private Socket Listener;
		protected List<Peer> _ConnectedPeers = new List<Peer>();
		private List<Task> _ReceivingTasks = new List<Task>();
		private Dictionary<int, TaskCompletionSource<byte[]>> _PendingRequests = new Dictionary<int, TaskCompletionSource<byte[]>>();
		private Predicate<ReceiveState> _onReceiveRequest;
		private Predicate<ReceiveState> _onReceiveBroadcast;

		public Node(int port, Predicate<ReceiveState> onReceiveRequest, Predicate<ReceiveState> onReceiveBroadcast) {
			Port = port;
			_onReceiveRequest = onReceiveRequest;
			_onReceiveBroadcast = onReceiveBroadcast;
		}
		
		public async Task ListenAsync() {
			
			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());	
			IPAddress ipAddress = ipHostInfo.AddressList[0];
			IPEndPoint localEndPoint = new IPEndPoint(ipAddress, Port);
			
			Listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			Listener.Bind(localEndPoint);		
			Listener.Listen(100);					
		
			await Task.Run(() => {
				while (true) {

					// TODO: Add pause token so that the user can pause this task
					// https://devblogs.microsoft.com/pfxteam/cooperatively-pausing-async-methods/					

					Socket handler = Listener.Accept();
					
					Peer peer = new Peer(handler);	
					_ConnectedPeers.Add(peer);
					
					ReceiveFromPeer(peer);
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
			ReceiveFromPeer(peer);
			return peer;
		}

		private async Task SendAsync(MessageHeader header, byte[] msg) {
			List<Task> sendingTasks = new List<Task>();

			var headerBytes = MessageHeader.GetBytes(header);
			
			_ConnectedPeers.ForEach((Peer peer) => {
				// Send header and then msg content			
				Task sendingTask = peer.SendAsync(headerBytes);
				sendingTask.ContinueWith(task => peer.SendAsync(msg),
							TaskContinuationOptions.OnlyOnRanToCompletion);
				
				sendingTasks.Add(sendingTask);
			});
	
			await Task.WhenAll(sendingTasks);
		}
	
		public async Task BroadcastAsync(byte[] msg) {
			MessageHeader header = new MessageHeader(new Random().Next(), MessageType.Broadcast, msg.Length);
			await SendAsync(header, msg);
		}

		public async Task<byte[]> RequestAsync(byte[] msg) {
			// Sends a request and returns a response
			CancellationTokenSource source = new CancellationTokenSource();
			CancellationToken token = source.Token;

			MessageHeader header = new MessageHeader(new Random().Next(), MessageType.Request, msg.Length);
			var headerBytes = MessageHeader.GetBytes(header);

			TaskCompletionSource<byte[]> requestCompleteSource = new TaskCompletionSource<byte[]>();
			_PendingRequests.Add(header.Id, requestCompleteSource);

			_ConnectedPeers.ForEach ((peer) => {
				// TODO: this task should time out
				Task requestTask = peer.SendAsync(headerBytes);
				requestTask.ContinueWith(task => peer.SendAsync(msg),
							TaskContinuationOptions.OnlyOnRanToCompletion);
			});
			
			byte[] response =  await requestCompleteSource.Task;

			return response;	

		}

		private void ReceiveFromPeer(Peer peer) {
			// TODO: Add pause token so that the user can pause this task
			// https://devblogs.microsoft.com/pfxteam/cooperatively-pausing-async-methods/					

			Task<byte[]> readTask =  peer.ReceiveAsync(Marshal.SizeOf(typeof(MessageHeader)));

			Task successTask = readTask.ContinueWith(ReadPacket, peer, TaskContinuationOptions.OnlyOnRanToCompletion);
			successTask.ContinueWith((task) =>  {
				ReceiveFromPeer(peer);
			});	

			readTask.ContinueWith(ReadError, peer, TaskContinuationOptions.OnlyOnFaulted);
		}

		private async void ReadPacket(Task<byte[]> readTask, object state) {
			Peer peer = (Peer) state;
			
			MessageHeader header = MessageHeader.FromBytes(readTask.Result);
			var msg = peer.ReceiveAsync(header.MsgLen).Result;

			if (header.Type == MessageType.Response) {							
					
				var headerSize = Marshal.SizeOf(typeof(ResponseHeader));
				var headerBytes = new byte[headerSize];
				var response = new byte[msg.Length - headerSize];
				Array.Copy(msg, headerBytes, headerSize);
				Array.Copy(msg, headerSize, response, 0, msg.Length - headerSize);

				ResponseHeader responseHeader = ResponseHeader.FromBytes(headerBytes);

				TaskCompletionSource<byte[]> completionSource = null;
				_PendingRequests.TryGetValue(responseHeader.RefId, out completionSource);

				if (completionSource != null) {
					completionSource.SetResult(response);
				}

				return;
			}

			// Check that message has not already been received
			bool alreadyReceived = false;
			if (!alreadyReceived) { 
				
				bool fwd = await Task.Run<bool>(() => {
					
					var receiveState = new ReceiveState(msg, peer, header.Id);
					
					if (header.Type == MessageType.Request) {
						return _onReceiveRequest(receiveState);
					} else if (header.Type == MessageType.Broadcast) {
						return _onReceiveBroadcast(receiveState);
					}

					return false;

				});

				// Forward to other peers
				if (fwd) {
					Console.WriteLine("Forwarding...");
					await SendAsync(header, msg);
				}
			}

		}

		private void ReadError(Task failedTask, object peerState) {
			Console.WriteLine($"Error Reading Packet {((Peer)peerState).LastException.GetType().Name}");
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
					Console.WriteLine("ErrorB");
				}
			});

		}

		public async Task<byte[]> ReceiveAsync(int minBytes) {

			byte[] content = null;			
			await Task.Run(() => {

				byte[] buffer = new byte[minBytes];
				try {
					
					int received = 0;
					while (received != minBytes) {
						received += Connection.Receive(buffer, received, minBytes - received, SocketFlags.None);
					}

					_received += received;
					content = buffer;

				} catch (Exception e) {
					_hasErrored = true;
					_lastException = e;
					Console.WriteLine("ErrorA");
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
