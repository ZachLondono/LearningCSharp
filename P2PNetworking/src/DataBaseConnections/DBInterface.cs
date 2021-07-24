using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace P2PNetworking {

	public struct Data {
		byte[] Key;
		byte[] Value;
		
		public Data(byte[] key, byte[] value) {
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

		public void CreateTableIfNotExist(string name, string[] columns);
		public bool ContainsKey(byte[] key);
		public bool InsertPair(byte[] key, byte[] value);
		public byte[] SelectData(string dataCol, string conditionCol, byte[] conditionVal);
		public List<Peer> GetPeers(); 
		public bool InsertPeer(Peer newPeer); 
		public bool RemovePeer(Peer peer);
		// public void BlackListPeer(Peer badPeer) { } 

	}

}
