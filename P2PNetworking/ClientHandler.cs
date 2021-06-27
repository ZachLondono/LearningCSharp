using System;
using System.Threading;
using System.Net.Sockets;

namespace P2PNetworking {

	class ClientHandler {
	    public Socket Socket { get; }
	    public DateTime StartTime { get; }
	
	    private bool HasRecievedMessage;
		private bool IsAlive;

	    public ClientHandler(Socket socket) {
	        Socket = socket;
	        StartTime = DateTime.Now;
	        HasRecievedMessage = false;
			IsAlive = true;
	    }
		
	    public void Run() {

	        System.Timers.Timer timer = new System.Timers.Timer();
	        timer.Interval = 1000 * 60; // 1 Minute timeout
	        timer.Elapsed += OnTimerEvent;
	        timer.AutoReset = false;
	        timer.Enabled = true;

	        // If no message is recieved in a reasonable time, disconect from client
	
	        // A client will first try to read in the necessary bytes for a MessageHeader
	        // If a reasonable amount of time has elapsed, and an insufficient amount of bytes has
	        // been read, then the client will stop trying to read more bytes and discard the currently 
	        // read bytes.

	        // If a message is recieved, this client will process the message and respond with either
	        // a success or failure message 

	        while (true) {

	            int headerSize = MessageHeader.Size;

				ReadState state = RecieveData(headerSize);

				// If connection died while recieving data, exit function
				if (!IsAlive) return;

				Console.WriteLine("--- Message Header ---");
				foreach (byte b in state.Buffer) {
					Console.Write(Convert.ToString(b, 2).PadLeft(8,'0'));
				}
				Console.WriteLine("\n----------------------");

	            MessageHeader header = MessageHeader.FromBytes(state.Buffer);
				HasRecievedMessage = true;

	            if (header.ProtocolVersion < Node.MinimumSupportedVersion) {
	                // Unsupported version
					SendMessage(MessageType.UnsuportedProtocolVersion, null);
					continue;
	            }

				byte[] content = null;

				// Once header is recieved, read the content
				if (header.ContentLength > 0) {
					
					ReadState contentState = RecieveData(header.ContentLength);

					if (!IsAlive) return;

					content = contentState.Buffer;	

				}

				if (content != null) Console.WriteLine($"Message Recieved: v{header.ProtocolVersion}\nType: {header.ContentType}\nSize: {header.ContentLength}\nContent: {BitConverter.ToString(content).Replace("-","")}");
				else Console.WriteLine($"Message Recieved: v{header.ProtocolVersion}\nType: {header.ContentType}\nSize: {header.ContentLength}\nContent: {{None}}");

				if (header.ContentType == MessageType.ConnectionCheck) {						
					
					SendMessage(MessageType.SuccessfulConnection, null);

				} else {
					// TODO pass the content to the appropiate handler function
				}
				
	        }

	    }

		private ReadState RecieveData(int size) {
	
			ReadState state = new ReadState(size);
			// Start to recieve data from client
			//Socket.BeginReceive(state.Buffer, 0, size, 
			//		SocketFlags.None, new AsyncCallback(DataRecieved), state);
			BeginReceive(state);
   
            while (!state.IsDone) {
				if (!IsAlive) return state;
				// As data is read, check that message has not timed out
                if (state.ElapsedTime > (1000 * 30)) {
                    // too mutch Time has passed, send timeout to message 
					SendMessage(MessageType.MessageTimeout, null);
					
					// Reset state and begin waiting for next message
					state.ResetState();
					Socket.BeginReceive(state.Buffer, 0, size, 
						SocketFlags.None, new AsyncCallback(DataRecieved), state);
                }
            }

			return state;

		}
		public void SendMessage(MessageType type, byte[] content) {

			MessageHeader header = new MessageHeader();
			header.ProtocolVersion = Convert.ToByte(Node.Version);
			header.ContentType = type;
			header.ContentLength = content == null ? 0 : content.Length;

			// TODO make sending async
			Socket.Send(MessageHeader.GetBytes(header));
			if (header.ContentLength != 0) Socket.Send(content);

		}

		/// Start recieving data with a given ReadState
		private void BeginReceive(ReadState state) {

			try {
				Socket.BeginReceive(state.Buffer, state.TotalRecieved, 
					state.ExpectedSize - state.TotalRecieved,
					SocketFlags.None, new AsyncCallback(DataRecieved), state);
			} catch (SocketException e) {					
				Console.WriteLine("Exception occurred while reading from peer:\n{0}", e.ToString());
				IsAlive = false;
			}

		}
	
		/// Callback function called when data is recieved from async Recieve
		private void DataRecieved(IAsyncResult ar) {
			
			// Get state
			ReadState state = (ReadState) ar.AsyncState;
			int recieved = 0;
			try {
				recieved = Socket.EndReceive(ar);
			} catch (SocketException e) {					
				Console.WriteLine("Exception occurred while reading from peer:\n{0}", e.ToString());
				IsAlive = false;
				return;
			} 

			state.TotalRecieved = state.TotalRecieved += recieved;

			// Check how much data has been read, if it is less then the exptected amount of data, continue to read more data
			if (state.TotalRecieved < state.ExpectedSize) {					
				BeginReceive(state);
			}

		}

        private void OnTimerEvent(Object source, System.Timers.ElapsedEventArgs e) {
			if (HasRecievedMessage) return;
			// Too much time has elapsed, send timeout to client
			SendMessage(MessageType.ConnectionTimeout, null);
			// After sending timeout response to client, wait up to 10 seconds before terminating the connection to allow the client to recieve the message
			IsAlive = false;
			Socket.Close(10);
			// TODO signal main thread to get rid of this connection and thread
        }


		class ReadState {
			private DateTime startTime;
			
			public int TotalRecieved { get; set; }
			public int ExpectedSize { get; }
			public bool IsDone { 
				get => TotalRecieved == ExpectedSize;
			}
			public double ElapsedTime {
				get => (DateTime.Now - startTime).TotalMilliseconds;
			}
			public byte[] Buffer;


			public ReadState (int expectedSize) {
				ExpectedSize = expectedSize;
				Buffer = new byte[ExpectedSize];
				startTime = DateTime.Now;
			}

			public void ResetState() {
				startTime = DateTime.Now;
				Buffer = new byte[ExpectedSize];
				TotalRecieved = 0;
			}

		}

	}

}