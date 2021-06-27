using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace P2PNetworking {

	public enum MessageType : byte {
		ConnectionCheck = 0,
		SuccessfulConnection = 1,
		PeerRequest = 2, 	// Request for all known peers that can be connected to
		RecordResponse = 3,	// Response containing a record
		InvalidRequest = 4,	// Response to an invalid request
		UnsuportedProtocolVersion = 5,	// Response to a request with an unsuported version
		MessageTimeout = 6,	// Response when the last sent message has timed out before the entire message was recieved
		ConnectionTimeout = 7,	// Response when a connection has been timed out, and then disconnected
	}

	public struct MessageHeader {
		public byte ProtocolVersion;
		public MessageType ContentType;
		public int ContentLength;
		public bool forward; 

		public static int Size {
			get => Marshal.SizeOf(typeof(MessageHeader));
		}

		/// Returns the bytes which represent the given header struct
		public static byte[] GetBytes(MessageHeader header) {
				
			int size = Marshal.SizeOf(header);
			byte[] encoded = new byte[size];

			IntPtr ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(header, ptr, true);
			Marshal.Copy(ptr, encoded, 0, size);
			Marshal.FreeHGlobal(ptr);


			return encoded;

		}

		/// Constructs a message header from the given bytes
		public static MessageHeader FromBytes(byte[] data) {

			MessageHeader obj;
			int size = Marshal.SizeOf(typeof(MessageHeader));
			IntPtr ptr = Marshal.AllocHGlobal(size);
			Marshal.Copy(data, 0, ptr, size);
			obj = (MessageHeader) Marshal.PtrToStructure(ptr, typeof(MessageHeader));
			Marshal.FreeHGlobal(ptr);

			return obj;

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