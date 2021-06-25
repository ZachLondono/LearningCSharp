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

		public static ArrayContent<T> GetContent(byte[] encoded) {
			
			byte count = encoded[0];
			int objSize = Marshal.SizeOf(typeof(T));

			T[] objArray = new T[count]; 

			for (int i = 0; i < count; i++) {
				
				int offset = (i * objSize) + 1;

				T obj;

				IntPtr ptr = Marshal.AllocHGlobal(objSize);
				Marshal.Copy(encoded, offset, ptr, objSize);
				obj = (T) Marshal.PtrToStructure(ptr, typeof(T));
				Marshal.FreeHGlobal(ptr);

				objArray[i] = obj;

			}

			ArrayContent<T> objs = new ArrayContent<T>();

			return objs;

		}

		public byte[] GetBytes() {
			
			int objSize = Marshal.SizeOf(typeof(T));
			
			// Enough bytes for the objects, plus 1 for the object count
			byte[] encoded = new byte[objSize * Count + 1];

			encoded[0] = Count;

			for (int i = 0; i < Count; i++) {
				
				int offset = (objSize * i)+ 1;

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