using System;
using System.Net;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace P2PNetworking {

	public class FileShareNode : IDisposable {
		public const int PROTOCOL_VERSION = 1;
		private Node node;
		private IDBInterface _dbInterface;
		private Dictionary<byte[], List<Guid>> _knownFileLocations;
		public FileShareNode(int port, IDBInterface dbInterface) {
			node = new Node(port, onReceiveRequest, onReceiveBroadcast);
			_dbInterface = dbInterface;
			_knownFileLocations = new Dictionary<byte[], List<Guid>>();

			var listenTask = node.ListenAsync();
		}

		private async Task<bool> onReceiveRequest(Node.ReceiveState state) {

			byte[] requestData = state.Content;
			(MessageHeader header, byte[] msg) request = GetMessage(requestData);

			if (request.header.MessageType == MessageType.REQUEST_RESOURCE) {

				byte[] data = _dbInterface.SelectData("value", "key", request.msg).Result;

				if (data == null) {
					byte[] response = MessageToBytes(MessageType.RESOURCE_NOT_FOUND, new byte[]{});
					await state.Respond(response);
				} else {
					byte[] response = MessageToBytes(MessageType.REQUEST_SUCCESSFUL, data);
					await state.Respond(response);
				}

			}

			return false;
		}

		private async Task<bool> onReceiveBroadcast(Node.ReceiveState state) {

			byte[] broadcastData = state.Content;
			(MessageHeader header, byte[] msg) broadcast = GetMessage(broadcastData);

			switch (broadcast.header.MessageType) {

				case MessageType.CREATE_RESOURCE:
					using (SHA256 sha = SHA256.Create()) {
						byte[] hash = sha.ComputeHash(broadcast.msg);
						DataPair data = new DataPair(hash, broadcast.msg);
						bool inserted = await _dbInterface.InsertPair(data);

						if (inserted) {
							byte[] suc = MessageToBytes(MessageType.RESOURCE_CREATED, hash);
							await node.BroadcastAsync(suc);
						}
					}
					break;

				case MessageType.RESOURCE_CREATED:
					if (!_knownFileLocations.ContainsKey(broadcast.msg)) 
						_knownFileLocations.Add(broadcast.msg, new List<Guid>());
					if(!_knownFileLocations[broadcast.msg].Contains(state.SenderId)) // make sure there are no duplicates
						_knownFileLocations[broadcast.msg].Add(state.SenderId);
					break;

				case MessageType.RESOURCE_DELETED:
					if (_knownFileLocations.ContainsKey(broadcast.msg)) 
						_knownFileLocations[broadcast.msg].Remove(state.SenderId);
					break;

			}
			

			return false;
		}

		public async Task ConnectAsync(IPEndPoint remoteEP) {
			await node.ConnectAsync(remoteEP);
		}

		public async Task<byte[]> GetFileOnNetwork(byte[] key) {
			byte[] request = MessageToBytes(MessageType.REQUEST_RESOURCE, key);

			// Check if we know a specific peer has it
			if (_knownFileLocations.ContainsKey(key)) {

				List<Guid> peerIds = _knownFileLocations[key];
				foreach (Guid peer_id in peerIds) {
					try {						
						byte[] response = await node.RequestFromPeer(peer_id, request, 1000);
						return response;
					} catch (TimeoutException) {
						continue;
					}
				}

			}

			// If we do not, or they no longer have it, ask all the peers we do know (this will reask the same peers from above, should probably do something about that... maybe)
			try {
				byte[] responseData = await node.RequestAsync(request, 3000);
				(MessageHeader header, byte[] responseData) response = GetMessage(responseData);
				return response.responseData;
			} catch (TimeoutException) {
				return null;
			}
		}

		public async Task StoreFileOnNetwork(byte[] data) {
			byte[] creationMsg = MessageToBytes(MessageType.CREATE_RESOURCE, data);		
			await node.BroadcastAsync(creationMsg);
		}

		private byte[] MessageToBytes(MessageType type, byte[] content) {
			MessageHeader header = new MessageHeader();
			header.MessageType = type;
			header.ProtocolVersion = FileShareNode.PROTOCOL_VERSION;
			header.ContentLength = content.Length;

			byte[] headerBytes = Node.GetBytes<MessageHeader>(header);

			byte[] fullMessage = new byte[content.Length + headerBytes.Length];
			
			Array.Copy(headerBytes, fullMessage, headerBytes.Length);
			Array.Copy(content, 0, fullMessage, headerBytes.Length, content.Length);

			return fullMessage;
		}

		private (MessageHeader header, byte[] msg) GetMessage(byte[] data) {
			MessageHeader header;
			byte[] headerBytes = new byte[MessageHeader.Size];
			byte[] msg = new byte[data.Length - headerBytes.Length];

			Array.Copy(data, headerBytes, headerBytes.Length);
			Array.Copy(data, headerBytes.Length, msg, 0, msg.Length);

			Node.FromBytes<MessageHeader>(headerBytes, out header);

			return  (header, msg);
		}

		public void Dispose() {
			_dbInterface.Close();
		}

	}

}