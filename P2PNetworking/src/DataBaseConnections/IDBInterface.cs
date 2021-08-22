using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace P2PNetworking {

	public struct DataPair {
		public byte[] Key { get; }
		public byte[] Value { get; }
		public DataPair(byte[] key, byte[] value) {
			Key = key;
			Value = value;
		}
	}

	public struct PeerInfo {
		public string Host { get; }
		public int Port { get; }

		public PeerInfo(string host, int port) {
			Host = host;
			Port = port;
		}

	}

	public interface IDBInterface {
		void Close();
		Task<bool> ContainsKey(byte[] key);
		Task<bool> InsertPair(DataPair pair);
		Task<bool> UpdatePair(DataPair pair);
		Task<byte[]> SelectData(string dataCol, string conditionCol, byte[] conditionVal);
		Task<bool> RemoveKey(byte[] key);

		Task<List<PeerInfo>> GetPeers(); 
		Task<bool> InsertPeer(PeerInfo newPeer); 
		Task<bool> RemovePeer(PeerInfo peer);
		Task<bool> ContainsPeer(PeerInfo peer);
		// public void BlackListPeer(Peer badPeer) { } 

	}

}
