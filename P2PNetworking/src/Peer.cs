using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;


namespace P2PNetworking {
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
