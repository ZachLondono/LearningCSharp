using System;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;

namespace P2PNetworking {

	public interface IClientHandler {
		bool ConnectionAccepted { get; set; }
		void Disconnect();
		void SendMessage(MessageHeader header, byte[] content);
		void SendRequest(MessageHeader header, byte[] content, OnReceivedResponse onResponse);
		void SetOnMessageRecieve(MessageReceiveHandler handler);
		void SetOnPeerDisconected(PeerDisconectHandler handler);
		void Run();
	}

	public interface IClientHandlerFactory {
		public IClientHandler BuildClientHandler(Socket socket);
	}

}
