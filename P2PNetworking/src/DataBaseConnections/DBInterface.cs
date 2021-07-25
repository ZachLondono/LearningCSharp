using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace P2PNetworking {

	public struct DataPair {
		public byte[] Key { get; }
		public byte[] Value { get; }
		
		public DataPair(byte[] key, byte[] value) {
			Key = key;
			Value = value;
		}
	}

	public struct Peer {
		public string Host { get; }
		public int Port { get; }

		public Peer(string host, int port) {
			Host = host;
			Port = port;
		}

	}

	public interface IDBInterface {

		public bool ContainsKey(byte[] key);
		public bool InsertPair(DataPair pair);
		public byte[] SelectData(string dataCol, string conditionCol, byte[] conditionVal);
		public bool RemoveKey(byte[] key);

		public List<Peer> GetPeers(); 
		public bool InsertPeer(Peer newPeer); 
		public bool RemovePeer(Peer peer);
		public bool ContainsPeer(Peer peer);
		// public void BlackListPeer(Peer badPeer) { } 

	}

}
