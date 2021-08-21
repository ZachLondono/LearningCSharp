using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace P2PNetworking {
 
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
				
				await peer.SendAsync(GetBytes<MessageHeader>(messageHeader));
				await peer.SendAsync(GetBytes<ResponseHeader>(responseHeader));
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
		}

		public struct ResponseHeader {
			public int RefId { get; }
			public int Length { get; }
			public ResponseHeader(int refId, int length) {
				RefId = refId;
				Length = length;
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

			var headerBytes = GetBytes<MessageHeader>(header);
			
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
			var headerBytes = GetBytes<MessageHeader>(header);

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
			
			MessageHeader header;
			FromBytes<MessageHeader>(readTask.Result, out header);

			var msg = peer.ReceiveAsync(header.MsgLen).Result;

			if (header.Type == MessageType.Response) {							
					
				var headerSize = Marshal.SizeOf(typeof(ResponseHeader));
				var headerBytes = new byte[headerSize];
				var response = new byte[msg.Length - headerSize];
				Array.Copy(msg, headerBytes, headerSize);
				Array.Copy(msg, headerSize, response, 0, msg.Length - headerSize);

				ResponseHeader responseHeader;
				FromBytes<ResponseHeader>(headerBytes, out responseHeader);

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

		public static byte[] GetBytes<T>(T str) {
			int size = Marshal.SizeOf(str);
			byte[] arr = new byte[size];

			IntPtr ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(str, ptr, true);
			Marshal.Copy(ptr, arr, 0, size);
			Marshal.FreeHGlobal(ptr);
			return arr;
		}

		public static void FromBytes<T>(byte[] bytes, out T output) {
			int size = Marshal.SizeOf(typeof(T));
			IntPtr ptr = Marshal.AllocHGlobal(size);
			Marshal.Copy(bytes, 0, ptr, size);
			output = (T) Marshal.PtrToStructure(ptr, typeof(T));
			Marshal.FreeHGlobal(ptr);
		}

	}

}
