using System.Collections.Generic;
using System.Security.Cryptography;

namespace P2PNetworking {

	public struct DataPair {
		public byte[] Key { get; }
		public byte[] Value { get; }

		public byte[] GetEncoded() {
			
			// | 256-bit Hashed Key | Value |

			byte[] encoded = new byte[256 + Value.Length];
			byte[] keyDigest;

			using (SHA256 sha256 = SHA256.Create()) {
				keyDigest = sha256.ComputeHash(Key);
			}

			System.Buffer.BlockCopy(encoded, 0, keyDigest, 0, keyDigest.Length);
			System.Buffer.BlockCopy(encoded, keyDigest.Length, Value, 0, encoded.Length - keyDigest.Length);
			
			return encoded;

		}

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
		public bool UpdatePair(DataPair pair);
		public byte[] SelectData(string dataCol, string conditionCol, byte[] conditionVal);
		public bool RemoveKey(byte[] key);

		public List<Peer> GetPeers(); 
		public bool InsertPeer(Peer newPeer); 
		public bool RemovePeer(Peer peer);
		public bool ContainsPeer(Peer peer);
		// public void BlackListPeer(Peer badPeer) { } 

	}

}
