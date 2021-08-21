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

			System.Buffer.BlockCopy(keyDigest, 0, encoded, 0, keyDigest.Length);
			System.Buffer.BlockCopy(Value, 0, encoded, keyDigest.Length, Value.Length);
			
			return encoded;

		}

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

		public bool ContainsKey(byte[] key);
		public bool InsertPair(DataPair pair);
		public bool UpdatePair(DataPair pair);
		public byte[] SelectData(string dataCol, string conditionCol, byte[] conditionVal);
		public bool RemoveKey(byte[] key);

		public List<PeerInfo> GetPeers(); 
		public bool InsertPeer(PeerInfo newPeer); 
		public bool RemovePeer(PeerInfo peer);
		public bool ContainsPeer(PeerInfo peer);
		// public void BlackListPeer(Peer badPeer) { } 

	}

}
