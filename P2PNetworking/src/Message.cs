using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace P2PNetworking {

	public enum MessageType : byte {
		// Request Codes 
		CONNECT = 1,
		REQUEST_PEERS = 2,
		REQUEST_RESOURCE = 3,
		CREATE_RESOURCE = 4,
		UPDATE_RESOURCE = 5,
		DELETE_RESOURCE = 6,
		
		// Response Codes
		REQUEST_SUCCESSFUL = 128,
		INVALID_REQUEST = 129,
		RESOURCE_NOT_FOUND = 130,
		RESOURCE_CREATED = 134,
		RESOURCE_ALREADY_EXISTS = 132,
		RESOURCE_UPDATED = 133,
		RESOURCE_DELETED = 134,
		VERSION_UNSUPPORTED = 135,
		NOT_IMPLEMENTED = 136,
		REQUEST_TIMEOUT = 137
	}

	public struct MessageHeader {
		public byte ProtocolVersion;
		public MessageType MessageType;
		public int ContentLength;

		public static int Size {
			get => Marshal.SizeOf(typeof(MessageHeader));
		}
	}

}
