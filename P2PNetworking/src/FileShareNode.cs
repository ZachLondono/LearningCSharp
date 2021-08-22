using System;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace P2PNetworking {

	public struct Chunk {
		public byte[] Data;
		public byte[] Hash;
		public int Index;
	}

	public class File {
		private Dictionary<int,Chunk> Chunks { get; } 
		private File(Dictionary<int, Chunk> chunks) {
			Chunks = chunks;
		}

		public ICollection<Chunk> GetChunks() {
			return Chunks.Values;
		}

		public static File MakeFileFromData(byte[] data, HashAlgorithm algorithm) {
			Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();		
			byte[][] dataSections = FileShareNode.GetChunks(data);
			var range = Enumerable.Range(0, dataSections.Length);	

			// TODO this should be paralelized
			for (int i = 0; i < dataSections.Length; i++) {
				Chunk chunk = new Chunk();
				chunk.Data = dataSections[i];
				chunk.Hash = algorithm.ComputeHash(dataSections[i]);
				chunk.Index = i;
				chunks.Add(i, chunk);
			}

			File file = new File(chunks);

			return file;
		}

	}

	public class FileShareNode : IDisposable {
		public const int PROTOCOL_VERSION = 1;
		public const int CHUNK_SIZE = 2;
	
		private Node node;
		private IDBInterface _dbInterface;
		private Dictionary<byte[], List<Guid>> _knownFileLocations;
		private HashAlgorithm _digest;

		public FileShareNode(int port, HashAlgorithm algorithm, IDBInterface dbInterface) {
			node = new Node(port, onReceiveRequest, onReceiveBroadcast);
			_dbInterface = dbInterface;
			_knownFileLocations = new Dictionary<byte[], List<Guid>>();
			_digest = algorithm;

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
					byte[] hash = _digest.ComputeHash(broadcast.msg);
					DataPair data = new DataPair(hash, broadcast.msg);

					bool inserted = await _dbInterface.InsertPair(data);

					if (inserted) {
						byte[] suc = MessageToBytes(MessageType.RESOURCE_CREATED, hash);
						await node.BroadcastAsync(suc);
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

		public async Task<byte[]> GetChunkOnNetwork(byte[] key) {
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
				byte[] responseData = await node.RequestAsync(request, 1000);
				(MessageHeader header, byte[] responseData) response = GetMessage(responseData);
				return response.responseData;
			} catch (TimeoutException) {
				return null;
			}
		}

		public async Task StoreFileOnNetwork(File file) {
			List<Task> broadcastTasks = new List<Task>();
			foreach (Chunk chunk in file.GetChunks()) {					
				byte[] creationMsg = MessageToBytes(MessageType.CREATE_RESOURCE, chunk.Data);
				broadcastTasks.Add(node.BroadcastAsync(creationMsg));
			}

			await Task.WhenAll(broadcastTasks);
		}
		
		
		public async Task StoreDataLocally(File file) {
			List<Task> insertTasks = new List<Task>();
			foreach (Chunk chunk in file.GetChunks()) {	
				insertTasks.Add(_dbInterface.InsertPair(new DataPair(chunk.Hash, chunk.Data)));
			}

			await Task.WhenAll(insertTasks);
		}

		public static byte[][] GetChunks(byte[] data) {
			
			int chunkCount = data.Length / CHUNK_SIZE  + 1;
			byte[][] chunks = new byte[chunkCount][];
		
			for (int i = 0; i < chunkCount; i++) {
				int startIdx = i * CHUNK_SIZE;
				int endIdx = (i+1)*CHUNK_SIZE;
				endIdx = endIdx > data.Length ? data.Length : endIdx ;

				chunks[i] = new byte[endIdx - startIdx];

				Array.Copy(data, startIdx, chunks[i], 0, endIdx - startIdx);
			}
			
			return chunks;
		}

		public static byte[] MessageToBytes(MessageType type, byte[] content) {
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

		public static (MessageHeader header, byte[] msg) GetMessage(byte[] data) {
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