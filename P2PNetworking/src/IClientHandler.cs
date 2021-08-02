using System;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;

namespace P2PNetworking {

	public interface IClientHandler {
		bool ConnectionAccepted { get; set; }
		void SendMessage(MessageHeader header, byte[] content);
		void SendRequest(MessageHeader header, byte[] content, OnReceivedResponse onResponse);
		void SetOnMessageRecieve(MessageReceiveHandler handler);
		void SetOnPeerDisconected(PeerDisconectHandler handler);
		void Run();
	}

	public class TestHandler : IClientHandler {
		public bool ConnectionAccepted { get; set; }
		
		public void SendMessage(MessageHeader header, byte[] content) {
			Console.WriteLine("---- Sending Message ----");
			Console.WriteLine($"type:{header.ContentType} fwd:{header.Forward} len:{content.Length} ver:{header.ProtocolVersion} ref:{header.ReferenceId}");
			Console.WriteLine("-------------------------");
		}

		public void SendRequest(MessageHeader header, byte[] content, OnReceivedResponse onResponse) {
			Console.WriteLine("---- Sending Request ----");
			Console.WriteLine($"type:{header.ContentType} fwd:{header.Forward} len:{content.Length} ver:{header.ProtocolVersion} ref:{header.ReferenceId}");
			Console.WriteLine("-------------------------");
		}

		public void SetOnMessageRecieve(MessageReceiveHandler handler) {

		}

		public void SetOnPeerDisconected(PeerDisconectHandler handler) {

		}

		public void Run() {

		}

	}

}
