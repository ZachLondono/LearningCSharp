using System;
using P2PNetworking;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.Security.Cryptography;

namespace P2PTesting { 

	public class Testing {

		static async Task Main(string[] args) {
			await TestSuite(args);
		}

		static async Task NodeTest1() {
			
			TaskCompletionSource<byte[]> requestSource = new TaskCompletionSource<byte[]>();
			TaskCompletionSource<byte[]> broadcastASource = new TaskCompletionSource<byte[]>();
			TaskCompletionSource<byte[]> broadcastBSource = new TaskCompletionSource<byte[]>();

			var request = new byte[1]{222};
			var response = new byte[1]{111};
			var broadcast = new byte[1]{123};

			Node nodeB = new Node(11000, 
					async (state) => {
						requestSource.SetResult(state.Content);
						await state.Respond(response);
						return false;
					},async (state) => {
						broadcastBSource.SetResult(state.Content);
						return await Task<bool>.Run(()=>false);
					});

			Node nodeA = new Node(10101, 
					async (state) => {
						return await Task<bool>.Run(()=>false);
					},async (state) => {
						broadcastASource.SetResult(state.Content);
						return await Task<bool>.Run(() => false);
					});


			var listenTask = nodeB.ListenAsync();

			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());	
			IPAddress ipAddress = ipHostInfo.AddressList[0];
			IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);

			await nodeA.ConnectAsync(remoteEP);

			try {
				byte[] responseReceived = await nodeA.RequestAsync(request, 3000);
				var result = IsEqualArr(response, responseReceived) ? '✓' : 'x';
				Console.WriteLine($"ResponseA Received:	{result}");
			} catch (TimeoutException) {
				Console.WriteLine("RequestA Received:	timedout");
			}
			
			try {
				byte[] responseReceived = await nodeB.RequestAsync(request, 3000);
				Console.WriteLine($"ResponseB Received:	'x'");
			} catch (TimeoutException) {
				Console.WriteLine("RequestB Received:	✓");
			}

			if (await Task.WhenAny(requestSource.Task, Task.Delay(1000)) == requestSource.Task) {
				var result = IsEqualArr(request, requestSource.Task.Result) ? '✓' : 'x';
				Console.WriteLine($"Request Received:	{result}");
			} else {
				Console.WriteLine($"Request Received:	timeout");
			}
			
			await nodeA.BroadcastAsync(broadcast);
			await nodeB.BroadcastAsync(broadcast);
		
			if (await Task.WhenAny(broadcastASource.Task, Task.Delay(1000)) == broadcastASource.Task) {
				var result = IsEqualArr(broadcast, broadcastASource.Task.Result) ? '✓' : 'x';
				Console.WriteLine($"BroadcastA Received:	{result}");
			} else {
				Console.WriteLine($"BroadcastA Received:	timeout");
			}

			if (await Task.WhenAny(broadcastBSource.Task, Task.Delay(1000)) == broadcastBSource.Task) {
				var result = IsEqualArr(broadcast, broadcastBSource.Task.Result) ? '✓' : 'x';
				Console.WriteLine($"BroadcastB Received:	{result}");
			} else {
				Console.WriteLine($"BroadcastB Received:	timeout");
			}

		}

		static async Task FileShareTest() {

			IDBInterface dbConnection = await SQLiteDBConnection.CreateConnection("./FileShareTest/");
			FileShareNode fsnA = new FileShareNode(11111, dbConnection);
			FileShareNode fsnB = new FileShareNode(22222, dbConnection);

			IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());	
			IPAddress ipAddress = ipHostInfo.AddressList[0];
			IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11111);

			await fsnB.ConnectAsync(remoteEP);

			byte[] data = Encoding.ASCII.GetBytes("Hello World");

			await fsnA.StoreFileOnNetwork(data);

			using (SHA256 sha = SHA256.Create()) {
				byte[] hash = sha.ComputeHash(data);
				var result = (await dbConnection.ContainsKey(hash)) ? '✓' : 'x';
				Console.WriteLine($"Data Stored:	{result}");			
				
				byte[] received = await fsnA.GetFileOnNetwork(hash);
				result = IsEqualArr(received, data) ? '✓' : 'x';
				Console.WriteLine($"Data Received:	{result}");			
			}

			fsnA.Dispose();
			fsnB.Dispose();

		}


		static async Task TestSuite(string[] args) {
			IDBInterface dbConnection = await SQLiteDBConnection.CreateConnection("./FileShareTest/");

			byte[] testKey = Encoding.ASCII.GetBytes("Hello");
			byte[] testValue = Encoding.ASCII.GetBytes("World");
			byte[] testValue2 = Encoding.ASCII.GetBytes("World!!");

			DataPair pair = new DataPair(testKey, testValue);
			DataPair pair2 = new DataPair(testKey, testValue2);


			System.Console.WriteLine("=================================");
			System.Console.WriteLine("Testing data table commands");
			var result = ' ';

			// Check if data contains key, before inserting it
			result = (await dbConnection.ContainsKey(pair.Key)) == false ? '✓' : 'x';
			Console.WriteLine($"Check for pair before insert:\t{result}");

			// Check if pair is inserted into data
			result = await dbConnection.InsertPair(pair) == true ? '✓' : 'x';
			Console.WriteLine($"Insert new pair:\t{result}");

			// Check if data contains key, after inserting it
			result = (await dbConnection.ContainsKey(pair.Key)) == true ? '✓' : 'x';
			Console.WriteLine($"Check for pair after insert:\t{result}");

			// Try to insert duplicate key
			result = await dbConnection.InsertPair(pair) == false ? '✓' : 'x';
			Console.WriteLine($"Try insert duplicate key:\t{result}");

			// Read value from known key
			var a = await dbConnection.SelectData("value", "key", pair.Key);
			result = IsEqualArr(a, pair.Value) ? '✓' : 'x';
			Console.WriteLine($"Get value from key:\t{result}");

			// Update value
			result = await dbConnection.UpdatePair(pair2) ? '✓' : 'x';
			Console.WriteLine($"Update value:\t{result}");
			
			// Read updated value
			var c = await dbConnection.SelectData("value", "key", pair.Key);
			result = IsEqualArr(c, pair2.Value) ? '✓' : 'x';
			Console.WriteLine($"Get updated value:\t{result}");

			// Read key from known value
			var b = await dbConnection.SelectData("key", "value", pair2.Value);
			if (b == null) result = 'x';
			else result = IsEqualArr(b, pair2.Key) ? '✓' : 'x';
			Console.WriteLine($"Get key from value:\t{result}");

			// Delete key
			result = await dbConnection.RemoveKey(pair.Key) == true ? '✓' : 'x';
			Console.WriteLine($"Remove key:\t{result}");

			// Check if data contains key, after removing it
			result = (await dbConnection.ContainsKey(pair.Key)) == false ? '✓' : 'x';
			Console.WriteLine($"Check for key after delete:\t{result}");
			System.Console.WriteLine("=================================");

			
			string host = "Hello";
			int port = 111111;			
			string host2 = "Hello2";
			int port2 = 111112;			
			
			PeerInfo peer = new PeerInfo(host, port);
			PeerInfo peer2 = new PeerInfo(host, port2);
			PeerInfo peer3 = new PeerInfo(host2, port);
			
			System.Console.WriteLine("=================================");
			System.Console.WriteLine("Testing peer table commands");
			// Check if data contains key, before inserting it
			result = await dbConnection.ContainsPeer(peer) == false ? '✓' : 'x';
			Console.WriteLine($"Check for non-existing peer:\t{result}");

			// Check if pair is inserted into data
			result = await dbConnection.InsertPeer(peer) == true ? '✓' : 'x';
			Console.WriteLine($"Inserting new peer:\t{result}");

			// Check if data contains key, after inserting it
			result = await dbConnection.ContainsPeer(peer) == true ? '✓' : 'x';
			Console.WriteLine($"Check for new peer:\t{result}");

			// Try to insert duplicate key
			result = await dbConnection.InsertPeer(peer) == false ? '✓' : 'x';
			Console.WriteLine($"Try to insert duplicate peer:\t{result}");

			// Insert peer at same host, different port
			result = await dbConnection.InsertPeer(peer2) == true ? '✓' : 'x';
			Console.WriteLine($"Insert new peer w/ same host:\t{result}");

			// Insert peer at same host, different port
			result = await dbConnection.InsertPeer(peer3) == true ? '✓' : 'x';
			Console.WriteLine($"Insert new peer w/ same port:\t{result}");

			// Delete peer
			result = await dbConnection.RemovePeer(peer) == true ? '✓' : 'x';
			Console.WriteLine($"Remove peer1:\t{result}");

			result = await dbConnection.RemovePeer(peer2) == true ? '✓' : 'x';
			Console.WriteLine($"Remove peer2:\t{result}");
			
			result = await dbConnection.RemovePeer(peer3) == true ? '✓' : 'x';
			Console.WriteLine($"Remove peer3:\t{result}");

			// Check if data contains key, after removing it
			result = await dbConnection.ContainsPeer(peer) == false ? '✓' : 'x';
			Console.WriteLine($"Check for peer1 after delete:\t{result}");

			Console.WriteLine("Closing DB Connection");
			dbConnection.Close();
			System.Console.WriteLine("=================================");
		
			System.Console.WriteLine("=================================");
			System.Console.WriteLine("Testing Nodes");
			await NodeTest1();
			System.Console.WriteLine("=================================");
			
			System.Console.WriteLine("=================================");
			System.Console.WriteLine("Testing File Share Nodes");
			await FileShareTest();
			System.Console.WriteLine("=================================");

		}

		public static bool IsEqualArr(byte[] arr1, byte[] arr2) {
			if (arr1.Length != arr2.Length) return false;

			for (int i = 0; i < arr1.Length; i++) {
				if (arr1[i] != arr2[i]) return false;
			}

			return true;
		}

	}
}
