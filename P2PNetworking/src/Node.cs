using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace P2PNetworking {
 
	class Node {
	
		public class ReceiveState {
			public byte[] Content { get; } 
			public Guid SenderId { get => peer.Id; }
			private Peer peer;
			private Guid requestId;
			public ReceiveState(byte[] received, Peer receivedFrom, Guid requestId) {
				Content = received;
				peer = receivedFrom;
				this.requestId = requestId;
			}
			
			public async Task Respond (byte[] response) {
				ResponseHeader responseHeader = new ResponseHeader(requestId, response.Length);
				MessageHeader messageHeader = new MessageHeader(MessageType.Response, Marshal.SizeOf(responseHeader) + response.Length);

				var headerBytes = GetBytes<MessageHeader>(messageHeader);
				var responseHeaderBytes = GetBytes<ResponseHeader>(responseHeader);

				byte[] fullMsg = new byte[headerBytes.Length + responseHeaderBytes.Length + response.Length];
				Array.Copy(headerBytes, fullMsg, headerBytes.Length);
				Array.Copy(responseHeaderBytes, 0, fullMsg, headerBytes.Length, responseHeaderBytes.Length);
				Array.Copy(response, 0, fullMsg, headerBytes.Length + responseHeaderBytes.Length, response.Length);
		
				await peer.SendAsync(fullMsg);
			}

		}
		
		public enum MessageType : byte{
			Broadcast = 2,
			Request = 1,
			Response = 0
		}
		
		public struct MessageHeader {
			public Guid Id { get; }
			public MessageType Type { get; }
			public int MsgLen { get; }
			
			public MessageHeader(MessageType type, int len) {
				Type = type;
				MsgLen = len;
				Id = Guid.NewGuid();
			}
		}

		public struct ResponseHeader {
			public Guid RefId { get; }
			public int Length { get; }
			public ResponseHeader(Guid refId, int length) {
				RefId = refId;
				Length = length;
			}
		}
	
		public int Port { get; }
		private Socket Listener;
		protected List<Peer> _ConnectedPeers = new List<Peer>();
		private List<Task> _ReceivingTasks = new List<Task>();
		private Dictionary<Guid, TaskCompletionSource<byte[]>> _PendingRequests = new Dictionary<Guid, TaskCompletionSource<byte[]>>();
		private HashSet<Guid> _processedMessages;
		private Func<ReceiveState,Task> _onReceiveRequest;
		private Func<ReceiveState,Task<bool>> _onReceiveBroadcast;

		public Node(int port, Func<ReceiveState, Task> onReceiveRequest, Func<ReceiveState,Task<bool>> onReceiveBroadcast) {
			Port = port;
			_onReceiveRequest = onReceiveRequest;
			_onReceiveBroadcast = onReceiveBroadcast;
			_processedMessages = new HashSet<Guid>();
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

			var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
			var ipAddress = ipHostInfo.AddressList[0];
			Socket peerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);			

			Peer peer = new Peer(peerSocket);
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
				byte[] fullMsg = new byte[headerBytes.Length + msg.Length];
				Array.Copy(headerBytes, fullMsg, headerBytes.Length);
				Array.Copy(msg, 0, fullMsg, headerBytes.Length, msg.Length);

				Task sendingTask = peer.SendAsync(fullMsg);
				
				sendingTasks.Add(sendingTask);
			});
	
			await Task.WhenAll(sendingTasks);
		}
	
		public async Task BroadcastAsync(byte[] msg) {
			MessageHeader header = new MessageHeader(MessageType.Broadcast, msg.Length);
			_processedMessages.Add(header.Id);
			await SendAsync(header, msg);
		}

		public async Task<byte[]> RequestAsync(byte[] msg, int timeout) {
			
			MessageHeader header = new MessageHeader(MessageType.Request, msg.Length);
			var headerBytes = GetBytes<MessageHeader>(header);
			
			TaskCompletionSource<byte[]> requestCompleteSource = new TaskCompletionSource<byte[]>();
			_PendingRequests.Add(header.Id, requestCompleteSource);

			byte[] response = null; 

			foreach (Peer peer in _ConnectedPeers) {

				byte[] fullMsg = new byte[headerBytes.Length + msg.Length];
				Array.Copy(headerBytes, fullMsg, headerBytes.Length);
				Array.Copy(msg, 0, fullMsg, headerBytes.Length, msg.Length);
				
				await peer.SendAsync(fullMsg);

				if (await Task.WhenAny(requestCompleteSource.Task, Task.Delay(timeout)) == requestCompleteSource.Task) {						
					response = requestCompleteSource.Task.Result;
				} // else try the next peer
			}

			if (response == null) {
				// All nodes timed out, remove the message from pending requests
				_PendingRequests.Remove(header.Id);
				throw new TimeoutException("No response received");
			}

			return response;
		}

		public async Task<byte[]> RequestFromPeer(Guid peer_id, byte[] msg, int timeout) {
			// Should probably switch to a hash map rather than list
			Peer peer = _ConnectedPeers.Find((peer) => peer.Id.Equals(peer_id));
			if (peer == null) throw new ArgumentException("No such peer");

			MessageHeader header = new MessageHeader(MessageType.Request, msg.Length);
			var headerBytes = GetBytes<MessageHeader>(header);
			
			TaskCompletionSource<byte[]> requestCompleteSource = new TaskCompletionSource<byte[]>();
			_PendingRequests.Add(header.Id, requestCompleteSource);

			byte[] fullMsg = new byte[headerBytes.Length + msg.Length];
			Array.Copy(headerBytes, fullMsg, headerBytes.Length);
			Array.Copy(msg, 0, fullMsg, headerBytes.Length, msg.Length);

			await peer.SendAsync(fullMsg);
			
			byte[] response = null; 
			if (await Task.WhenAny(requestCompleteSource.Task, Task.Delay(timeout)) == requestCompleteSource.Task) {						
				response = requestCompleteSource.Task.Result;
			} else throw new TimeoutException("No response received");

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

			// If this message has already been seen, it can be ignored
			// The node still needs to read the message's content otherwise those bytes will be misinterpreted as a header
			if (_processedMessages.Contains(header.Id)) return;
			// If the message has not been seen, add it to the list of seen messages 
			else _processedMessages.Add(header.Id);

			if (header.Type == MessageType.Response) {	
					
				// If the message is a response type, the following bytes are a response header and then the response
				var headerSize = Marshal.SizeOf(typeof(ResponseHeader));
				var headerBytes = new byte[headerSize];
				var response = new byte[msg.Length - headerSize];
				Array.Copy(msg, headerBytes, headerSize);
				Array.Copy(msg, headerSize, response, 0, msg.Length - headerSize);

				ResponseHeader responseHeader;
				FromBytes<ResponseHeader>(headerBytes, out responseHeader);

				// Check if the response is referencing a previous request which is pending
				TaskCompletionSource<byte[]> completionSource = null;
				_PendingRequests.TryGetValue(responseHeader.RefId, out completionSource);

				if (completionSource != null) {
					// If there is a pending request, set its value to the bytes received
					_PendingRequests.Remove(responseHeader.RefId);
					completionSource.SetResult(response);
				}

				return;
			}

			var receiveState = new ReceiveState(msg, peer, header.Id);

			if (header.Type == MessageType.Request) {
				// TODO: create a proxy request to allow a node to request data from another node which it is not directly connected
				await _onReceiveRequest(receiveState);
			} else if (header.Type == MessageType.Broadcast) {
				bool fwd = await _onReceiveBroadcast(receiveState);
				// Only broadcasts should ever be forwarded 
				// A response will be ignored by any node which did not request it and a request which goes through a proxy node will not have a way to return the response
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
