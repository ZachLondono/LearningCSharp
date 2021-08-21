using System;
using System.Net;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace P2PNetworking {

	public class FileShareNode : IDisposable {
		public const int PROTOCOL_VERSION = 1;
		private Node node;
		private IDBInterface _dbInterface;
		public FileShareNode(int port, IDBInterface dbInterface) {
			node = new Node(port, onReceiveRequest, onReceiveBroadcast);
			_dbInterface = dbInterface;

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

			/* 
				Broadcast Examples:
				1. A node lets everyone else know that it is storing a certain file
				2. A node lets everyone else know that it no longer has a certain file
				3. A new file is transmitted on the network, nodes can decide individually if they want to store it
			*/

			byte[] broadcastData = state.Content;
			(MessageHeader header, byte[] msg) broadcast = GetMessage(broadcastData);

			if (broadcast.header.MessageType == MessageType.CREATE_RESOURCE) {
				using (SHA256 sha = SHA256.Create()) {
					byte[] hash = sha.ComputeHash(broadcast.msg);
					DataPair data = new DataPair(hash, broadcast.msg);
					await _dbInterface.InsertPair(data);
				}
			}

			return false;
		}

		public async Task ConnectAsync(IPEndPoint remoteEP) {
			await node.ConnectAsync(remoteEP);
		}

		public async Task<byte[]> GetFileOnNetwork(byte[] key) {
			byte[] request = MessageToBytes(MessageType.REQUEST_RESOURCE, key);
			byte[] responseData = await node.RequestAsync(request);

			(MessageHeader header, byte[] responseData) response = GetMessage(responseData);
			return response.responseData;
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