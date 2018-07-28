// -----------------------------------------------------------------------
// <copyright file="HubConnection.cs" company="John Lynch">
//   This file is licensed under the MIT license.
//   Copyright (c) 2018 John Lynch
// </copyright>
// -----------------------------------------------------------------------

namespace Harmony {
	using System;
	using System.Net.WebSockets;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	using Harmony.CommandParameters;

	using Newtonsoft.Json;

	/// <summary>
	///     A WebSocket connection to a Hub and low-level methods to interface with it
	/// </summary>
	internal class HubConnection {
		/// <summary>
		///     The ID to use when sending messages over the WebSocket
		/// </summary>
		private readonly DeviceID MessageID;

		/// <summary>
		///     The WebSocket connected to the Hub
		/// </summary>
		private readonly ClientWebSocket WebSocket = new ClientWebSocket();

		/// <summary>
		///     Initializes a new instance of the <see cref="HubConnection" /> class.
		/// </summary>
		/// <param name="messageID">The ID to use when sending messages over the WebSocket</param>
		public HubConnection(DeviceID messageID) => this.MessageID = messageID;

		/// <summary>
		///     Connects to the WebSocket
		/// </summary>
		/// <param name="hostname">The hostname of the Hub, without a port</param>
		/// <param name="hubRemoteId">The RemoteId of the Hub</param>
		/// <returns>When the WebSocket has connected</returns>
		public async Task Connect(string hostname, string hubRemoteId) {
			string FullUrl = $"ws://{hostname}:8088/?domain=svcs.myharmony.com&hubId={hubRemoteId}";
			await this.WebSocket.ConnectAsync(new Uri(FullUrl), CancellationToken.None);
		}

		/// <summary>
		///     Disconnects from the WebSocket
		/// </summary>
		/// <returns>When the WebSocket has disconnected</returns>
		public async Task Disconnect() {
			await this.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
		}

		/// <summary>
		///     Waits for a message with serialized response data from the websocket
		/// </summary>
		/// <returns>The <see cref="StringResponse" /></returns>
		public async Task<StringResponse> ReceiveMessage() =>
			JsonConvert.DeserializeObject<StringResponse>(await this.ReceiveStringMessage());

		/// <summary>
		///     Waits for a message from the websocket
		/// </summary>
		/// <typeparam name="T">The type to deserialize the JSON into</typeparam>
		/// <returns>The response data wrapped in a <see cref="Response{T}" /> object</returns>
		public async Task<Response<T>> ReceiveMessageAs<T>() =>
			JsonConvert.DeserializeObject<Response<T>>(await this.ReceiveStringMessage());

		/// <summary>
		///     Sends a command with no parameters
		/// </summary>
		/// <param name="commandName">The name of the command to execute</param>
		/// <returns>When the command has been sent</returns>
		public async Task SendCommand(string commandName) => await this.SendCommand(commandName, null);

		/// <summary>
		///     Sends a command with the specified parameters
		/// </summary>
		/// <param name="commandName">The name of the command to execute</param>
		/// <param name="parameters">The parameters to execute the command with</param>
		/// <returns>When the command has been sent</returns>
		public async Task SendCommand(string commandName, CommandParams parameters) {
			Command Command = new Command(commandName, parameters, this.MessageID.Next());
			await this.SendCommand(Command);
		}

		/// <summary>
		///     Sends a command with no parameters, and recieves a response of type <typeparamref name="T" />
		/// </summary>
		/// <typeparam name="T">The type of response data</typeparam>
		/// <param name="commandName">The name of the command to execute</param>
		/// <returns>The response data wrapped in a <see cref="Response{T}" /> object</returns>
		public async Task<Response<T>> SendCommand<T>(string commandName) =>
			await this.SendCommand<T>(commandName, null);

		/// <summary>
		///     Sends a command with the specified parameters, and recieves a response of type T
		/// </summary>
		/// <typeparam name="T">The type of response data</typeparam>
		/// <param name="commandName">The name of the command to execute</param>
		/// <param name="parameters">The parameters to execute the command with</param>
		/// <returns>The response data wrapped in a <see cref="Response{T}" /> object</returns>
		public async Task<Response<T>> SendCommand<T>(string commandName, CommandParams parameters) {
			string NextID = this.MessageID.Next();
			Command Command = new Command(commandName, parameters, NextID);

			// first, send the command
			await this.SendCommand(Command);

			// then, wait until we get the right response
			while (true)
				try {
					Response<T> Response = await this.ReceiveMessageAs<T>();
					if (Response.ID == NextID) return Response;
				}
				catch (JsonException) {
					// ignored, wait for the next message
					////Console.WriteLine("Exception Thrown");
				}
		}

		/// <summary>
		///     Recieves a message on the WebSocket
		/// </summary>
		/// <returns>The data received</returns>
		private async Task<string> ReceiveStringMessage() {
			string Message = string.Empty;
			while (true) {
				ArraySegment<byte> Buffer = new ArraySegment<byte>(new byte[1000]);
				WebSocketReceiveResult Result = await this.WebSocket.ReceiveAsync(Buffer, CancellationToken.None);
				Message += Encoding.UTF8.GetString(Buffer.Array ?? new byte[0], 0, Result.Count);
				
				if (Result.EndOfMessage) return Message;
			}
		}

		/// <summary>
		///     Sends a command over the WebSocket
		/// </summary>
		/// <param name="command">The command to send</param>
		/// <returns>When the command has been sent</returns>
		private async Task SendCommand(Command command) {
			string CommandJson = JsonConvert.SerializeObject(command);

			// need to surround in another object
			await this.SendString("{\"hbus\": " + CommandJson + "}");
		}

		/// <summary>
		///     Sends a string over the WebSocket
		/// </summary>
		/// <param name="data">The string data to send</param>
		/// <returns>When the data has been sent</returns>
		private async Task SendString(string data) {
			ArraySegment<byte> DataBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(data));
			await this.WebSocket.SendAsync(DataBytes, WebSocketMessageType.Text, true, CancellationToken.None);
		}
	}
}