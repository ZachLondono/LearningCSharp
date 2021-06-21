using System.Runtime.InteropServices;

namespace P2PNetworking {

	public enum MessageType : byte {
		ProtocolCheck = 0,
		KnwonPeers = 1
	}

	public struct MessageHeader {
		public byte ProtocolVersion;
		public MessageType ContentType;
		public int ContentLength;

		/// Returns the bytes which represent the given header struct
		public byte[] GetBytes(MessageHeader header) {
			return new byte[0];
		}

		/// Constructs a message header from the given bytes
		public static MessageHeader FromBytes(byte[] data) {
			return new MessageHeader();
		}
	}

	public struct KnownPeers {
		public byte Count;
		public Peer[] peers;
	}

	public struct Peer {
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
		public string host;
		public ushort port;
	}

}