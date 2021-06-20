using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;

namespace Networking {

	enum MsgType : byte {
		Request = 1,
		Response = 2
	};

	struct ProtocolMsg {

		public MsgType MessageType;
		public byte ProtocolVersion;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
		public string Message;
	};

	class Peer {
		static void Main(string[] args) {
			
			if (args.Length == 0 || args[0].Equals("client")) Client();
			else if (args[0].Equals("server")) Server();

		}

		static void Server() {

			int size = Marshal.SizeOf(new ProtocolMsg());
			byte[] bytes = new byte[size];

			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
			IPAddress iPAddress= ipHostInfo.AddressList[0];
			IPEndPoint localEndPoint = new IPEndPoint(iPAddress, 11000);

			Socket listener  = new Socket(iPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			try {

				listener.Bind(localEndPoint);
				listener.Listen(10);

				while (true) {

					Console.WriteLine("Waiting for a connection.... ");

					Socket handler = listener.Accept();

					int bytesRec = handler.Receive(bytes);

					//string binary = Convert.ToString(bytes, 2).PadLeft(size, '0');
					string binary = ""; 
					foreach (byte b in bytes) {
						binary += Convert.ToString(b, 2).PadLeft(8, '0');
					}

					Console.WriteLine("Recieved Bytes: {0}", binary);

					ProtocolMsg recieved = new ProtocolMsg();
					recieved = FromBytes(bytes);

					Console.WriteLine("Type {0}, Version {1}, Message: {2}", recieved.MessageType, recieved.ProtocolVersion, recieved.Message);

					recieved.MessageType = MsgType.Response;
					bytes = GetBytes(recieved);

					handler.Send(bytes);
					handler.Shutdown(SocketShutdown.Both);
					handler.Close();

				}

			} catch (Exception e) {
				Console.WriteLine("Failed to recieve message {0}", e.ToString());
			}

			Console.WriteLine("\nPress Enter to continue...");
			Console.Read();

		}
		static void Client() {

			try {

				IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());  
				IPAddress ipAddress = ipHostInfo.AddressList[0];  
				IPEndPoint remoteEP = new IPEndPoint(ipAddress,11000);  

				Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				
				try {

					client.Connect(remoteEP);

					Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());

					ProtocolMsg msg = new ProtocolMsg();
					msg.MessageType = MsgType.Request;
					msg.ProtocolVersion = 1;
					msg.Message = "";

					byte[] marshaledBytes = GetBytes(msg);

					int bytesSent = client.Send(marshaledBytes);

					ProtocolMsg response = new ProtocolMsg();
					byte[] bytes = new byte[Marshal.SizeOf(response)];
					int read = client.Receive(bytes);
					response = FromBytes(bytes);
					
					Console.WriteLine("Type: {0}, Version: {1}, Message: {2}", response.MessageType, response.ProtocolVersion, response.Message);

					client.Shutdown(SocketShutdown.Both);
					client.Close();

				} catch (Exception e) {
					Console.WriteLine("Failed to send message {0}", e.ToString());
				}

			} catch (Exception e) {
				Console.WriteLine("Failed to create Socket {0}", e.ToString());
			}

		}

		public static byte[] GetBytes(ProtocolMsg msg) {
			int size = Marshal.SizeOf(msg);
			byte[] arr = new byte[size];

			IntPtr ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(msg, ptr, true);
			Marshal.Copy(ptr, arr, 0, size);
			Marshal.FreeHGlobal(ptr);

			return arr;
		}

		public static ProtocolMsg FromBytes(byte[] arr) {

			ProtocolMsg msg = new ProtocolMsg();
			
			int size = Marshal.SizeOf(msg);
			IntPtr ptr = Marshal.AllocHGlobal(size);

			Marshal.Copy(arr, 0, ptr, size);

			msg = (ProtocolMsg) Marshal.PtrToStructure(ptr, msg.GetType());
			Marshal.FreeHGlobal(ptr);

			return msg;
		}

	}

}