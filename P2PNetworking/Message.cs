using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace P2PNetworking {

	public enum MessageType : byte {
		ProtocolCheck = 0,
		PeerRequest = 1,
		RecordResponse = 2,
		InvalidRequest = 3,
		UnsuportedProtocolVersion = 4
	}

	public struct MessageHeader {
		public byte ProtocolVersion;
		public MessageType ContentType;
		public int ContentLength;

		/// Returns the bytes which represent the given header struct
		public static byte[] GetBytes(MessageHeader header) {
			return new byte[0];
		}

		/// Constructs a message header from the given bytes
		public static MessageHeader FromBytes(byte[] data) {
			return new MessageHeader();
		}
	}

	public struct Peer {
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
		public string host;
		public ushort port;
	}

	public class ArrayContent<T> {
		private byte _count;
		private T[] _content; 
		public byte Count { get => _count; }
		public T[] Contents {
			get => _content;
			set {
				_content = value;
				_count = Convert.ToByte(value.Length);
			}
		}

		public byte[] GetBytes() {
			
			int objSize = Marshal.SizeOf(typeof(T));
			
			byte[] encoded = new byte[objSize * Count];

			for (int i = 0; i < Count; i++) {
				
				int offset = objSize * i;

				T obj = Contents[i];				

				IntPtr ptr = Marshal.AllocHGlobal(objSize);
				Marshal.StructureToPtr(obj, ptr, true);
				Marshal.Copy(ptr, encoded, offset, objSize);
				Marshal.FreeHGlobal(ptr);

			}

			return encoded;
			
		}

	}
}