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
		public Guid Id { get; }	

		public Peer(Socket connection) {
			Connection = connection;

			Id = new Guid();
			_isConnected = false;
			_sent = 0;	
			_received = 0;
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
				}

			});

			return content;

		}

	}
}
